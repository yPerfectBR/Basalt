# 11 — Agent Implementation Guide

Operational guide for AI agents (and human developers) implementing the World Scheduler incrementally.

---

## Before writing code

1. Read [00-overview.md](./00-overview.md) and [01-current-state.md](./01-current-state.md).
2. Read [02-core-concepts.md](./02-core-concepts.md) — **memorize invariants**.
3. Identify current phase in [08-phased-implementation.md](./08-phased-implementation.md).
4. Read phase-specific docs (e.g. Phase 3 → [04-world-scheduler.md](./04-world-scheduler.md), [06-packet-routing.md](./06-packet-routing.md)).
5. Check tests to add/run in [09-testing.md](./09-testing.md).

**Never skip phases.** Never enable multi-worker before Phase 1 marshaling is complete.

---

## Golden rules

| Rule | Detail |
|------|--------|
| R1 | Do not mutate `Dimension`, `Entity`, or blocks off the owning worker thread |
| R2 | Do not assign a world to a worker outside `AllowedWorkers` |
| R3 | Do not tick worlds with `PresentPlayerCount == 0` |
| R4 | Do not break `Server.Sessions` as the plugin-facing online registry |
| R5 | Keep `world-scheduler-enabled=false` default until Phase 3 is tested |
| R6 | Prefer messages over locks for cross-thread work |
| R7 | Preserve behavior when feature flags are off |

---

## PR template

```markdown
## World Scheduler — Phase X.Y: [title]

### Phase
- [ ] Phase 0 / 1 / 2 / 3 / 4 / 5 (check one)

### Summary
[1-3 sentences]

### Files changed
- Created: ...
- Modified: ...

### Invariants verified
- [ ] INVARIANT 1 World state only on worker thread
- [ ] INVARIANT 3 Dormant worlds not ticked
- [ ] (others applicable)

### Tests
- Automated: [list or "N/A for Phase 0"]
- Manual: [checklist from 09-testing.md]

### Feature flags
- world-scheduler-enabled: unchanged / true / false

### Out of scope
[what this PR intentionally does NOT do]
```

---

## Typical edit order by phase

### Phase 0

1. `Basalt/Scheduling/WorldRegistration.cs`
2. `Basalt/Scheduling/WorldRegistrationLoader.cs`
3. `Basalt/Scheduling/IWorldScheduler.cs`
4. `Basalt/Scheduling/SingleThreadScheduler.cs`
5. `Basalt/World/World.cs` — properties only
6. `Basalt/Server.cs` — wire noop scheduler

### Phase 1

1. `Basalt/Scheduling/Messages/*`
2. `Basalt/Scheduling/PacketIngress.cs`
3. `Basalt/Network/Network.cs` — call PacketIngress
4. Refactor `NetworkHandler.HandleGamePacket` → thread-safe entry point
5. `Basalt/Server.cs` — drain queue, skip dormant worlds
6. Spawn/disconnect — maintain `PresentPlayerCount`
7. `tests/Basalt.Tests/` — Phase 1 tests

### Phase 2

1. `Basalt/Player/PlayerSession.cs`
2. `Basalt/Server.cs` — `Sessions`
3. Handlers one file at a time (start with `Login.cs`, `Network.cs`)
4. `Dimension.cs` — broadcast/simulation
5. Commands — `List.cs`, `TargetEnum.cs`, `Teleport.cs`
6. Strip session fields from `Player.cs`

### Phase 3

1. `Basalt/Properties.cs` — config keys
2. `WorldWorker.cs`, `WorldWorkerPool.cs`
3. `WorldScheduler.cs` — PickWorker, attach, detach
4. Replace/enhance `PacketIngress` worker routing
5. `Server.Start/Stop` — pool lifecycle
6. Per-world JSON loader (optional)

### Phase 4

1. `PlayerEntitySnapshot`
2. Transfer messages + session `TransferState`
3. `Teleport.cs` cross-worker branch
4. Integration tests

### Phase 5

1. Metrics API + debug command — **done**
2. `RunOnWorldThread` helper — **done**
3. `Server.Emit` affinity — **done**
4. Remove `Players` adapter — **done**

---

## Plugin thread rules (Phase 5)

Plugins interact with the server at two levels:

**Session / global (any thread):**

- Iterate `server.Sessions` for online identity
- Register handlers for `ServerStart`, `PlayerJoin` (before spawn)
- Read-only session fields (`Username`, `Uuid`, connection)

**World-bound (worker thread only):**

