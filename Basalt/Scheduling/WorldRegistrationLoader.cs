namespace Basalt.Server.Scheduling;

using System.Text.Json;

/// <summary>
/// Loads <see cref="WorldRegistration"/> from per-world JSON files.
/// </summary>
public static class WorldRegistrationLoader
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Resolves and loads registration for <paramref name="identifier"/>; returns null when no file exists.
    /// </summary>
    public static WorldRegistration? TryLoad(string identifier, string? worldDataPath)
    {
        string? filePath = ResolveRegistrationPath(identifier, worldDataPath);
        if (filePath is null)
        {
            return null;
        }

        return TryLoadFromFile(filePath, identifier);
    }

    /// <summary>
    /// Loads registration from an explicit file path and validates it against the worker pool size.
    /// </summary>
    public static WorldRegistration LoadFromFile(string filePath, string expectedIdentifier, int workerCount)
    {
        WorldRegistration? registration = TryLoadFromFile(filePath, expectedIdentifier)
            ?? throw new InvalidOperationException($"Failed to load world registration from '{filePath}'.");

        WorldRegistration.Validate(registration, workerCount);
        return registration;
    }

    static WorldRegistration? TryLoadFromFile(string filePath, string expectedIdentifier)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            WorldRegistrationDocument? document = JsonSerializer.Deserialize<WorldRegistrationDocument>(json, JsonOptions);
            if (document?.AllowedWorkers is not { Length: > 0 })
            {
                Logger.Warn("[WorldRegistration] missing or empty allowedWorkers in {0}", filePath);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(document.Identifier)
                && !document.Identifier.Equals(expectedIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn(
                    "[WorldRegistration] identifier mismatch in {0}: expected {1}, got {2}",
                    filePath,
                    expectedIdentifier,
                    document.Identifier);
            }

            return new WorldRegistration
            {
                Identifier = expectedIdentifier,
                AllowedWorkers = document.AllowedWorkers,
                PreferredWorker = document.PreferredWorker,
                MaxConcurrentPlayers = document.MaxConcurrentPlayers ?? int.MaxValue
            };
        }
        catch (Exception ex)
        {
            Logger.Error("[WorldRegistration] failed to parse {0}: {1}", filePath, ex.Message);
            return null;
        }
    }

    static string? ResolveRegistrationPath(string identifier, string? worldDataPath)
    {
        if (!string.IsNullOrWhiteSpace(worldDataPath))
        {
            string alongside = Path.Combine(worldDataPath, "world.json");
            if (File.Exists(alongside))
            {
                return alongside;
            }
        }

        string sidecar = Path.Combine("worlds", $"{identifier}.json");
        return File.Exists(sidecar) ? sidecar : null;
    }

    sealed class WorldRegistrationDocument
    {
        public string? Identifier { get; set; }
        public int[]? AllowedWorkers { get; set; }
        public int? PreferredWorker { get; set; }
        public int? MaxConcurrentPlayers { get; set; }
    }
}
