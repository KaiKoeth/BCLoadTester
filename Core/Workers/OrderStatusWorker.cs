namespace BCLoadtester.Loadtest;

public class OrderStatusWorker : BaseWorker
{
    private readonly OrderStatusPool _pool;
    private readonly string _endpointBase;

    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public OrderStatusWorker(
        HttpClient client,
        OrderStatusPool pool,
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
        _pool = pool;

        _endpointBase = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // 🔥 Pool Size live tracken (bleibt!)
        _stats.SetPoolSize(_workerName, _company, _pool.Count);

        string? customerNo;

        // 🔥 Hybrid Verhalten bleibt
        if (_rnd.Value!.Next(100) < 10)
            customerNo = _pool.TakeRandom();
        else
            customerNo = _pool.GetRandom();

        // 🔥 Pool leer → eigener Fehler
        if (customerNo == null)
        {
            await Task.Delay(200, token);
            throw new Exception("PoolEmpty");
        }

        var separator = _endpointBase.Contains("?") ? "&" : "?";

        var encodedCustomer = Uri.EscapeDataString(customerNo);

        var url = $"{_endpointBase}{separator}$filter=customerNumber eq '{encodedCustomer}'";

        var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            token);

        await response.Content.LoadIntoBufferAsync();

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            // 🔥 Retry bleibt
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }

            throw new Exception(
                $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase} | {body}"
            );
        }
    }
}