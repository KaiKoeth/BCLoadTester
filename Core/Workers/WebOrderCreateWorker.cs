namespace BCLoadtester.Loadtest;

using System.Text;

public class WebOrderCreateWorker : BaseWorker
{
    private readonly OrderStatusPool _orderStatusPool;
    private readonly WebOrderPayloadPool _payloadPool;
    private readonly string _serviceRoot;
    private readonly string _apiRoot;
    private readonly string _endpoint;
    private readonly string _companyId;

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

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var payload = _payloadPool.GetRandom();

        var url = $"{_serviceRoot}{_apiRoot}{_endpoint}"
            .Replace("{company}", _companyId);

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content, token);

        // wichtig für Connection Reuse
        await response.Content.LoadIntoBufferAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorText = $"{(int)response.StatusCode} {response.ReasonPhrase}";

            // Retry bei BC-Überlast
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }

            throw new Exception(errorText);
        }

        // Optional: Pool Size für UI anzeigen
        _stats.SetPoolSize(_workerName, _company, _payloadPool.Count);
    }
}