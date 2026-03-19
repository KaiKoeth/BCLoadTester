public class Company
{
    public string name { get; set; }
    public string guid { get; set; }
    public bool enabled{ get; set; }

    public Dictionary<string, int> rpm { get; set; }
    public WebOrderConfig webOrderConfig { get; set; }
}

public class WebOrderConfig
{
    public int minLines { get; set; }
    public int maxLines { get; set; }
    public int WeborderPoolSize { get; set; } = 2000;
}