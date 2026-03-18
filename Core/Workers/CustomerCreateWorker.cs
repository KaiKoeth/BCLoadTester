namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class CustomerCreateWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _url;

    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

    public CustomerCreateWorker(
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

        _url = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // 🔥 exakt wie vorher: wenn keine Daten → kein Request
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return;
        }

        try
        {
            var entry = _customers[_rnd.Value!.Next(_customers.Count)];

            string email = $"loadtest{Guid.NewGuid():N}@test.de";

            var payload = new
            {
                creditLimit = 0,
                creditworthinessClass = 0,
                type = "Person",
                salutationCode = "M",
                displayName = Utils.SafeData.TrimTo(entry.Name,30),
                firstName = Utils.SafeData.TrimTo(entry.Name,30),
                surname = "Loadtest",
                addressLine1 = entry.Address,
                addressLine2 = "",
                street = entry.Address,
                houseNo = "1",
                postalCode = entry.PostalCode,
                city = entry.City,
                country = "DE",
                phoneNumber = "",
                email = email,
                birthday = "2000-01-01",
                currencyCode = "EUR",
                guestAccount = false,
                locale = "de-DE"
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
            // 🔥 identisches Fehlerverhalten wie vorher
            var errorText = ex is TaskCanceledException
                ? "Timeout"
                : ex.GetType().Name;

            throw new Exception(errorText);
        }
    }
}