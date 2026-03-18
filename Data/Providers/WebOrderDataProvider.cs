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

    public async Task<WebOrderProfile> LoadProfile(string company)
    {
        var profile = new WebOrderProfile();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var randomTable = $"[{company}$Random Profile Line MAC]";
        var customerTable = $"[{company}$Customer]";
        var itemTable = $"[{company}$Item]";
         var SalutationTable = $"[{company}$Salutation]";


        // -------------------------
        // Customers laden
        // -------------------------

        var customerCmd = new SqlCommand($@"
            SELECT
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
            FROM {randomTable} r WITH (READUNCOMMITTED)
            INNER JOIN {customerTable} c WITH (READUNCOMMITTED)
                ON r.[No_] = c.[No_]

            -- 🔥 NEUER JOIN
            JOIN {SalutationTable} s WITH (READUNCOMMITTED)
                ON c.[Salutation Code MAC] = s.[Code]

            WHERE r.[Random Profile No_] = 'DEFAULT'
            AND r.[Type] = 1
        ", conn);

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
                    Email = reader.GetString(9),
                    salutation = reader.GetInt32(10).ToString(),
                    countryregion = reader.GetString(11),
                    paymentmethod = reader.GetString(12),
                });
            }
        }

        // -------------------------
        // Items laden
        // -------------------------

        var itemCmd = new SqlCommand($@"
            SELECT
                i.[No_],
                i.[Description],
                i.[Unit Price]
            FROM {randomTable} r WITH (READUNCOMMITTED)
            INNER JOIN {itemTable} i WITH (READUNCOMMITTED)
                ON r.[No_] = i.[No_]
            WHERE r.[Random Profile No_] = 'DEFAULT'
              AND r.[Type] = 3
        ", conn);

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