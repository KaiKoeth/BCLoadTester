namespace BCLoadtester.Loadtest;

using System.Text.Json;

public class WebOrderPayloadPool
{
    private readonly List<string> _payloads;
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public int Count => _payloads.Count;

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

        for (int i = 0; i < size; i++)
        {
            _payloads.Add(CreatePayload(profile, minLines, maxLines));
        }
    }

    public string GetRandom()
    {
        return _payloads[Random.Shared.Next(_payloads.Count)];
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

        var id = Guid.NewGuid().ToString("N")[..20];
        var now = DateTime.UtcNow;

        var payload = new
        {
            externalReferenceNo = id,
            externalDocumentNo = id,
            basketId = id,
            shopOrderNumber = id,
            orderDateTime = now,

            sellToCustomerNo = customer.No,
            sellToCustSalutationCode = customer.salutation,
            sellToCustomerName = customer.Name,
            sellToCustomerFirstName = Utils.SafeData.TrimTo(customer.FirstName,30),
            sellToCustomerSurname = Utils.SafeData.TrimTo(customer.Surname,30),
            sellToStreet = customer.Street,
            sellToHouseNo = customer.HouseNo,
            sellToAddress = customer.Address,
            sellToCity = customer.City,
            sellToPostCode = customer.PostCode,
            sellToCountryRegionCode = customer.countryregion,

            shipToCustSalutationCode = customer.salutation,
            shipToName = customer.Name,
            shipToCustomerFirstName =  Utils.SafeData.TrimTo(customer.FirstName,30),
            shipToCustomerSurname = Utils.SafeData.TrimTo(customer.Surname,30),
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