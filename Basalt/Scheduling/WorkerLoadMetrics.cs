namespace Basalt.Server.Scheduling;

/// <summary>
/// Load snapshot for a world worker thread (used by PickWorker in Phase 3+).
/// </summary>
public sealed class WorkerLoadMetrics
{
    /// <summary>Worker index (0 .. pool size - 1).</summary>
    public int WorkerId { get; init; }

    /// <summary>Worlds currently attached and ticking on this worker.</summary>
    public int ActiveWorldCount { get; set; }

    /// <summary>Total players present across active worlds on this worker.</summary>
    public int TotalPresentPlayers { get; set; }

    /// <summary>Milliseconds spent in the last tick iteration.</summary>
    public double LastTickWorkMs { get; set; }

    /// <summary>Tick lag behind target 50 ms budget.</summary>
    public double TickLagMs { get; set; }

    /// <summary>Average ticks per second on this worker.</summary>
    public double Tps { get; set; }
}
