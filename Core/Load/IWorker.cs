namespace BCLoadtester.Loadtest;

public interface IWorker
{
    Task Run(CancellationToken token);
}