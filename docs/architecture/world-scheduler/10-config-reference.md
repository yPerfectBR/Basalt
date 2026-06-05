# 10 — Config Reference

All configuration keys and per-world registration formats for the World Scheduler.

---

## server.properties

Add to [`Basalt/Properties.cs`](../../../Basalt/Properties.cs) with `ServerProperties` attributes.

| Key | Type | Default | Phase | Description |
|-----|------|---------|-------|-------------|
| `world-thread-count` | int | `4` | 3 | Number of worker threads (indices `0` .. `count-1`) |
| `world-scheduler-enabled` | bool | `false` | 3 | Enable multi-worker pool; `false` uses Phase 1 single-thread queue |
| `world-scheduler-debug` | bool | `false` | 1 | Verbose scheduler/packet routing logs |

### Default allowed workers by profile

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `world-profile-hub-workers` | int[] | `0` | Comma-separated worker indices for `Hub` when not specified per world |
| `world-profile-light-workers` | int[] | `1,2` | Default for `Light` profile |
| `world-profile-heavy-workers` | int[] | `2,3` | Default for `Heavy` profile |

Parser example: `"1,2"` → `[1, 2]`.

### Example server.properties

```properties
server-port=19132
max-players=100

# World Scheduler (Phase 3+)
world-thread-count=6
world-scheduler-enabled=false
world-scheduler-debug=false

world-profile-hub-workers=0
world-profile-light-workers=1,2
world-profile-heavy-workers=2,3,4,5
```

---

## Worker index layout (recommended 6-thread setup)

| Index | Profile | Role |
|-------|---------|------|
| `0` | Hub | Spawn, lobby |
| `1`, `2` | Light | Skyblock islands (scheduler picks freest) |
| `3`, `4`, `5` | Heavy | Dungeons, arenas |

This is convention only — actual mapping is defined by per-world `allowedWorkers`.

---

## Per-world registration JSON

### Path resolution (implement in Phase 3)

Search order:

1. `{world-path}/world.json` (alongside LevelDB data)
2. `worlds/{identifier}.json`
3. API argument to `CreateWorld`
4. Profile defaults from server.properties

### Schema

```json
{
  "$schema": "optional",
  "identifier": "string (required)",
  "profile": "hub | light | heavy (required)",
  "allowedWorkers": [1, 2],
  "preferredWorker": null,
  "maxConcurrentPlayers": 2147483647
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `identifier` | yes | Must match world name |
| `profile` | yes | `hub`, `light`, or `heavy` (case-insensitive) |
| `allowedWorkers` | yes | Non-empty array of valid worker indices |
| `preferredWorker` | no | Must be in `allowedWorkers` |
| `maxConcurrentPlayers` | no | Soft reject or queue when full (future) |

### Examples

**Skyblock island**

```json
{
  "identifier": "island_042",
  "profile": "light",
  "allowedWorkers": [1, 2]
}
```

**Dungeon instance**

```json
{
  "identifier": "dungeon_boss_01",
  "profile": "heavy",
  "allowedWorkers": [3, 4, 5],
  "preferredWorker": 4,
  "maxConcurrentPlayers": 8
}
```

**Hub**

```json
{
  "identifier": "world",
  "profile": "hub",
  "allowedWorkers": [0]
}
```

---

## C# API

### CreateWorld with registration

```csharp
server.CreateWorld(
    name: "island_042",
    providerIdentifier: "leveldb",
    registration: new WorldRegistration
    {
        Identifier = "island_042",
        Profile = WorldProfile.Light,
        AllowedWorkers = [1, 2]
    },
    providerArgs: Path.Combine("worlds", "island_042"));
```

### Shorthand (uses defaults from properties)

```csharp
server.CreateWorld("island_042", "leveldb", WorldProfile.Light);
```

### Load from JSON

```csharp
WorldRegistration reg = WorldRegistrationLoader.LoadFromFile(path);
server.LoadWorld(reg.Identifier, "leveldb", reg, dataPath);
```

---

## WorldRegistrationDefaults

```csharp
public static class WorldRegistrationDefaults
{
    public static WorldRegistration ForProfile(
        WorldProfile profile,
        string identifier,
        Properties properties)
    {
        int[] workers = profile switch
        {
            WorldProfile.Hub => properties.HubWorkers,
            WorldProfile.Light => properties.LightWorkers,
            WorldProfile.Heavy => properties.HeavyWorkers,
            _ => [0]
        };

        return new WorldRegistration
        {
            Identifier = identifier,
            Profile = profile,
            AllowedWorkers = workers
        };
    }
}
```

---

## Validation errors

| Error | Cause |
|-------|-------|
| `AllowedWorkers must not be empty` | Missing array |
| `Worker X out of range` | Index >= world-thread-count |
| `PreferredWorker must be in AllowedWorkers` | Invalid preferred |
| `world-thread-count must be >= 1` | Invalid pool size |

Log level: **Error** on create; fail world creation.

---

## Feature flag behavior

| world-scheduler-enabled | Behavior |
|-------------------------|----------|
| `false` | Phase 1: main-thread queue, single tick thread, active-only worlds |
| `true` | Phase 3+: full worker pool, PickWorker, per-worker tick |

`world-thread-count` is ignored when scheduler disabled (except validation at startup for future enable).

---

## Related documents

- Registration model: [03-world-registration.md](./03-world-registration.md)
- PickWorker: [04-world-scheduler.md](./04-world-scheduler.md)
- Phases: [08-phased-implementation.md](./08-phased-implementation.md)
