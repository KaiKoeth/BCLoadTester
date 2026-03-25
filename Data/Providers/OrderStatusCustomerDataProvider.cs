using Microsoft.Data.SqlClient;

public class OrderStatusCustomerDataProvider
{
    private readonly string _connectionString;

    public OrderStatusCustomerDataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<string>> LoadCustomers(string company, int limit)
    {
        var result = new List<string>();

        var tableCustomer = $"[{company}$Customer]";
        var tableSales = $"[{company}$Sales Header]";
        var tableQuick = $"[{company}$Quick Sales Header MAC]";

        var query = $@"
            SELECT TOP (@limit) [No_]
            FROM (
                SELECT DISTINCT c.[No_]
                FROM {tableCustomer} c WITH (NOLOCK)
                WHERE 
                    EXISTS (
                        SELECT 1 
                        FROM {tableSales} s WITH (NOLOCK)
                        WHERE s.[Document Type] = 1
                        AND s.[Sell-to Customer No_] = c.[No_]
                    )
                    OR EXISTS (
                        SELECT 1 
                        FROM {tableQuick} q WITH (NOLOCK)
                        WHERE q.[Document Type] = 1
                        AND q.[Sell-to Customer No_] = c.[No_]
                    )
            ) t
            ORDER BY NEWID()
        ";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}