namespace BCLoadtester.Loadtest;

using System.Text;
using System.Text.Json;

public class ShipToAddressCreateWorker : BaseWorker
{
    private readonly List<CustomerEntry> _customers;
    private readonly string _endpointBase;

    // 🔥 thread-safe Random (ersetzt Lock!)
    private readonly ThreadLocal<Random> _rnd = new(() => new Random());

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
         string workerName, Func<int> getConcurrency)
        : base(client, stats, workerName, companyName, Math.Max(1, rpm), getConcurrency)
    {
        _customers = customers ?? new List<CustomerEntry>();

        _endpointBase = $"{serviceRoot}{apiRoot}{endpoint}"
            .Replace("{company}", companyId);
    }

    protected override async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
    {
        // =========================
        // 🔹 Fallback
        // =========================
        if (_customers.Count == 0)
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }

        // =========================
        // 🔹 Random Auswahl (ohne Lock!)
        // =========================
        var rnd = _rnd.Value!;

        var customer = _customers[rnd.Next(_customers.Count)];
        var custAddress = _customers[rnd.Next(_customers.Count)];

        // =========================
        // 🔹 URL bauen
        // =========================
        var url = _endpointBase.Replace(
            "{customer}",
            customer.SystemId.ToString()
        );

        // =========================
        // 🔹 Payload
        // =========================
        var payload = new
        {
            phoneNumber = "(555) 555-1234",
            defaultShiptoAddress = false,
            salutationCode = "W",
            displayName = customer.Name,
            firstName = customer.Firstname,
            surname = customer.Surname,
            addressLine1 = custAddress.Address,
            addressLine2 = "TEST",
            street = custAddress.Address,
            houseNo = "1",
            postalCode = custAddress.PostalCode,
            city = custAddress.City,
            country = "DE"
        };

        var json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("If-Match", "*");
        request.Content = content;

        // =========================
        // 🔹 Request
        // =========================
        var response = await _client.SendAsync(request, token);

        // 🔥 wichtig für Connection-Reuse (statt ReadAsStringAsync!)
        await response.Content.LoadIntoBufferAsync();

        // =========================
        // 🔹 Retry + Jitter
        // =========================
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;

            if (status == 429 || status >= 500)
            {
                await Task.Delay(200 + rnd.Next(0, 200), token);
            }
        }

        // =========================
        // 🔹 Kein throw
        // =========================
        return response;
    }
}