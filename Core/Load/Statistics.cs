using System.Collections.Concurrent;
using System.Diagnostics;

public class Statistics
{
    private class WorkerStats
    {
        public long Requests;
        public long Errors;
        public long Rpm;

        public Stopwatch Timer = Stopwatch.StartNew();

        public ConcurrentQueue<DateTime> RequestTimes = new();
        public ConcurrentQueue<long> ResponseTimes = new();

        // 🔥 THREAD SAFE
        public ConcurrentDictionary<string, long> ErrorMessages = new();

        public int PoolSize;

        public double AvgMs;
        public long MaxMs;
        public long TotalMs;
        public long Count;
    }

    private readonly ConcurrentDictionary<string, WorkerStats> _stats = new();

    private string BuildKey(string worker, string company)
        => $"{worker}|{company}";

    // ✅ EINHEITLICH (nur diese Methode behalten!)
    public void RequestSent(string worker, string company, long rpm)
    {
        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        // RPM nur einmal setzen
        if (stat.Rpm == 0)
            stat.Rpm = rpm;

        Interlocked.Increment(ref stat.Requests);

        stat.RequestTimes.Enqueue(DateTime.UtcNow);
    }

    public void Error(string worker, string company, string errorMessage)
    {
        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        Interlocked.Increment(ref stat.Errors);

        // 🔥 THREAD SAFE UPDATE
        stat.ErrorMessages.AddOrUpdate(
            errorMessage,
            1,
            (_, count) => count + 1);
    }

    public Dictionary<string, long> GetErrors(string worker, string company)
    {
        var key = BuildKey(worker, company);

        if (_stats.TryGetValue(key, out var stat))
        {
            return stat.ErrorMessages
                .OrderByDescending(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        return new Dictionary<string, long>();
    }

    public IEnumerable<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> GetStats()
    {
        foreach (var s in _stats)
        {
            var parts = s.Key.Split('|');

            var worker = parts[0];
            var company = parts[1];

            double rps = GetCurrentRps(s.Value);

            yield return (
                worker,
                company,
                s.Value.Rpm,
                s.Value.Requests,
                s.Value.Errors,
                rps,
                s.Value.PoolSize,
                s.Value.AvgMs,
                s.Value.MaxMs
            );
        }
    }

    private double GetCurrentRps(WorkerStats stat)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-1);

        while (stat.RequestTimes.TryPeek(out var t) && t < cutoff)
            stat.RequestTimes.TryDequeue(out _);

        return stat.RequestTimes.Count;
    }

    public void SetPoolSize(string worker, string company, int size)
    {
        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        stat.PoolSize = size;
    }

    public void AddResponseTime(string worker, string company, long ms)
    {
        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        lock (stat)
        {
            stat.ResponseTimes.Enqueue(ms);

            stat.TotalMs += ms;
            stat.Count++;

            stat.AvgMs = stat.TotalMs / (double)stat.Count;

            if (ms > stat.MaxMs)
                stat.MaxMs = ms;

            // optionales Sliding Window
            while (stat.ResponseTimes.Count > 100)
                stat.ResponseTimes.TryDequeue(out _);
        }
    }
}