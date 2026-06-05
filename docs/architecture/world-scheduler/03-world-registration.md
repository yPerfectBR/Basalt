# 03 — World Registration

Each world registered in the server carries **scheduling metadata** that controls which workers may host it and how the scheduler balances load.

## WorldRegistration

```csharp
namespace Basalt.Server.Scheduling;

public sealed class WorldRegistration
{
    /// <summary>Same as World.Name / server world identifier.</summary>
    public required string Identifier { get; init; }

    /// <summary>Expected simulation cost category.</summary>
    public WorldProfile Profile { get; init; } = WorldProfile.Light;

    /// <summary>
    /// Worker indices (0 .. world-thread-count - 1) that may host this world.
    /// PickWorker chooses the freest among these when attaching.
    /// </summary>
    public required int[] AllowedWorkers { get; init; }

    /// <summary>
    /// If set and present in AllowedWorkers, always use this worker (skip load balancing).
    /// </summary>
    public int? PreferredWorker { get; init; }

    /// <summary>Optional soft cap for concurrent players in this world instance.</summary>
    public int MaxConcurrentPlayers { get; init; } = int.MaxValue;
}
```

## WorldProfile

```csharp
public enum WorldProfile
{
    /// <summary>Spawn hub, lobby — usually one world, moderate cost.</summary>
    Hub,

    /// <summary>Skyblock islands, simple survival — low cost per world, many instances.</summary>
    Light,

    /// <summary>Dungeons, complex minigames — high cost per world, few instances.</summary>
    Heavy
}
```

### Default profile → allowed workers

Applied when `AllowedWorkers` is omitted in config. Server must validate indices against `world-thread-count`.

| Profile | Default allowedWorkers | Typical use |
|---------|------------------------|-------------|
| `Hub` | `[0]` | Spawn, lobby |
| `Light` | `[1, 2]` | Skyblock islands |
| `Heavy` | `[2, 3]` | Dungeons, boss arenas |

Defaults are **configurable** in `server.properties` (see [10-config-reference.md](./10-config-reference.md)).

---

## Registration sources

Worlds can receive registration from (in priority order):

1. **Explicit API** — `Server.CreateWorld(name, provider, registration)`
2. **Per-world JSON** — `worlds/{identifier}/world.json` or `worlds/{identifier}.json`
3. **Profile shorthand** — `CreateWorld(name, provider, profile: WorldProfile.Light)`
4. **Server defaults** — `default-world-profile=hub`, default allowed worker sets

### Per-world JSON example

File: `worlds/island_042/world.json`

```json
{
  "identifier": "island_042",
  "profile": "light",
  "allowedWorkers": [1, 2],
  "preferredWorker": null,
  "maxConcurrentPlayers": 4
}
```

File: `worlds/dungeon_event_alpha/world.json`

```json
{
  "identifier": "dungeon_event_alpha",
  "profile": "heavy",
  "allowedWorkers": [2, 3],
  "preferredWorker": 3,
  "maxConcurrentPlayers": 8
}
```

---

## Integration with World

Add to `Basalt/World/World.cs`:

```csharp
public WorldRegistration Registration { get; internal set; }

/// <summary>Null when world is dormant (no players).</summary>
public int? AttachedWorkerId { get; internal set; }

public bool IsAttached => AttachedWorkerId.HasValue;

public int PresentPlayerCount { get; internal set; }
```

Add to `Server.CreateWorld` / `LoadWorld`:

```csharp
public WorldInstance CreateWorld(
    string name,
    string providerIdentifier,
    WorldRegistration? registration = null,
    params object[] providerArgs)
{
    registration ??= WorldRegistrationDefaults.ForProfile(WorldProfile.Light, name);
    // validate AllowedWorkers against Properties.WorldThreadCount
    // ...
}
```

---

## Validation rules

On world create/load, the server MUST:

1. Reject empty `AllowedWorkers`.
2. Reject any index `< 0` or `>= world-thread-count`.
3. Reject `PreferredWorker` not in `AllowedWorkers`.
4. Log warning if `Heavy` world shares only `Light` workers (misconfiguration).

```csharp
public static void Validate(WorldRegistration reg, int workerCount)
{
    if (reg.AllowedWorkers.Length == 0)
        throw new ArgumentException("AllowedWorkers must not be empty.");

    foreach (int id in reg.AllowedWorkers)
    {
        if (id < 0 || id >= workerCount)
            throw new ArgumentOutOfRangeException(nameof(reg.AllowedWorkers),
                $"Worker {id} is out of range for pool size {workerCount}.");
    }

    if (reg.PreferredWorker is int preferred && !reg.AllowedWorkers.Contains(preferred))
        throw new ArgumentException("PreferredWorker must be in AllowedWorkers.");
}
```

---

## Skyblock pattern

Many island worlds, same profile:

```csharp
for (int i = 0; i < 500; i++)
{
    server.CreateWorld($"island_{i:D3}", "leveldb",
        new WorldRegistration
        {
            Identifier = $"island_{i:D3}",
            Profile = WorldProfile.Light,
            AllowedWorkers = [1, 2]
        },
        Path.Combine("worlds", $"island_{i:D3}"));
}
```

- All 500 worlds exist in `Server._worlds` metadata.
- Only islands with a player online are **attached** to worker 1 or 2.
- Scheduler picks the freest among `[1, 2]` per attach.

---

## Dungeon pattern

Few instances, dedicated heavy workers:

```csharp
server.CreateWorld("dungeon_01", "memory",
    new WorldRegistration
    {
        Identifier = "dungeon_01",
        Profile = WorldProfile.Heavy,
        AllowedWorkers = [2, 3],
        MaxConcurrentPlayers = 8
    });
```

When 4 dungeon instances are active on worker 3, new attaches to worker 2 if it has lower load (still within `[2, 3]`).

---

## Dynamic world creation (plugins)

Plugins creating worlds at runtime must supply registration:

```csharp
server.CreateWorld("arena_temp", "memory", new WorldRegistration
{
    Identifier = "arena_temp",
    Profile = WorldProfile.Heavy,
    AllowedWorkers = [3]
});
```

On plugin disable, call `UnloadWorld` only after all players leave (or force teleport out).

---

## World vs dimension

Basalt structure: `Server` → `World` → `Dimension`(s).

Registration is **per World**, not per Dimension. All dimensions in a world tick on the **same worker** as their parent world. Cross-dimension teleport within one world stays on one worker (existing `Player.Teleport` path).

---

## Related documents

- PickWorker algorithm: [04-world-scheduler.md](./04-world-scheduler.md)
- Config keys: [10-config-reference.md](./10-config-reference.md)
- Attach/detach triggers: [08-phased-implementation.md](./08-phased-implementation.md) Phase 3
