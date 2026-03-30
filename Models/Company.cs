public class Company
{
    public string name { get; set; }
    public string guid { get; set; }
    public bool enabled { get; set; }

    public Dictionary<string, int> rpm { get; set; }
    public WebOrderConfig webOrderConfig { get; set; }
    public string? serviceRoot { get; set; }
    public string? apiRoot { get; set; }
}

public class WebOrderConfig
{
    public int minLines { get; set; }
    public int maxLines { get; set; }
    // 🔥 NEU: Big Orders
    public int bigOrderLines { get; set; } = 0;
    public int bigOrderIntervalMinutes { get; set; } = 0;
    public string promotionMediumNo { get; set; } = "";
    public string promotionMediumTrgGrpNo { get; set; } = "";
    public decimal shippingChargeAmount { get; set; } = 0;
}