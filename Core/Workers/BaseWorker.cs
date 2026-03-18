namespace BCLoadtester.Loadtest;

using System.Diagnostics;

public abstract class BaseWorker : IWorker
{
    protected readonly HttpClient _client;
    protected readonly Statistics _stats;
    protected readonly string _workerName;
    protected readonly string _company;

    private readonly int _delayMs;
    private readonly long _rpm; // 🔥 NEU

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

        _rpm = rpm; // 🔥 NEU

        _delayMs = Math.Max(1, 60000 / rpm);
    }

    public async Task Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await ExecuteAsync(token);

                // 🔥 FIX
                _stats.RequestSent(_workerName, _company, _rpm);
            }
            catch (Exception ex)
            {
                _stats.Error(_workerName, _company, ex.Message);
            }

            sw.Stop();
            _stats.AddResponseTime(_workerName, _company, sw.ElapsedMilliseconds);

            var delay = _delayMs - (int)sw.ElapsedMilliseconds;
            if (delay > 0)
                await Task.Delay(delay, token);
        }
    }

    protected abstract Task ExecuteAsync(CancellationToken token);
}