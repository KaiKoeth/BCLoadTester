namespace BCLoadtester.Loadtest;

using System.Diagnostics;
using System.Net.Http;

public abstract class BaseWorker : IWorker
{
    protected readonly HttpClient _client;
    protected readonly Statistics _stats;
    protected readonly string _workerName;
    protected readonly string _company;

    private readonly int _delayMs;
    private readonly int _rpm;

    protected BaseWorker(
        HttpClient client,
        Statistics stats,
        string workerName,
        string company,
        int rpm)
    {
        _client = client;
        _stats = stats;
        _workerName = workerName;
        _company = company;

        _rpm = Math.Max(1, rpm);
        _delayMs = Math.Max(1, 60000 / _rpm);
    }

    public async Task Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await ExecuteAsync(token);

                // ✅ Request zählen inkl. RPM
                _stats.RequestSent(_workerName, _company, _rpm);
            }
            catch (Exception ex)
            {
                // 🔥 sauberes Error Handling
                var message = BuildErrorMessage(ex);

                _stats.Error(_workerName, _company, message);
            }

            sw.Stop();

            // ✅ Response Time tracken
            _stats.AddResponseTime(_workerName, _company, sw.ElapsedMilliseconds);

            // ✅ Ziel-RPM einhalten
            var delay = _delayMs - (int)sw.ElapsedMilliseconds;
            if (delay > 0)
            {
                try
                {
                    await Task.Delay(delay, token);
                }
                catch
                {
                    // ignore cancellation
                }
            }
        }
    }

    protected abstract Task ExecuteAsync(CancellationToken token);

    // =========================================
    // 🔥 Zentrale Fehler-Aufbereitung
    // =========================================
    private string BuildErrorMessage(Exception ex)
    {
        // HTTP Fehler schöner darstellen
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode != null)
            {
                return $"HTTP {(int)httpEx.StatusCode} - {httpEx.StatusCode}";
            }

            return $"HTTP ERROR - {httpEx.Message}";
        }

        // Standard
        return ex.Message;
    }
}