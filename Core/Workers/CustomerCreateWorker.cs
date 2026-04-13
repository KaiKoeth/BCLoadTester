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
        StatisticsService stats, // ✅ konsistent benannt
        string workerName,
        Func<int> getConcurrency
    )
        : base(client, stats, workerName, companyName, Math.Max(1, rpm), getConcurrency)
    {
        _customers = customers ?? new List<CustomerEntry>(); // 🔥 Null-Safety

        _url = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        var rnd = _rnd.Value!; // 🔥 einmal ziehen (ThreadLocal Zugriff reduzieren)

        var nameEntry = _customers[rnd.Next(_customers.Count)];
        var addrEntry = _customers[rnd.Next(_customers.Count)];

        string email = $"loadtest{Guid.NewGuid():N}@test.de";

        var payload = new
        {
            creditLimit = 0,
            creditworthinessClass = 0,
            type = "Person",
            salutationCode = "M",
            displayName = Utils.SafeData.TrimTo(nameEntry.Name, 30),
            firstName = nameEntry.Firstname,
            surname = nameEntry.Surname,
            addressLine1 = addrEntry.Address,
            addressLine2 = "",
            street = addrEntry.Address,
            houseNo = "1",
            postalCode = addrEntry.PostalCode,
            city = addrEntry.City,
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

        await response.Content.LoadIntoBufferAsync();

        // 🔥 Retry-Logik unverändert
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;

            if (status == 429 || status >= 500)
            {
                await Task.Delay(200 + rnd.Next(0, 200), token);
            }
        }

        return response; // 🔥 zwingend
    }
}