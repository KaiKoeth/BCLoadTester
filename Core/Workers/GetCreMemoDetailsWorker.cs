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
        string workerName)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _customers = customers;

        _endpointBase = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return;
        }

        var customer = _customers[_rnd.Value!.Next(_customers.Count)];

        // 🔥 Encoding bleibt unverändert
        var encodedCustomer = Uri.EscapeDataString(customer);

        var url = _endpointBase.Replace("{customer}", encodedCustomer);

        var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            token);

        await response.Content.LoadIntoBufferAsync();

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            // 🔥 Retry unverändert
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }

            // 🔥 volle Fehlerdetails
            throw new Exception(
                $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase} | {body}"
            );
        }
    }
}