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

    // 🔥 Lock-freie Steuerung
    private long _lastBigOrderTicks;

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
         Func<int> getConcurrency,
        int bigOrderLines = 0,
        int bigOrderIntervalMinutes = 0,
        string? promotionMediumNo = null,
        string? promotionMediumTrgGrpNo = null,
        decimal shippingChargeAmount = 0
    )
    : base(client, stats, workerName, companyName, Math.Max(1, rpm), getConcurrency)
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

        var now = DateTime.UtcNow;

        if (_bigOrderIntervalMinutes > 0)
        {
            var offsetSeconds = Random.Shared.Next(0, _bigOrderIntervalMinutes * 60);
            var start = now - TimeSpan.FromSeconds(offsetSeconds);
            _lastBigOrderTicks = start.Ticks;
        }
        else
        {
            _lastBigOrderTicks = now.Ticks;
        }
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        string json;

        // =========================
        // 🔥 BIG ORDER (LOCK-FREE)
        // =========================
        bool createBigOrder = false;

        if (_bigOrderLines > 0 && _bigOrderIntervalMinutes > 0)
        {
            var last = new DateTime(Interlocked.Read(ref _lastBigOrderTicks));

            if ((now - last).TotalMinutes >= _bigOrderIntervalMinutes)
            {
                if (Interlocked.Exchange(ref _lastBigOrderTicks, now.Ticks) != last.Ticks)
                {
                    createBigOrder = true;
                }
            }
        }

        if (createBigOrder)
        {
            json = _payloadPool.CreateBigOrder(_bigOrderLines);
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

        // 🔁 Refill
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
        payload["orderDateTime"] = now.ToString("o");

        // =========================
        // 🔥 PROMOTION
        // =========================
        if (!string.IsNullOrEmpty(_promotionMediumNo))
            payload["promotionMediumNo"] = _promotionMediumNo;

        if (!string.IsNullOrEmpty(_promotionMediumTrgGrpNo))
            payload["promotionMediumTrgGrpNo"] = _promotionMediumTrgGrpNo;

        var newJson = JsonSerializer.Serialize(payload);

        var url = $"{_serviceRoot}{_apiRoot}{_endpoint}"
            .Replace("{company}", _companyId);

        using var content = new StringContent(newJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content, token);

        await response.Content.LoadIntoBufferAsync();

        // =========================
        // 🔥 RETRY + JITTER
        // =========================
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;

            if (status == 429 || status >= 500)
            {
                await Task.Delay(200 + Random.Shared.Next(0, 200), token);
            }
        }
        else
        {
            // =========================
            // 🔥 ORDERSTATUS POOL
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
                // bewusst ignorieren
            }
        }

        return response;
    }
}