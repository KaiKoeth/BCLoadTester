namespace BCLoadtester;

public class DashboardRow
{
    public string Company { get; set; } = "";
    public string Worker { get; set; } = "";
    public string DisplayWorker { get; set; } = "";

    public bool IsGroup { get; set; }
    public bool IsExpanded { get; set; }

    public long RPM { get; set; }
    public long Requests { get; set; }
    public long Errors { get; set; }

    public double AvgMs { get; set; }
    public long MaxMs { get; set; }

    public double HistoryAvgMs { get; set; }
}