using System.Collections.Generic;
using BCLoadtester.Loadtest;

namespace BCLoadtester;

public class WorkerDataContext
{
    public WorkerDataContext(
        Dictionary<(string Company, string Worker), List<CustomerEntry>> customerPools,
        Dictionary<string, List<string>> invoiceCustomerNoCache,
        Dictionary<string, List<string>> creditMemoCustomerNoCache,
        Dictionary<string, OrderStatusPool> orderStatusCache,
        Dictionary<string, WebOrderPayloadPool> webOrderPoolCache)
    {
        CustomerPools = customerPools;
        InvoiceCustomerNoCache = invoiceCustomerNoCache;
        CreditMemoCustomerNoCache = creditMemoCustomerNoCache;
        OrderStatusCache = orderStatusCache;
        WebOrderPoolCache = webOrderPoolCache;
    }

    public Dictionary<(string Company, string Worker), List<CustomerEntry>> CustomerPools { get; }
    public Dictionary<string, List<string>> InvoiceCustomerNoCache { get; }
    public Dictionary<string, List<string>> CreditMemoCustomerNoCache { get; }
    public Dictionary<string, OrderStatusPool> OrderStatusCache { get; }
    public Dictionary<string, WebOrderPayloadPool> WebOrderPoolCache { get; }
}
