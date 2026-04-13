using System;
using System.Collections.Generic;
using System.Linq;

namespace BCLoadtester;

public class StatisticsService
{
    private readonly object _lock = new();
    private Statistics _stats = new Statistics();

    public void ResetStats()
    {
        lock (_lock)
        {
            _stats = new Statistics();
        }
    }

    public IReadOnlyList<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> GetSortedStats()
    {
        lock (_lock)
        {
            var list = _stats.GetStats().ToList();

            list.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Company, b.Company, StringComparison.Ordinal);
                if (cmp != 0)
                    return cmp;

                return string.Compare(a.Worker, b.Worker, StringComparison.Ordinal);
            });

            return list;
        }
    }

    // =========================
    // 🔹 WRITE METHODS (thread-safe)
    // =========================
    public void RequestSent(string worker, string company, long rpm)
    {
        lock (_lock)
            _stats.RequestSent(worker, company, rpm);
    }

    public void Error(string worker, string company, string errorMessage)
    {
        lock (_lock)
            _stats.Error(worker, company, errorMessage);
    }

    public void SetPoolSize(string worker, string company, int size)
    {
        lock (_lock)
            _stats.SetPoolSize(worker, company, size);
    }

    public void AddResponseTime(string worker, string company, long ms)
    {
        lock (_lock)
            _stats.AddResponseTime(worker, company, ms);
    }

    public void IncrementCustomMetric(string worker, string company, string key)
    {
        lock (_lock)
            _stats.IncrementCustomMetric(worker, company, key);
    }

    // =========================
    // 🔹 READ METHODS (thread-safe)
    // =========================
    public long GetCustomMetric(string worker, string company, string key)
    {
        lock (_lock)
            return _stats.GetCustomMetric(worker, company, key);
    }

    public IReadOnlyDictionary<string, long> GetErrors(string worker, string company)
    {
        lock (_lock)
        {
            // defensive copy → verhindert externe Mutation
            return new Dictionary<string, long>(_stats.GetErrors(worker, company));
        }
    }
}