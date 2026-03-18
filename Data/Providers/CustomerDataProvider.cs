using Microsoft.Data.SqlClient;

public class CustomerDataProvider
{
    private readonly string _connectionString;

    public CustomerDataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<CustomerEntry>> LoadCustomers(string companyName)
    {
        var result = new List<CustomerEntry>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Tabellenname dynamisch bauen
        var tableName = $"[{companyName}$Customer]";

        var sql = $@"
            SELECT
                Name,
                Address,
                [Post Code],
                City,
                [$systemId],
                [E-Mail]
            FROM {tableName} WITH (NOLOCK)
            WHERE [E-Mail] <> ''";

        using var command = new SqlCommand(sql, connection);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new CustomerEntry
            {
                Name = reader.GetString(0),
                Address = reader.GetString(1),
                PostalCode = reader.GetString(2),
                City = reader.GetString(3),
                SystemId = reader.GetGuid(reader.GetOrdinal("$systemId")),
                Email = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return result;
    }
}