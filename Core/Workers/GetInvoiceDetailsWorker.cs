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
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
        string workerName)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _customers = customers;

        // 🔥 exakt wie vorher (HARDCODE bewusst erhalten!)
        _endpointBase =
            $"{serviceRoot}/API/macits/baur/v1.0/companies({companyId})" +
            "/salesInvoiceHeaders?$expand=salesInvoiceLines&$filter=customerNumber eq '{customer}'";
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // 🔥 exakt wie vorher
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return;
        }

        try
        {
            var customer = _customers[_rnd.Value!.Next(_customers.Count)];

            // 🔥 Encoding bleibt erhalten
            var encodedCustomer = Uri.EscapeDataString(customer);

            var url = _endpointBase.Replace("{customer}", encodedCustomer);

            var response = await _client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                token);

            // 🔥 wichtig für Connection Pooling
            await response.Content.LoadIntoBufferAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = $"{(int)response.StatusCode} {response.ReasonPhrase}";

                // 🔥 Retry exakt wie vorher
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    await Task.Delay(200, token);
                }

                throw new Exception(errorText);
            }
        }
        catch (Exception ex)
        {
            // 🔥 identisches Verhalten wie vorher
            var errorText = ex is TaskCanceledException
                ? "Timeout"
                : ex.GetType().Name;

            throw new Exception(errorText);
        }
    }
}