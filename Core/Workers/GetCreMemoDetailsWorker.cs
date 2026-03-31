namespace BCLoadtester.Loadtest;

public class GetCreMemoDetailsWorker : BaseWorker
{
    private readonly List<string> _customers;
    private readonly string _endpointBase;

    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public GetCreMemoDetailsWorker(
        HttpClient client,
        List<string> customers,
        string serviceRoot,
        string apiRoot,
        string endpoint,
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
         string workerName, Func<int> getConcurrency)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm), getConcurrency)
    {
        _customers = customers;

        _endpointBase = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        var customer = _customers[_rnd.Value!.Next(_customers.Count)];

        // 🔥 Encoding bleibt
        var encodedCustomer = Uri.EscapeDataString(customer);

        var url = _endpointBase.Replace("{customer}", encodedCustomer);

        var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            token);

        await response.Content.LoadIntoBufferAsync();

        // 🔥 Retry bleibt
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200 + _rnd.Value!.Next(0, 200), token);
            }
        }

        // 🔥 KEIN throw mehr
        return response;
    }
}