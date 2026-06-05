namespace Basalt.Server.Scheduling;

/// <summary>
/// Expected simulation cost category for a world instance.
/// </summary>
public enum WorldProfile
{
    /// <summary>Spawn hub, lobby — usually one world, moderate cost.</summary>
    Hub,

    /// <summary>Skyblock islands, simple survival — low cost per world, many instances.</summary>
    Light,

    /// <summary>Dungeons, complex minigames — high cost per world, few instances.</summary>
    Heavy
}
