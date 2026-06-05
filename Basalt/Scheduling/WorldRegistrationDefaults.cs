namespace Basalt.Server.Scheduling;

/// <summary>
/// Default <see cref="WorldRegistration"/> values from server.properties when no per-world file exists.
/// </summary>
public static class WorldRegistrationDefaults
{
    /// <summary>
    /// Creates a registration using <see cref="Properties.DefaultAllowedWorkers"/>,
    /// or all workers in the pool when unset.
    /// </summary>
    public static WorldRegistration ForWorld(string identifier, Properties properties)
    {
        int[] allowed = ParseWorkers(properties.DefaultAllowedWorkers, properties.WorldThreadCount);
        allowed = ClampWorkers(allowed, properties.WorldThreadCount);

        return new WorldRegistration
        {
            Identifier = identifier,
            AllowedWorkers = allowed
        };
    }

    public static int[] ParseWorkers(string? value, int workerCount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [.. Enumerable.Range(0, workerCount)];
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

        return workers.Count == 0 ? [.. Enumerable.Range(0, workerCount)] : [.. workers];
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
