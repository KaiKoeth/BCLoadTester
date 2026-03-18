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
        var CustTable = $"[{companyName}$Customer]";
        var ContTable = $"[{companyName}$Contact]";

        var sql = $@"
            SELECT
                C.Name,
                C.Address,
                C.[Post Code],
                C.City,
                C.[$systemId],
                C.[E-Mail],
                CT.[First Name],
                CT.Surname
            FROM {CustTable} C WITH (NOLOCK)
            JOIN {ContTable} CT WITH (NOLOCK) ON C.No_ = CT.No_
            WHERE C.[E-Mail] <> '' AND CT.Surname <> '' and CT.[First Name] <> ''";

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
                Email = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Firstname = reader.GetString(6),
                Surname = reader.GetString(7)
            });
        }

        return result;
    }
}