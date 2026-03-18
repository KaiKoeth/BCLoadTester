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
        string workerName)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _orderStatusPool = orderStatusPool;
        _payloadPool = payloadPool;
        _serviceRoot = serviceRoot;
        _apiRoot = apiRoot;
        _endpoint = endpoint;
        _companyId = companyId;
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        // 🔥 Pool Size für UI (live)
        _stats.SetPoolSize(_workerName, _company, _payloadPool.Count);

        // 🔥 JSON aus Pool holen
        var json = _payloadPool.GetRandom();

        if (string.IsNullOrWhiteSpace(json))
        {
            await Task.Delay(200, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        // 🔥 JSON → Dictionary
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (payload == null)
        {
            await Task.Delay(200, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        // 🔥 eindeutige OrderNo (max 20 Zeichen!)
        var now = DateTime.UtcNow;

        var id = $"{now:yyMMddHHmmssfff}{Interlocked.Increment(ref _counter) % 1000:000}";

        // 🔥 Safety (falls sich Format mal ändert)
        if (id.Length > 20)
            id = id.Substring(0, 20);

        // 🔥 Felder überschreiben
        payload["shopOrderNumber"] = id;
        payload["externalReferenceNo"] = id;
        payload["externalDocumentNo"] = id;
        payload["basketId"] = id;
        payload["orderDateTime"] = now;

        // 🔥 zurück zu JSON
        var newJson = JsonSerializer.Serialize(payload);

        var url = $"{_serviceRoot}{_apiRoot}{_endpoint}"
            .Replace("{company}", _companyId);

        using var content = new StringContent(newJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content, token);

        // 🔥 wichtig für Connection Reuse
        await response.Content.LoadIntoBufferAsync();

        // 🔥 Retry bleibt
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }
        }

        // 🔥 KEIN throw mehr!
        return response;
    }
}