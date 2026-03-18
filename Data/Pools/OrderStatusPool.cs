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

                if (_list.Count > 10000)
                {
                    var removed = _list[0];
                    _list.RemoveAt(0);
                    _set.Remove(removed);
                }
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

    // 🔥 NEU
    public string? TakeRandom()
    {
        lock (_lock)
        {
            if (_list.Count == 0)
                return null;

            var index = Random.Shared.Next(_list.Count);
            var value = _list[index];

            _list.RemoveAt(index);
            _set.Remove(value);

            return value;
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