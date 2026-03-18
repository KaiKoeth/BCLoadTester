namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class PhoneticSearchWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _url;

    private readonly Random _rnd = new Random(); // 🔥 bewusst NICHT ThreadLocal

    public PhoneticSearchWorker(
        HttpClient client,
        List<CustomerEntry> customers,
        string serviceRoot,
        string endpoint,
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
        string workerName)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm))
    {
        _customers = customers ?? new List<CustomerEntry>();

        _url = $"{serviceRoot}{endpoint}?company={companyId}";
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
            var entry = _customers[_rnd.Next(_customers.Count)];

            var payload = new
            {
                name = entry.Name,
                address = entry.Address,
                postalCode = entry.PostalCode
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(_url, content, token);

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