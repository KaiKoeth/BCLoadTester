using Microsoft.Data.SqlClient;

public class CreditMemoCustomerDataProvider
{
    private readonly string _connectionString;

    public CreditMemoCustomerDataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<string>> LoadCustomers(string company)
    {
        var result = new List<string>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var table = $"[{company}$Sales Cr_Memo Header]";

        var sql = $@"
            SELECT DISTINCT [Sell-to Customer No_]
            FROM {table} WITH (NOLOCK)
            WHERE [Sell-to Customer No_] <> ''
        ";

        using var cmd = new SqlCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}