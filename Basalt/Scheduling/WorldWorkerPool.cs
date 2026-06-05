namespace Basalt.Server.Scheduling;

/// <summary>
/// Pool of world simulation worker threads.
/// </summary>
public sealed class WorldWorkerPool : IDisposable
{
    private readonly WorldWorker[] _workers;

    public WorldWorkerPool(int workerCount, Server server)
    {
        if (workerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be at least 1.");
        }

        _workers = new WorldWorker[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = new WorldWorker(i, server);
        }
    }

    public int WorkerCount => _workers.Length;

    public WorldWorker GetWorker(int id)
    {
        if (id < 0 || id >= _workers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return _workers[id];
    }

    public IReadOnlyList<WorkerLoadMetrics> GetAllMetrics()
    {
        WorkerLoadMetrics[] metrics = new WorkerLoadMetrics[_workers.Length];
        for (int i = 0; i < _workers.Length; i++)
        {
            metrics[i] = _workers[i].Metrics;
        }

        return metrics;
    }

    public void Start()
    {
        for (int i = 0; i < _workers.Length; i++)
        {
            _workers[i].Start();
        }
    }

    public void Stop()
    {
        for (int i = 0; i < _workers.Length; i++)
        {
            _workers[i].Stop();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
