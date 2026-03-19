using System.Collections.Concurrent;
using System.Text.Json;

namespace BCLoadtester.Loadtest;

public class WebOrderPayloadPool
{
    private readonly List<string> _payloads; // 🔥 bleibt für Random
    private readonly ConcurrentQueue<string> _queue; // 🔥 neu für echten Verbrauch

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public int Count => _queue.Count; // 🔥 jetzt echter Live-Wert

    public WebOrderPayloadPool(
        string companyName,
        WebOrderProfile profile,
        int minLines,
        int maxLines,
        int size)
    {
        if (profile.Customers.Count == 0)
            throw new Exception($"{companyName}: RandomProfile contains no customers");

        if (profile.Items.Count == 0)
            throw new Exception($"{companyName}: RandomProfile contains no items");

        _payloads = new List<string>(size);
        _queue = new ConcurrentQueue<string>();

        for (int i = 0; i < size; i++)
        {
            var payload = CreatePayload(profile, minLines, maxLines);

            _payloads.Add(payload);   // 🔹 für Random
            _queue.Enqueue(payload); // 🔹 für Dequeue
        }
    }

    // =========================
    // 🔹 ALT (BLEIBT)
    // =========================
    public string GetRandom()
    {
        return _payloads[Random.Shared.Next(_payloads.Count)];
    }

    // =========================
    // 🔥 NEU: echter Verbrauch
    // =========================
    public bool TryGet(out string payload)
    {
        return _queue.TryDequeue(out payload);
    }

    // =========================
    // 🔁 OPTIONAL: Refill (für später)
    // =========================
    public void Refill(IEnumerable<string> items)
    {
        foreach (var item in items)
            _queue.Enqueue(item);
    }

    private static string CreatePayload(
        WebOrderProfile profile,
        int minLines,
        int maxLines)
    {
        var rnd = Random.Shared;

        var customer =
            profile.Customers[rnd.Next(profile.Customers.Count)];

        int lines =
            rnd.Next(minLines, maxLines + 1);

        var quicksaleslines = new List<object>(lines);

        for (int i = 0; i < lines; i++)
        {
            var item =
                profile.Items[rnd.Next(profile.Items.Count)];

            quicksaleslines.Add(new
            {
                lineNo = ((i + 1) * 10000).ToString(),
                type = "Item",
                no = item.No,
                description = item.Description,
                quantity = 1,
                unitPrice = item.UnitPrice
            });
        }

        var payload = new
        {
            externalReferenceNo = "",
            externalDocumentNo = "",
            basketId = "",
            shopOrderNumber = "",
            orderDateTime = DateTime.UtcNow,

            sellToCustomerNo = customer.No,
            sellToCustSalutationCode = customer.salutation,
            sellToCustomerName = customer.Name,
            sellToCustomerFirstName = Utils.SafeData.TrimTo(customer.FirstName, 30),
            sellToCustomerSurname = Utils.SafeData.TrimTo(customer.Surname, 30),
            sellToStreet = customer.Street,
            sellToHouseNo = customer.HouseNo,
            sellToAddress = customer.Address,
            sellToCity = customer.City,
            sellToPostCode = customer.PostCode,
            sellToCountryRegionCode = customer.countryregion,

            shipToCustSalutationCode = customer.salutation,
            shipToName = customer.Name,
            shipToCustomerFirstName = Utils.SafeData.TrimTo(customer.FirstName, 30),
            shipToCustomerSurname = Utils.SafeData.TrimTo(customer.Surname, 30),
            shipToStreet = customer.Street,
            shipToHouseNo = customer.HouseNo,
            shipToAddress = customer.Address,
            shipToCity = customer.City,
            shipToPostCode = customer.PostCode,
            shipToCountryRegionCode = customer.countryregion,

            paymentmethodcode = customer.paymentmethod,

            quicksaleslines = quicksaleslines
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }
}