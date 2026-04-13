namespace BCLoadtester.Loadtest;

using System.Diagnostics;
using System.Net.Http;

public abstract class BaseWorker : IWorker
{
    protected readonly HttpClient _client;
    protected readonly StatisticsService _stats; // ✅ geändert
    protected readonly string _workerName;
    protected readonly string _company;

    private readonly int _rpm;
    private readonly double _intervalMs;
    private readonly SemaphoreSlim _semaphore;
    private readonly Func<int> _getConcurrency;
    private const int MaxSemaphore = 500;
    private int _currentTarget = 1;

    protected BaseWorker(
        HttpClient client,
        StatisticsService stats, // ✅ geändert
        string workerName,
        string company,
        int rpm,
        Func<int> getConcurrency
    )
    {
        _client = client;
        _stats = stats;
        _workerName = workerName;
        _company = company;

        _rpm = Math.Max(1, rpm);
        _intervalMs = 60000.0 / _rpm;

        _getConcurrency = getConcurrency;

        _semaphore = new SemaphoreSlim(MaxSemaphore);
    }

    public async Task Run(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var loopStart = DateTime.UtcNow;

                int desired = Math.Min(MaxSemaphore, Math.Max(1, _getConcurrency()));

                // 🔥 sanfte Anpassung
                if (_currentTarget < desired)
                    _currentTarget++;
                else if (_currentTarget > desired)
                    _currentTarget--;

                int target = _currentTarget;

                int inFlight = MaxSemaphore - _semaphore.CurrentCount;

                if (inFlight < target)
                {
                    await _semaphore.WaitAsync(token);
                    _ = ExecuteInternalAsync(token);
                }
                else
                {
                    await Task.Delay(1, token);
                }

                var elapsed = (DateTime.UtcNow - loopStart).TotalMilliseconds;
                var delay = Math.Max(0, _intervalMs - elapsed);

                try
                {
                    await Task.Delay((int)delay, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // sauberer Exit
        }
    }

    private async Task ExecuteInternalAsync(CancellationToken token)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await ExecuteAsync(token);

            _stats.RequestSent(_workerName, _company, 1); // ✅ unverändert nutzbar

            if (response != null && !response.IsSuccessStatusCode)
            {
                var msg = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                _stats.Error(_workerName, _company, msg);
            }
        }
        catch (OperationCanceledException)
        {
            // normal beim Stop
        }
        catch (Exception ex)
        {
            _stats.Error(_workerName, _company, ex.Message);
        }
        finally
        {
            sw.Stop();

            _stats.AddResponseTime(
                _workerName,
                _company,
                sw.ElapsedMilliseconds
            );

            _semaphore.Release();
        }
    }

    protected abstract Task<HttpResponseMessage> ExecuteAsync(CancellationToken token);
}