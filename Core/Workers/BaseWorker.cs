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
                var response = await ExecuteAsync(token);

                // ✅ Request zählen
                _stats.RequestSent(_workerName, _company, _rpm);

                // 🔥 HTTP Fehler erkennen (DEIN FIX)
                if (response != null && !response.IsSuccessStatusCode)
                {
                    var msg = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                    _stats.Error(_workerName, _company, msg);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _stats.Error(_workerName, _company, ex.Message);
            }

            sw.Stop();

            _stats.AddResponseTime(_workerName, _company, sw.ElapsedMilliseconds);

            var delay = _delayMs - (int)sw.ElapsedMilliseconds;

            if (delay > 0)
            {
                try
                {
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    // 🔥 WICHTIG: MUSS implementiert werden!
    protected abstract Task<HttpResponseMessage> ExecuteAsync(CancellationToken token);
}