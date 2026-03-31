namespace BCLoadtester.Loadtest;

public class LoadController
{
    private readonly List<Task> _tasks = new();
    private CancellationTokenSource? _cts;

    public void Start(IEnumerable<IWorker> workers)
    {
        _cts = new CancellationTokenSource();

        foreach (var worker in workers)
        {
            var task = Task.Run(async () =>
            {
                // 🔥 NEU: Start-Offset (0–2 Sekunden)
                await Task.Delay(Random.Shared.Next(0, 2000), _cts.Token);

                await worker.Run(_cts.Token);
            });

            _tasks.Add(task);
        }
    }
    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }
    }
}