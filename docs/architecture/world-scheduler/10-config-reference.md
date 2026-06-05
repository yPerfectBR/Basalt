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
| `world-default-allowed-workers` | int[] | *(empty)* | 3 | Fallback worker indices when a world has no `world.json`. Empty = all workers `0..count-1` |

Parser example: `"1,2"` → `[1, 2]`. Empty string → `[0, 1, …, count-1]`.

### Example server.properties

```properties
server-port=19132
max-players=100

# World Scheduler (Phase 3+)
world-thread-count=6
world-scheduler-enabled=false
world-scheduler-debug=false

# Fallback when world.json is missing (empty = all workers)
world-default-allowed-workers=
```

---

## Worker index layout (recommended 6-thread setup)

| Index | Suggested role | Example worlds |
|-------|----------------|----------------|
| `0` | Hub / spawn | `world` with `"allowedWorkers": [0]` |
| `1`, `2` | Light simulation | Skyblock islands with `"allowedWorkers": [1, 2]` |
| `3`, `4`, `5` | Heavy simulation | Dungeons with `"allowedWorkers": [3, 4, 5]` |

This is **convention only** — actual mapping is defined per world in `world.json` or the create API.

---

## Per-world registration JSON

### Path resolution

Search order:

1. `{world-path}/world.json` (alongside LevelDB data)
2. `worlds/{identifier}.json`
3. API argument to `CreateWorld` / `LoadWorld`
4. `world-default-allowed-workers` from server.properties

### Schema

```json
{
  "$schema": "optional",
  "identifier": "string (optional, must match world name if set)",
  "allowedWorkers": [1, 2],
  "preferredWorker": null,
  "maxConcurrentPlayers": 2147483647
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `identifier` | no | Should match world name; used for validation warnings |
| `allowedWorkers` | yes | Non-empty array of valid worker indices |
| `preferredWorker` | no | Must be in `allowedWorkers`; skips load balancing |
| `maxConcurrentPlayers` | no | Soft reject or queue when full (future) |

### Examples

**Skyblock island**

```json
{
  "identifier": "island_042",
  "allowedWorkers": [1, 2]
}
```

**Dungeon instance**

```json
{
  "identifier": "dungeon_boss_01",
  "allowedWorkers": [3, 4, 5],
  "preferredWorker": 4,
  "maxConcurrentPlayers": 8
}
```

**Hub**

```json
{
  "identifier": "world",
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
        AllowedWorkers = [1, 2]
    },
    providerArgs: Path.Combine("worlds", "island_042"));
```

### Load from JSON

```csharp
WorldRegistration reg = WorldRegistrationLoader.LoadFromFile(path, expectedIdentifier, workerCount);
server.LoadWorld(reg.Identifier, "leveldb", reg, dataPath);
```

---

## WorldRegistrationDefaults

```csharp
public static class WorldRegistrationDefaults
{
    public static WorldRegistration ForWorld(string identifier, Properties properties)
    {
        int[] workers = ParseWorkers(properties.DefaultAllowedWorkers, properties.WorldThreadCount);
        return new WorldRegistration
        {
            Identifier = identifier,
            AllowedWorkers = ClampWorkers(workers, properties.WorldThreadCount)
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
