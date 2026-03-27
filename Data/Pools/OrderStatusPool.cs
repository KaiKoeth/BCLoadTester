public class OrderStatusPool
{
    private readonly List<string> _list = new();
    private readonly HashSet<string> _set = new();
    private readonly object _lock = new();

    public void Add(string customerNo)
    {
        lock (_lock)
        {
            if (_set.Add(customerNo))
            {
                _list.Add(customerNo);
            }
        }
    }

    public void AddRange(IEnumerable<string> nos)
    {
        lock (_lock)
        {
            foreach (var no in nos)
            {
                if (_set.Add(no))
                    _list.Add(no);
            }
        }
    }

    public string? GetRandom()
    {
        lock (_lock)
        {
            if (_list.Count == 0)
                return null;

            return _list[Random.Shared.Next(_list.Count)];
        }
    }

    // 🔥 BEHALTEN, aber NICHT destruktiv
    public string? TakeRandom()
    {
        lock (_lock)
        {
            if (_list.Count == 0)
                return null;

            // 🔥 kein Remove mehr
            return _list[Random.Shared.Next(_list.Count)];
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
                return _list.Count;
        }
    }
}