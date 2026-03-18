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
        try
        {
            // 🔥 Pool Size live tracken (wie vorher)
            _stats.SetPoolSize(_workerName, _company, _pool.Count);

            string? customerNo;

            // 🔥 Hybrid-Verhalten bleibt exakt
            if (_rnd.Value!.Next(100) < 10)
                customerNo = _pool.TakeRandom();
            else
                customerNo = _pool.GetRandom();

            // 🔥 PoolEmpty Verhalten bleibt (inkl. Delay!)
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
            // 🔥 identisches Fehlerverhalten wie vorher
            var errorText = ex is TaskCanceledException
                ? "Timeout"
                : ex.Message; // ⚠️ wichtig: Message wegen PoolEmpty

            throw new Exception(errorText);
        }
    }
}