namespace BCLoadtester.Loadtest;

public class EmailSearchWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _endpointBase;

    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public EmailSearchWorker(
        HttpClient client,
        List<CustomerEntry> customers,
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
        // 🔥 exakt wie vorher
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return;
        }

        try
        {
            var customer = _customers[_rnd.Value!.Next(_customers.Count)];

            var url = _endpointBase.Replace("{email}", customer.Email);

            var response = await _client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                token);

            // 🔥 wichtig für Connection Reuse
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