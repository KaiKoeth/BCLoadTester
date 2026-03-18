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
            var task = Task.Run(() => worker.Run(_cts.Token));
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