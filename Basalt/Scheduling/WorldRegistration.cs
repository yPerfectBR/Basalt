namespace Basalt.Server.Scheduling;

/// <summary>
/// Scheduling metadata for a registered world (profile, allowed workers, limits).
/// </summary>
public sealed class WorldRegistration
{
    /// <summary>Same as <see cref="Basalt.Server.World.World.Name"/> / server world identifier.</summary>
    public required string Identifier { get; init; }

    /// <summary>Expected simulation cost category.</summary>
    public WorldProfile Profile { get; init; } = WorldProfile.Light;

    /// <summary>
    /// Worker indices (0 .. workerCount - 1) that may host this world.
    /// PickWorker chooses the freest among these when attaching.
    /// </summary>
    public required int[] AllowedWorkers { get; init; }

    /// <summary>
    /// If set and present in <see cref="AllowedWorkers"/>, always use this worker (skip load balancing).
    /// </summary>
    public int? PreferredWorker { get; init; }

    /// <summary>Optional soft cap for concurrent players in this world instance.</summary>
    public int MaxConcurrentPlayers { get; init; } = int.MaxValue;

    /// <summary>
    /// Validates registration against the configured worker pool size.
    /// </summary>
    public static void Validate(WorldRegistration registration, int workerCount)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (workerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be at least 1.");
        }

        if (registration.AllowedWorkers.Length == 0)
        {
            throw new ArgumentException("AllowedWorkers must not be empty.", nameof(registration));
        }

        foreach (int workerId in registration.AllowedWorkers)
        {
            if (workerId < 0 || workerId >= workerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(registration),
                    $"Worker {workerId} is out of range for pool size {workerCount}.");
            }
        }

        if (registration.PreferredWorker is int preferred && !registration.AllowedWorkers.Contains(preferred))
        {
            throw new ArgumentException("PreferredWorker must be in AllowedWorkers.", nameof(registration));
        }
    }
}