- Mutate blocks, entities, inventories, or world state
- Handle `PlayerBreakBlock`, `PlayerSpawn`, `EntityHurt`, etc.
- Use `server.RunOnWorldThread(world, () => { ... })` when posting work from console or async code

**Rules:**

| Do | Don't |
|----|-------|
| Resolve `Player` via `session.ActiveEntity` each tick | Cache `Player` in static plugin fields |
| Use `RunOnWorldThread` for world mutations from non-worker threads | Call `Dimension.SetBlock` from Login handler |
| Check `session.ActiveEntity?.Dimension?.World` before world logic | Assume `ActiveEntity` survives transfer |

Event affinity is enforced in `Server.Emit`: global events run inline; world-bound events are dispatched synchronously on the owning worker via `RunOnWorldThread`.

Debug: `/worldscheduler` (alias `/scheddebug`) prints per-worker TPS, active worlds, and player counts.

## Logging conventions

Use structured prefixes when `world-scheduler-debug=true`:

```
[Scheduler] PickWorker world=island_042 allowed=[1,2] chosen=1 score=12.4
[Worker:1] Attach world=island_042 activeWorlds=5
[Worker:1] Detach world=island_042 activeWorlds=4
[Worker:1] Tick workMs=8.2 tps=20.0
[PacketIngress] enqueue worker=1 session=Steve packet=PlayerAuthInput
[Transfer] PrepareTransfer session=Steve from=island_042 to=dungeon_01
[Transfer] CompleteTransfer session=Steve worker=3
```

---

## Anti-patterns

| Anti-pattern | Why wrong | Fix |
|--------------|-----------|-----|
| `lock(player)` in handlers | Deadlocks, hides design flaw | Enqueue to worker |
| Iterate all worlds every tick | Skyblock scale failure | Active worlds only |
| `new Thread()` per world | Unbounded threads | Fixed pool |
| Cache `Player` in plugin static | Stale after transfer | Store session or uuid |
| Mutate blocks in Login handler on network thread | Race with tick | Global handler only for auth |
| Skip PresentPlayerCount on disconnect | Worlds never detach | Always decrement in despawn path |
| Hardcode worker 1 for Skyblock | Ignores registration | Use PickWorker + AllowedWorkers |

---

## Pre-merge checklist (copy per PR)

### All phases

- [ ] `dotnet build` succeeds
- [ ] Phase scope not exceeded
- [ ] Feature flag default safe
- [ ] Debug logs guarded by `world-scheduler-debug`
- [ ] No unrelated refactors

### Phase 1+

- [ ] No world mutation on network thread (ThreadGuard or review)
- [ ] Dormant worlds not ticked

### Phase 2+

- [ ] Sessions registry used for online identity
- [ ] `/list` accurate

### Phase 3+

- [ ] PickWorker respects AllowedWorkers (unit test)
- [ ] Attach/detach logs present in debug mode
- [ ] `world-scheduler-enabled=false` still works

### Phase 4+

- [ ] Transfer does not leave session in Transferring state
- [ ] Inventory survives cross-worker tp

---

## Handling unknowns

If codebase diverges from docs:

1. Update [01-current-state.md](./01-current-state.md) with delta.
2. Do not guess thread model — trace from `Server.Start()`.
3. Prefer minimal diff aligned with current phase.
4. Ask human before breaking plugin-facing APIs.

---

## Code references (quick links)

| Topic | File |
|-------|------|
| Tick loop | [`Basalt/Server.cs`](../../../Basalt/Server.cs) |
| Players today | [`Basalt/Server.cs`](../../../Basalt/Server.cs) L62 |
| Network ingress | [`Basalt/Network/Network.cs`](../../../Basalt/Network/Network.cs) |
| Dimension tick | [`Basalt/World/Dimension/Dimension.cs`](../../../Basalt/World/Dimension/Dimension.cs) |
| Chunk async pattern | [`Basalt/World/Dimension/Dimension.cs`](../../../Basalt/World/Dimension/Dimension.cs) |
| Player teleport | [`Basalt/Player/Player.cs`](../../../Basalt/Player/Player.cs) |
| Properties | [`Basalt/Properties.cs`](../../../Basalt/Properties.cs) |
| Login flow | [`Basalt/Network/Handlers/Login.cs`](../../../Basalt/Network/Handlers/Login.cs) |

---

## Related documents

- Index: [README.md](./README.md)
- Phases: [08-phased-implementation.md](./08-phased-implementation.md)
- Tests: [09-testing.md](./09-testing.md)
- Config: [10-config-reference.md](./10-config-reference.md)
