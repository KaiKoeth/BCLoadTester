namespace BCLoadtester.Loadtest;

public class GetInvoiceDetailsWorker : BaseWorker
{
    private readonly List<string> _customers;
    private readonly string _endpointBase;

    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public GetInvoiceDetailsWorker(
        HttpClient client,
        List<string> customers,
        string serviceRoot,
        string apiRoot,
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
        string workerName)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _customers = customers;

        // 🔥 HARDCODE bleibt unverändert
        _endpointBase =
            $"{serviceRoot}/API/macits/baur/v1.0/companies({companyId})" +
            "/salesInvoiceHeaders?$expand=salesInvoiceLines&$filter=customerNumber eq '{customer}'";
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
                await Task.Delay(200, token);
            }
        }

        // 🔥 KEIN throw mehr
        return response;
    }
}