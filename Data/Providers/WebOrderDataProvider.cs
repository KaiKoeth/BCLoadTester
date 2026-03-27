using Microsoft.Data.SqlClient;

public class WebOrderProfile
{
    public List<OrderCustomerEntry> Customers { get; set; } = new();
    public List<OrderItemEntry> Items { get; set; } = new();
}

public class WebOrderDataProvider
{
    private readonly string _connectionString;

    public WebOrderDataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WebOrderProfile> LoadProfile(
        string company,
        int limitCustomers,
        int limitItems)
    {
        var profile = new WebOrderProfile();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var randomTable = "[Random Profile No_ Laodtest]";
        var customerTable = $"[{company}$Customer]";
        var itemTable = $"[{company}$Item]";
        var salutationTable = $"[{company}$Salutation]";

        // =========================
        // 🔹 CUSTOMERS
        // =========================
        var customerCmd = new SqlCommand($@"
            SELECT TOP (@limitCustomers)
                c.[No_],
                c.[Name],
                c.[First Name GLX],
                c.[Surname GLX],
                c.[Street MAC],
                c.[House No_ MAC],
                c.[Address],
                c.[City],
                c.[Post Code],
                c.[E-Mail],
                s.[Salutation Code Webshop GLX], 
                c.[Country_Region Code],
                c.[Payment Method Code]
            FROM {randomTable} r WITH (NOLOCK)
            INNER JOIN {customerTable} c WITH (NOLOCK)
                ON r.[No] = c.[No_]
            JOIN {salutationTable} s WITH (NOLOCK)
                ON c.[Salutation Code MAC] = s.[Code]
            WHERE r.[Companyname] = @company
              AND r.[Type] = 1
            ORDER BY NEWID()
        ", conn);

        customerCmd.Parameters.AddWithValue("@limitCustomers", limitCustomers);
        customerCmd.Parameters.AddWithValue("@company", company);

        using (var reader = await customerCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                profile.Customers.Add(new OrderCustomerEntry
                {
                    No = reader.GetString(0),
                    Name = reader.GetString(1),
                    FirstName = reader.GetString(2),
                    Surname = reader.GetString(3),
                    Street = reader.GetString(4),
                    HouseNo = reader.GetString(5),
                    Address = reader.GetString(6),
                    City = reader.GetString(7),
                    PostCode = reader.GetString(8),
                    Email = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    salutation = reader.GetInt32(10).ToString(),
                    countryregion = reader.GetString(11),
                    paymentmethod = reader.GetString(12),
                });
            }
        }

        // =========================
        // 🔹 ITEMS
        // =========================
        var itemCmd = new SqlCommand($@"
            SELECT TOP (@limitItems)
                i.[No_],
                i.[Description],
                i.[Unit Price]
            FROM {randomTable} r WITH (NOLOCK)
            INNER JOIN {itemTable} i WITH (NOLOCK)
                ON r.[No] = i.[No_]
            WHERE r.[Companyname] = @company
              AND r.[Type] = 3
            ORDER BY NEWID()
        ", conn);

        itemCmd.Parameters.AddWithValue("@limitItems", limitItems);
        itemCmd.Parameters.AddWithValue("@company", company);

        using (var reader = await itemCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                profile.Items.Add(new OrderItemEntry
                {
                    No = reader.GetString(0),
                    Description = reader.GetString(1),
                    UnitPrice = reader.GetDecimal(2)
                });
            }
        }

        return profile;
    }
}