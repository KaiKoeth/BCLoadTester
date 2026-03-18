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
    
    public List<WorkerConfig> workers { get; set; }
    public List<Company> companies { get; set; } = new();
}
    