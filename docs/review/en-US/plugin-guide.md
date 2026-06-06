# Plugin Guide — World Scheduler

How to write plugins that respect threads, worlds, and sessions on the multi-worker Basalt scheduler.

[Versão em português](../pt-br/plugin-guide.md)

Example source: [`samples/plugins/`](../../../samples/plugins/)

---

## Build and load

```bash
dotnet build samples/plugins/JoinAnnouncer/JoinAnnouncer.csproj
dotnet build samples/plugins/SpawnWelcome/SpawnWelcome.csproj
dotnet build samples/plugins/BreakGuard/BreakGuard.csproj
dotnet build samples/plugins/SchedulerSnapshot/SchedulerSnapshot.csproj
```

Each build copies the DLL to `plugins/` at the repo root.

In `server.properties`:

```properties
plugins-directory=plugins
```

Restart the server and run `/plugins` to list loaded plugins.

---

## Lifecycle

| Method | When |
|--------|------|
| `OnLoad` | DLL loaded (light setup) |
| `OnStart` | Server starting — **register events here** |
| `OnDisable` | Shutdown |

Helpers on base [`Plugin`](../../../Basalt/Plugins/Plugin.cs):

- `Listen<T>(ServerEvent, Action<T>)` — register handler
- `RunOnWorld(World, Action)` — run on the world's worker
- `TryGetPlayerWorld(Player, out World?)` — resolve active world
- `ForEachOnlineSession(Action<PlayerSession>)` — iterate sessions (read-only)
- `Log(string)` — log with `[Plugin:Name]` prefix

**Current limitation:** there is no `Server.Off`; handlers remain until restart (fine for samples).

---

## Event → thread

| Event | Thread | Can mutate world? |
|-------|--------|-------------------|
| `ServerStart` | Global (start) | Not directly — use `RunOnWorld` |
| `PlayerJoin` | Global (login) | No — player not spawned yet |
| `PlayerSpawn` | World worker | Yes (in handler) |
| `PlayerBreakBlock` | World worker | Yes (including `Cancel()`) |
| `PlayerChat` | World worker | Safe reads; careful with broadcast |
| `PlayerLeave` | World worker | Yes |
| `EntityHurt` / `EntityDie` | World worker | Yes |

`Server.Emit` dispatches world-bound events on the correct worker via `RunOnWorldThread` when needed.

---

## Four sample plugins

### JoinAnnouncer — global thread

- **Path:** `samples/plugins/JoinAnnouncer/`
- **Event:** `PlayerJoin`
- **Does:** logs username and thread; **does not** touch blocks/entities
- **Lesson:** login runs before spawn; `player.Dimension` is still `null`

### SpawnWelcome — worker thread (automatic)

- **Path:** `samples/plugins/SpawnWelcome/`
- **Event:** `PlayerSpawn`
- **Does:** sends world name and `AttachedWorkerId` to the player
- **Lesson:** handler already runs on the correct worker; `player.Dimension.World` is safe to read

### BreakGuard — synchronous cancel

- **Path:** `samples/plugins/BreakGuard/`
- **Event:** `PlayerBreakBlock`
- **Does:** cancels bedrock or Y&lt;1 breaks; message to player
- **Lesson:** `signal.Cancel()` must run on the worker; core rolls back the block when cancelled

### SchedulerSnapshot — explicit `RunOnWorldThread`

- **Path:** `samples/plugins/SchedulerSnapshot/`
- **Event:** `ServerStart`
- **Does:** logs scheduler mode; for each active world (or default), calls `RunOnWorld` and logs `PresentPlayerCount` + worker on the worker thread
- **Lesson:** iterating `Server.Worlds` is global; simulation inspection requires `RunOnWorld`

---

## Rules for plugins

| Do | Don't |
|----|-------|
| Use `Server.Sessions` for online identity | Use `Server.Players` (removed) |
| Resolve `session.ActiveEntity` each use | Cache `Player` in static fields |
| Mutate world inside world-bound handlers | Call `Dimension.SetBlock` in `PlayerJoin` |
| Use `RunOnWorld` from global/async code | Assume all handlers share one thread |
| Verify `session.ActiveEntity` after transfer | Assume entity stays valid across ticks |

---

## Minimal skeleton

```csharp
using Basalt.Server.Events;
using Basalt.Server.Plugins;

[assembly: Plugin("MyPlugin", "1.0.0", Authors = ["You"])]

public sealed class MyPlugin : Plugin
{
    public override void OnStart()
    {
        Listen<PlayerSpawnSignal>(ServerEvent.PlayerSpawn, signal =>
        {
            Log($"spawn {signal.Player.Username}");
        });
    }
}
```

Project: `ProjectReference` to `Basalt/Basalt.csproj`, copy DLL to `plugins/` (see `samples/plugins/Directory.Build.props`).

---

## Anti-patterns

1. **Storing `Player` after join** — invalid after cross-world transfer
2. **Iterating worlds and calling `world.Tick()`** — only workers tick active worlds
3. **Mutating inventory in `PlayerJoin`** — use `PlayerSpawn` on worker or `RunOnWorld`
4. **Assuming single thread** — with `world-scheduler-enabled=true`, there are N workers

---

## References

- [implementation-notes.md](./implementation-notes.md)
- [quick-test-checklist.md](./quick-test-checklist.md) — optional plugins step
- [11-agent-implementation-guide.md](../../architecture/world-scheduler/11-agent-implementation-guide.md)
