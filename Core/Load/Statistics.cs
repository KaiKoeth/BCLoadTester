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

        public ConcurrentDictionary<string, long> ErrorMessages = new();

        public int PoolSize;

        public double AvgMs;
        public long MaxMs;
        public long TotalMs;
        public long Count;
    }

    private readonly ConcurrentDictionary<string, WorkerStats> _stats = new();

    // 🔥 NEU: thread-safe custom metrics
    private readonly ConcurrentDictionary<(string Worker, string Company, string Key), long> _customMetrics = new();

    private string BuildKey(string worker, string company)
        => $"{worker}|{company}";

    private string NormalizeWorker(string worker)
        => worker?.Trim() ?? "";

    // =========================
    // ✅ REQUEST
    // =========================
    public void RequestSent(string worker, string company, long rpm)
    {
        worker = NormalizeWorker(worker);

        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        if (stat.Rpm == 0)
            Interlocked.CompareExchange(ref stat.Rpm, rpm, 0);

        Interlocked.Increment(ref stat.Requests);

        stat.RequestTimes.Enqueue(DateTime.UtcNow);
    }

    // =========================
    // ❌ ERROR
    // =========================
    public void Error(string worker, string company, string errorMessage)
    {
        worker = NormalizeWorker(worker);

        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        Interlocked.Increment(ref stat.Errors);

        if (string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = "Unknown";

        stat.ErrorMessages.AddOrUpdate(
            errorMessage,
            1,
            (_, count) => count + 1);
    }

    // =========================
    // 🔍 ERROR DETAILS
    // =========================
    public Dictionary<string, long> GetErrors(string worker, string company)
    {
        worker = NormalizeWorker(worker);

        var key = BuildKey(worker, company);

        if (_stats.TryGetValue(key, out var stat))
        {
            return stat.ErrorMessages
                .OrderByDescending(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        return new Dictionary<string, long>();
    }

    // =========================
    // 📊 STATS
    // =========================
    public IEnumerable<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> GetStats()
    {
        foreach (var kvp in _stats)
        {
            var key = kvp.Key;
            var stat = kvp.Value;

            var separatorIndex = key.IndexOf('|');
            if (separatorIndex <= 0)
                continue;

            var worker = key.Substring(0, separatorIndex);
            var company = key.Substring(separatorIndex + 1);

            double rps = GetCurrentRps(stat);

            yield return (
                worker,
                company,
                stat.Rpm,
                stat.Requests,
                stat.Errors,
                rps,
                stat.PoolSize,
                stat.AvgMs,
                stat.MaxMs
            );
        }
    }

    // =========================
    // ⚡ RPS (Realtime)
    // =========================
    private double GetCurrentRps(WorkerStats stat)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-1);

        while (stat.RequestTimes.TryPeek(out var t) && t < cutoff)
            stat.RequestTimes.TryDequeue(out _);

        return stat.RequestTimes.Count;
    }

    // =========================
    // 📦 POOL SIZE
    // =========================
    public void SetPoolSize(string worker, string company, int size)
    {
        worker = NormalizeWorker(worker);

        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        stat.PoolSize = size;
    }

    // =========================
    // ⏱ RESPONSE TIMES
    // =========================
    public void AddResponseTime(string worker, string company, long ms)
    {
        worker = NormalizeWorker(worker);

        var key = BuildKey(worker, company);
        var stat = _stats.GetOrAdd(key, _ => new WorkerStats());

        lock (stat) // bewusst beibehalten (Aggregationen!)
        {
            stat.ResponseTimes.Enqueue(ms);

            stat.TotalMs += ms;
            stat.Count++;

            stat.AvgMs = stat.TotalMs / (double)stat.Count;

            if (ms > stat.MaxMs)
                stat.MaxMs = ms;

            while (stat.ResponseTimes.Count > 100)
                stat.ResponseTimes.TryDequeue(out _);
        }
    }

    // =========================
    // 📈 CUSTOM METRICS (🔥 FIXED)
    // =========================
    public void IncrementCustomMetric(string worker, string company, string key)
    {
        worker = NormalizeWorker(worker);

        var k = (worker, company, key);

        _customMetrics.AddOrUpdate(
            k,
            1,
            (_, current) => current + 1
        );
    }

    public long GetCustomMetric(string worker, string company, string key)
    {
        worker = NormalizeWorker(worker);

        var k = (worker, company, key);

        return _customMetrics.TryGetValue(k, out var value) ? value : 0;
    }
}