namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class ShipToAddressCreateWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _endpointBase;

    private readonly Random _rnd = new Random();
    private readonly object _rndLock = new object(); // bleibt!

    public ShipToAddressCreateWorker(
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
        _customers = customers ?? new List<CustomerEntry>();

        _endpointBase = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return;
        }

        CustomerEntry customer;

        // 🔥 THREAD SAFE RANDOM bleibt
        lock (_rndLock)
        {
            customer = _customers[_rnd.Next(_customers.Count)];
        }

        var url = _endpointBase.Replace(
            "{customer}",
            customer.SystemId.ToString()
        );

        var payload = new
        {
            phoneNumber = "(555) 555-1234",
            defaultShiptoAddress = false,
            salutationCode = "W",
            displayName = customer.Name,
            firstName = customer.Name,
            surname = "Lasttest",
            addressLine1 = customer.Address,
            addressLine2 = "TEST",
            street = customer.Address,
            houseNo = "1",
            postalCode = customer.PostalCode,
            city = customer.City,
            country = "DE"
        };

        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("If-Match", "*");
        request.Content = content;

        using var response = await _client.SendAsync(request, token);

        var body = await response.Content.ReadAsStringAsync(); // bleibt!

        if (!response.IsSuccessStatusCode)
        {
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