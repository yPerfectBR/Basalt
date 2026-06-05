namespace Basalt.Server.Scheduling;

/// <summary>
/// Default <see cref="WorldRegistration"/> values per profile from server.properties.
/// </summary>
public static class WorldRegistrationDefaults
{
    /// <summary>
    /// Creates a registration with profile-appropriate default allowed workers, clamped to <paramref name="workerCount"/>.
    /// </summary>
    public static WorldRegistration ForProfile(WorldProfile profile, string identifier, int workerCount = 4)
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

    public static WorldRegistration ForProfile(WorldProfile profile, string identifier, Properties properties)
    {
        int[] allowed = profile switch
        {
            WorldProfile.Hub => ParseWorkers(properties.HubWorkers),
            WorldProfile.Light => ParseWorkers(properties.LightWorkers),
            WorldProfile.Heavy => ParseWorkers(properties.HeavyWorkers),
            _ => [0]
        };

        allowed = ClampWorkers(allowed, properties.WorldThreadCount);

        return new WorldRegistration
        {
            Identifier = identifier,
            Profile = profile,
            AllowedWorkers = allowed
        };
    }

    public static int[] ParseWorkers(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [0];
        }

        string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<int> workers = [];
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int workerId))
            {
                workers.Add(workerId);
            }
        }

        return workers.Count == 0 ? [0] : [.. workers];
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
