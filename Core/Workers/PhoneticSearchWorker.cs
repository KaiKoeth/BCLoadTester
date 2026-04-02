namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class PhoneticSearchWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _url;

    // 🔥 thread-safe Random (wichtig für Parallelität)
    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public PhoneticSearchWorker(
        HttpClient client,
        List<CustomerEntry> customers,
        string serviceRoot,
        string endpoint,
        string companyId,
        string companyName,
        int rpm,
        Statistics stats,
         string workerName, Func<int> getConcurrency)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm), getConcurrency)
    {
        _customers = customers ?? new List<CustomerEntry>();

        _url = $"{serviceRoot}{endpoint}?company={companyId}";
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        // =========================
        // 🔹 Fallback wenn keine Daten
        // =========================
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        // =========================
        // 🔹 Zufälligen Kunden wählen (thread-safe)
        // =========================
        var customer = _customers[_rnd.Value!.Next(_customers.Count)];

        // =========================
        // 🔹 Payload erstellen
        // =========================
        var payload = new
        {
            name = customer.Name,
            address = customer.Address,
            postalCode = customer.PostalCode
        };

        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // =========================
        // 🔹 Request senden
        // =========================
        var response = await _client.PostAsync(_url, content, token);

        // 🔥 wichtig für Connection-Reuse
        await response.Content.LoadIntoBufferAsync();

        // =========================
        // 🔹 Retry-Logik (inkl. Jitter)
        // =========================
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;

            if (status == 429 || status >= 500)
            {
                await Task.Delay(200 + _rnd.Value!.Next(0, 200), token);
            }
        }

        // =========================
        // 🔹 KEIN throw → BaseWorker zählt Fehler
        // =========================
        return response;
    }
}