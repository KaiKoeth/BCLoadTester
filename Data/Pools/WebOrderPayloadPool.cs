namespace BCLoadtester.Loadtest;

using System.Text.Json;
using System.Collections.Concurrent;

public class WebOrderPayloadPool
{
    private readonly ConcurrentQueue<string> _payloads = new();
    private readonly WebOrderProfile _profile;

    private readonly int _minLines;
    private readonly int _maxLines;

    // 🔥 NEU
    private readonly decimal _shippingChargeAmount;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public int Count => _payloads.Count;
    public int InitialSize { get; }

    public WebOrderPayloadPool(
        string companyName,
        WebOrderProfile profile,
        int minLines,
        int maxLines,
        int size,
        decimal shippingChargeAmount = 0 // 🔥 NEU
    )
    {
        _profile = profile;
        _minLines = minLines;
        _maxLines = maxLines;
        _shippingChargeAmount = shippingChargeAmount;

        InitialSize = size;

        if (profile.Customers.Count == 0)
            throw new Exception($"{companyName}: RandomProfile contains no customers");

        if (profile.Items.Count == 0)
            throw new Exception($"{companyName}: RandomProfile contains no items");

        // 🔥 Initial Fill
        for (int i = 0; i < size; i++)
        {
            _payloads.Enqueue(CreatePayload(_profile, _minLines, _maxLines, _shippingChargeAmount));
        }
    }

    public bool TryGet(out string payload)
    {
        return _payloads.TryDequeue(out payload);
    }

    public string GetRandom()
    {
        return CreatePayload(_profile, _minLines, _maxLines, _shippingChargeAmount);
    }

    public string CreateBigOrder(int lines)
    {
        if (lines <= 0)
            return CreatePayload(_profile, _minLines, _maxLines, _shippingChargeAmount);

        var rnd = Random.Shared;

        int min = (int)(lines * 0.9);
        if (min < 1)
            min = 1;

        int randomized = rnd.Next(min, lines + 1);

        return CreatePayload(_profile, randomized, randomized, _shippingChargeAmount);
    }

    public void RefillIfLow(int threshold = 1000, int refillAmount = 5000)
    {
        if (_payloads.Count > threshold)
            return;

        for (int i = 0; i < refillAmount; i++)
        {
            _payloads.Enqueue(CreatePayload(_profile, _minLines, _maxLines, _shippingChargeAmount));
        }
    }

    // =========================
    // 🔧 CORE PAYLOAD BUILDER
    // =========================
    private static string CreatePayload(
        WebOrderProfile profile,
        int minLines,
        int maxLines,
        decimal shippingChargeAmount // 🔥 NEU
    )
    {
        var rnd = Random.Shared;

        var customer = profile.Customers[rnd.Next(profile.Customers.Count)];
        int lines = rnd.Next(minLines, maxLines + 1);

        var quicksaleslines = new List<object>(lines);

        decimal totalAmount = 0; // 🔥 NEU

        for (int i = 0; i < lines; i++)
        {
            var item = profile.Items[rnd.Next(profile.Items.Count)];

            decimal lineAmount = item.UnitPrice; // quantity = 1
            totalAmount += lineAmount;

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
        // 🔥 SHIPPING DAZURECHNEN
        if (shippingChargeAmount > 0)
        {
            totalAmount += shippingChargeAmount;
        }

        // 🔥 SHIPPING CHARGES ARRAY (BC-kompatibel)
        var shippingCharges = shippingChargeAmount > 0
            ? new[]
            {
                new
                {
                    shippingChargeType = "SC",
                    shippingChargeAmount = shippingChargeAmount
                }
            }
            : Array.Empty<object>();

        var payload = new
        {
            externalReferenceNo = "",
            externalDocumentNo = "",
            basketId = "",
            shopOrderNumber = "",
            orderDateTime = DateTime.UtcNow,

            // 🔥 NEU
            amount = totalAmount,
            shippingChargeAmount = shippingChargeAmount,

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

            quicksaleslines = quicksaleslines,

            // 🔥 NEU
            quicksalesshippingcharges = shippingCharges
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }
}