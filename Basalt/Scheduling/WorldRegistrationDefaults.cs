namespace Basalt.Server.Scheduling;

/// <summary>
/// Default <see cref="WorldRegistration"/> values per profile until server.properties keys exist (Phase 3).
/// </summary>
public static class WorldRegistrationDefaults
{
    /// <summary>Temporary default pool size until <c>world-thread-count</c> is added to Properties.</summary>
    public const int DefaultWorkerCount = 4;

    /// <summary>
    /// Creates a registration with profile-appropriate default allowed workers, clamped to <paramref name="workerCount"/>.
    /// </summary>
    public static WorldRegistration ForProfile(WorldProfile profile, string identifier, int workerCount = DefaultWorkerCount)
    {
        int[] allowed = profile switch
        {
            WorldProfile.Hub => [0],
            WorldProfile.Light => [1, 2],
            WorldProfile.Heavy => [2, 3],
            _ => [0]
        };

        allowed = ClampWorkers(allowed, workerCount);

        return new WorldRegistration
        {
            Identifier = identifier,
            Profile = profile,
            AllowedWorkers = allowed
        };
    }

    static int[] ClampWorkers(int[] workers, int workerCount)
    {
        List<int> clamped = [];
        for (int i = 0; i < workers.Length; i++)
        {
            int id = workers[i];
            if (id >= 0 && id < workerCount && !clamped.Contains(id))
            {
                clamped.Add(id);
            }
        }

        if (clamped.Count == 0)
        {
            clamped.Add(0);
        }

        return [.. clamped];
    }
}
