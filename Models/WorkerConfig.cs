public class WorkerConfig
{
    public string type { get; set; }
    public string endpoint { get; set; }
    public bool enabled { get; set; }
    public double bufferFactor { get; set; } = 1.5;
    public int? maxConcurrency { get; set; }

}