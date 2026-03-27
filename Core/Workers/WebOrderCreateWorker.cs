namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class WebOrderCreateWorker : BaseWorker
{
    private readonly OrderStatusPool _orderStatusPool;
    private readonly WebOrderPayloadPool _payloadPool;
    private readonly string _serviceRoot;
    private readonly string _apiRoot;
    private readonly string _endpoint;
    private readonly string _companyId;

    private readonly int _bigOrderLines;
    private readonly int _bigOrderIntervalMinutes;
    private readonly string _promotionMediumNo;
    private readonly string _promotionMediumTrgGrpNo;
    private readonly decimal _shippingChargeAmount;

    private DateTime _lastBigOrder = DateTime.MinValue;
    private readonly object _bigOrderLock = new();

    private static int _counter = 0;

    public WebOrderCreateWorker(
        HttpClient client,
        OrderStatusPool orderStatusPool,
        WebOrderPayloadPool payloadPool,
        string serviceRoot,
        string apiRoot,
        string endpoint,
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
        string workerName,
        int bigOrderLines = 0,
        int bigOrderIntervalMinutes = 0,
        string? promotionMediumNo = null,
        string? promotionMediumTrgGrpNo = null,
        decimal shippingChargeAmount = 0
    )
    : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _orderStatusPool = orderStatusPool;
        _payloadPool = payloadPool;
        _serviceRoot = serviceRoot;
        _apiRoot = apiRoot;
        _endpoint = endpoint;
        _companyId = companyId;

        _bigOrderLines = bigOrderLines;
        _bigOrderIntervalMinutes = bigOrderIntervalMinutes;
        _promotionMediumNo = promotionMediumNo ?? "";
        _promotionMediumTrgGrpNo = promotionMediumTrgGrpNo ?? "";
        _shippingChargeAmount = shippingChargeAmount;

        _lastBigOrder = DateTime.UtcNow;
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        string json;
        var now = DateTime.UtcNow;

        // =========================
        // 🔥 BIG ORDER LOGIC (FINAL)
        // =========================
        bool createBigOrder = false;

        if (_bigOrderLines > 0 && _bigOrderIntervalMinutes > 0)
        {
            lock (_bigOrderLock)
            {
                if ((now - _lastBigOrder).TotalMinutes >= _bigOrderIntervalMinutes)
                {
                    createBigOrder = true;
                    _lastBigOrder = now;
                }
            }
        }

        if (createBigOrder)
        {
            json = _payloadPool.CreateBigOrder(_bigOrderLines);

            // 🔥 Tracking
            _stats.IncrementCustomMetric(_workerName, _company, "BigOrders");
        }
        else
        {
            if (!_payloadPool.TryGet(out json))
            {
                json = _payloadPool.GetRandom();
                await Task.Delay(50, token);
            }
        }

        // 🔁 Auto-Refill
        _payloadPool.RefillIfLow();

        // 📊 Pool Size
        _stats.SetPoolSize(_workerName, _company, _payloadPool.Count);

        if (string.IsNullOrWhiteSpace(json))
        {
            await Task.Delay(200, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        Dictionary<string, object>? payload;

        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            await Task.Delay(50, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        }

        if (payload == null)
        {
            await Task.Delay(50, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        // =========================
        // 🔥 UNIQUE ID
        // =========================
        var id = $"{now:yyMMddHHmmssfff}{Interlocked.Increment(ref _counter) % 1000:000}";

        if (id.Length > 20)
            id = id.Substring(0, 20);

        payload["shopOrderNumber"] = id;
        payload["externalReferenceNo"] = id;
        payload["externalDocumentNo"] = id;
        payload["basketId"] = id;

        // 🔥 FIX: ISO 8601 (BC sauber)
        payload["orderDateTime"] = now.ToString("o");

        // =========================
        // 🔥 PROMOTION (NEU)
        // =========================
        if (!string.IsNullOrEmpty(_promotionMediumNo))
        {
            payload["promotionMediumNo"] = _promotionMediumNo;
        }

        if (!string.IsNullOrEmpty(_promotionMediumTrgGrpNo))
        {
            payload["promotionMediumTrgGrpNo"] = _promotionMediumTrgGrpNo;
        }

        var newJson = JsonSerializer.Serialize(payload);

        var url = $"{_serviceRoot}{_apiRoot}{_endpoint}"
            .Replace("{company}", _companyId);

        using var content = new StringContent(newJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content, token);

        await response.Content.LoadIntoBufferAsync();

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }
        }
        else
        {
            // =========================
            // 🔥 IN ORDER STATUS POOL ADDEN
            // =========================
            try
            {
                if (payload.TryGetValue("sellToCustomerNo", out var custObj))
                {
                    string? customerNo = custObj switch
                    {
                        JsonElement je => je.GetString(),
                        string s => s,
                        _ => custObj?.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(customerNo))
                    {
                        _orderStatusPool.Add(customerNo);
                    }
                }
            }
            catch
            {
                // bewusst ignorieren → darf Loadtest nicht stören
            }
        }

        return response;
    }
}