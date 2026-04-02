public class AppConfig
{
    public string serviceRoot { get; set; } = "";
    public string apiRoot { get; set; } = "";

    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public string connectionString { get; set; } = "";
    public int rpmPerWorker { get; set; } = 500;
    public int maxWorkersPerType { get; set; } = 50;
    public int maxConnectionsPerServer { get; set; } = 1000;

    public int maxConcurrencyPerWorker { get; set; } = 20;
    public Dictionary<string, double> avgResponseTimesMs { get; set; } = new();

    public string sqlServer { get; set; }
    public int sqlPort { get; set; }
    public string database { get; set; }
    public string dbUser { get; set; }
    public string dbPassword { get; set; }

    public List<WorkerConfig> workers { get; set; }
    public List<Company> companies { get; set; } = new();

    public string loadTestTableName { get; set; } = "BC Loadtest Protocol";
    public int maxTotalConcurrency { get; set; } = 20;

    public string BuildConnectionString()
    {
        return $"Server={sqlServer},{sqlPort};Database={database};User Id={dbUser};Password={dbPassword};TrustServerCertificate=True;";
    }
}


