namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class PhoneticSearchWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _url;

    private readonly Random _rnd = new Random(); // bewusst so lassen

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

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

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