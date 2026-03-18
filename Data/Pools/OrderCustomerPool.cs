public class OrderCustomerPool
{
    private readonly List<CustomerEntry> _customers = new();
    private readonly object _lock = new();

    public void Initialize(List<CustomerEntry> initial)
    {
        lock (_lock)
        {
            _customers.Clear();
            _customers.AddRange(initial);
        }
    }

    public void Add(CustomerEntry customer)
    {
        lock (_lock)
        {
            _customers.Add(customer);

            // optional Limit (wichtig für lange Tests)
            if (_customers.Count > 10000)
                _customers.RemoveAt(0);
        }
    }

    public CustomerEntry? GetRandom()
    {
        lock (_lock)
        {
            if (_customers.Count == 0)
                return null;

            return _customers[Random.Shared.Next(_customers.Count)];
        }
    }
}