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

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        var NameEntry = _customers[_rnd.Value!.Next(_customers.Count)];
        var AddrEntry = _customers[_rnd.Value!.Next(_customers.Count)];

        string email = $"loadtest{Guid.NewGuid():N}@test.de";

        var payload = new
        {
            creditLimit = 0,
            creditworthinessClass = 0,
            type = "Person",
            salutationCode = "M",
            displayName = Utils.SafeData.TrimTo(NameEntry.Name, 30),
            firstName = NameEntry.Firstname,
            surname = NameEntry.Surname,
            addressLine1 = AddrEntry.Address,
            addressLine2 = "",
            street = AddrEntry.Address,
            houseNo = "1",
            postalCode = AddrEntry.PostalCode,
            city = AddrEntry.City,
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

        // 🔥 Retry bleibt
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                await Task.Delay(200, token);
            }
        }

        // 🔥 GANZ WICHTIG: IMMER zurückgeben!
        return response;
    }
}