# 08 — Phased Implementation

Incremental delivery plan. **Do not skip phases.** Each phase has explicit deliverables, files, and acceptance criteria.

Feature flag: `world-scheduler-enabled` (default `false` until Phase 3 stable).

---

## Phase 0 — Contracts and stubs ✅

**Status:** Complete.

### Deliverables

- [x] Create `Basalt/Scheduling/` folder with stub types
- [x] `WorldRegistration`, `WorldRegistrationLoader`, `IWorldScheduler`
- [x] `SingleThreadScheduler` — all methods noop or pass-through
- [x] Add optional `World.Registration` property (defaults for existing worlds)
- [x] Document-only validation helpers

### Files to create

| File | Purpose |
|------|---------|
| `Basalt/Scheduling/WorldRegistration.cs` | Registration model + Validate |
| `Basalt/Scheduling/WorldRegistrationLoader.cs` | Load per-world JSON |
| `Basalt/Scheduling/IWorldScheduler.cs` | Interface |
| `Basalt/Scheduling/SingleThreadScheduler.cs` | Noop implementation |
| `Basalt/Scheduling/WorldRegistrationDefaults.cs` | Fallback from server.properties |

### Files to modify

| File | Change |
|------|--------|
| `Basalt/World/World.cs` | Add `Registration`, `AttachedWorkerId`, `PresentPlayerCount`, `IsAttached` |
| `Basalt/Server.cs` | Hold `IWorldScheduler _scheduler`; default `SingleThreadScheduler` |

### Do NOT

- Change network handler behavior
- Add worker threads
- Break existing `Server.Players` API

### Acceptance criteria

- `dotnet build` succeeds
- Server starts and behaves identically to before
- All new types have XML doc comments

### Suggested PR

`feat(scheduling): add WorldRegistration and scheduler stubs (Phase 0)`

---

## Phase 1 — Single-thread marshaling + active-only ticking ✅

**Status:** Complete.

**Goal:** Fix thread safety on one tick thread; only tick worlds with players present.

### Deliverables

- [x] `PacketIngress` routes world-bound packets to main thread queue
- [x] `SingleThreadScheduler.DrainMainQueue()` called from `Server.Tick()`
- [x] `PresentPlayerCount` maintained on spawn/despawn/disconnect
- [x] `Server.Tick()` skips worlds with `PresentPlayerCount == 0`
- [x] Refactor handlers to `HandleGamePacketOnWorker` (still one thread)

### Files to create

| File | Purpose |
|------|---------|
| `Basalt/Scheduling/PacketIngress.cs` | Route global vs world-bound |
| `Basalt/Scheduling/Messages/IWorldMessage.cs` | Message interface |
| `Basalt/Scheduling/Messages/ProcessPacketMessage.cs` | Packet queue item |

### Files to modify

| File | Change |
|------|--------|
| `Basalt/Network/Network.cs` | Delegate to PacketIngress |
| `Basalt/Network/Handlers/*.cs` | World-bound logic callable from worker/main thread |
| `Basalt/Server.cs` | Drain queue before world tick; filter dormant worlds |
| `Basalt/Network/Handlers/ResourcePackClientResponse.cs` | Increment PresentPlayerCount on spawn |
| `Basalt/Network/Network.cs` | Decrement on disconnect |

### Do NOT

- Spawn multiple worker threads yet
- Split PlayerSession yet (optional overlap with Phase 2)

### Acceptance criteria

- No world state mutation from network thread (verify with debug assert)
- Empty worlds do not increment `TickValue`
- Manual: 2 players break/place blocks — no corruption
- Manual: login/logout 10× — no crash
- Automated tests from [09-testing.md](./09-testing.md) Phase 1

### Suggested PRs

1. `feat(scheduling): PacketIngress and main-thread queue (Phase 1a)`
2. `feat(scheduling): active-only world ticking (Phase 1b)`

---

## Phase 2 — PlayerSession split ✅

**Status:** Complete.

**Goal:** Separate session from entity; central `Server.Sessions` for plugins.

### Deliverables

- [x] `PlayerSession` class
- [x] `Server.Sessions` concurrent dictionary
- [x] Migrate all 15 network handlers to sessions
- [x] `Dimension.Broadcast` / simulation distance via sessions
- [x] Commands `list`, `tp`, target enums updated
- [x] Legacy `Server.Players` adapter (obsolete)

### Files to create

| File | Purpose |
|------|---------|
| `Basalt/Player/PlayerSession.cs` | Session type |
| `Basalt/Player/TransferState.cs` | Enum for transfers |

### Files to modify

See full map in [05-player-session-split.md](./05-player-session-split.md).

### Do NOT

- Enable multi-worker yet
- Remove `Players` adapter until internal migration complete

### Acceptance criteria

- `/list`, chat, `/tp` work
- Disconnect saves NBT via entity, session removed
- NPC spawn without session works
- Tests from [09-testing.md](./09-testing.md) Phase 2

### Suggested PRs

1. `feat(player): add PlayerSession and Sessions registry (Phase 2a)`
2. `refactor(network): migrate handlers to PlayerSession (Phase 2b)`
3. `refactor(world): Dimension broadcast via sessions (Phase 2c)`

---

## Phase 3 — Worker pool + dynamic attach/detach ✅

**Status:** Complete.

**Goal:** Real multi-worker simulation with PickWorker and per-world registration.

### Deliverables

- [x] `world-thread-count`, `world-scheduler-enabled` in Properties
- [x] `WorldWorkerPool`, `WorldWorker`, `WorldScheduler`
- [x] `PickWorker` with allowed workers + load metrics (no profiles)
- [x] `RequestAttach` / `RequestDetach` on first/last player
- [x] Packet routing to correct worker
- [x] Per-world JSON registration loader (`world.json`)

### Files to create

| File | Purpose |
|------|---------|
| `Basalt/Scheduling/WorldWorkerPool.cs` | Pool lifecycle |
| `Basalt/Scheduling/WorldWorker.cs` | Thread + inbox + tick |
| `Basalt/Scheduling/WorldScheduler.cs` | PickWorker attach detach |
| `Basalt/Scheduling/WorkerLoadMetrics.cs` | Metrics |
| `Basalt/Scheduling/Messages/AttachWorldMessage.cs` | Attach |
| `Basalt/Scheduling/Messages/DetachWorldMessage.cs` | Detach |

### Files to modify

| File | Change |
|------|--------|
| `Basalt/Properties.cs` | New config keys |
| `Basalt/Server.cs` | Start/stop pool; replace SingleThreadScheduler when enabled |
| `Basalt/Server.CreateWorld` | Accept WorldRegistration |
| `Basalt/Scheduling/PacketIngress.cs` | Enqueue to worker id |

### Do NOT

- Cross-worker transfer yet (same-worker teleport only)
- Plugin API breaking changes without adapter

### Acceptance criteria

- Two worlds with `allowedWorkers: [1]` each attach to worker 1 when load is equal
- One world with `allowedWorkers: [2]` attaches to worker 2 — isolated from worker 1
- Heavy tick cost on worker 2 does not block worlds on worker 1
- Skyblock: attach only when player enters; detach when last leaves (`[Detach]` log)
- PickWorker respects allowedWorkers (unit tests)
- Manual smoke: login, spawn, movement, commands, disconnect — all on correct worker

### Suggested PRs

1. `feat(scheduling): WorldWorkerPool and worker tick loop (Phase 3a)`
2. `feat(scheduling): attach detach and PickWorker (Phase 3b)`
3. `feat(scheduling): multi-worker packet routing (Phase 3c)`

---

## Phase 4 — Cross-worker transfer ✅

**Status:** Complete.

**Goal:** Teleport and world changes across workers.

### Deliverables

- [x] `PlayerEntitySnapshot`
- [x] `PrepareTransferMessage`, `CompleteTransferMessage`
- [x] `TransferState` on session (blocks packets during transfer)
- [x] Update `Teleport.cs` — `/tp <worldName>` and cross-worker branch
- [x] `Player.Teleport(..., forceDimensionChange)` for cross-world client sync
- [x] Abort/recovery on failure (disconnect with message)

### Files added/modified

| File | Change |
|------|--------|
| `Basalt/Scheduling/PlayerEntitySnapshot.cs` | Snapshot model |
| `Basalt/Scheduling/CrossWorldTransferHandler.cs` | Prepare/Complete logic |
| `Basalt/Scheduling/Messages/PrepareTransferMessage.cs` | Source worker message |
| `Basalt/Scheduling/Messages/CompleteTransferMessage.cs` | Target worker message |
| `Basalt/Scheduling/WorldScheduler.cs` | `BeginCrossWorldTransfer`, improved `GetWorkerLoad` |
| `Basalt/Commands/List/Operator/Teleport.cs` | `/tp world_copy`, cross-worker path |
| `Basalt/Properties.cs` | `additional-worlds` |
| `worlds/world_copy/` | LevelDB copy + `world.json` with `[0,1]` |

### Acceptance criteria

- `/tp world_copy` from `world` attaches target on worker 1 when worker 0 is loaded
- Client receives `ChangeDimension` on cross-world transfer
- No duplicate entities after transfer (snapshot despawn + respawn)
- 5 automated tests in `CrossWorkerTransferTests.cs`

---

## Phase 5 — Observability and plugins

**Goal:** Metrics, debug commands, plugin thread rules.

### Deliverables

- [ ] `WorkerLoadMetrics` exposed via API
- [ ] Command `/worldscheduler` or `/scheddebug` (op only)
- [ ] `Server.RunOnWorldThread(world, action)` helper
- [ ] Event affinity: world events on worker thread in `Server.Emit`
- [ ] Plugin documentation in agent guide
- [ ] Remove obsolete `Server.Players` adapter

### Files to create

| File | Purpose |
|------|---------|
| `Basalt/Commands/List/Operator/WorldSchedulerDebug.cs` | Debug command |

### Acceptance criteria

- Metrics show per-worker TPS and active world count
- Plugin handler runs on correct thread (manual test)
- Tests from [09-testing.md](./09-testing.md) Phase 5

---

## Dependency graph

```mermaid
flowchart LR
    P0[Phase0 Stubs]
    P1[Phase1 Marshaling]
    P2[Phase2 SessionSplit]
    P3[Phase3 WorkerPool]
    P4[Phase4 CrossWorker]
    P5[Phase5 Observability]

    P0 --> P1
    P1 --> P2
    P2 --> P3
    P3 --> P4
    P4 --> P5
```

Phase 2 and Phase 1 can overlap slightly, but **Phase 3 requires Phase 1 and Phase 2 complete**.

---

## Global Definition of Done

For any phase:

- [ ] `dotnet build` Debug and Release
- [ ] No new linter errors in touched files
- [ ] Applicable automated tests pass
- [ ] Manual checklist in [09-testing.md](./09-testing.md) completed
- [ ] Debug logs behind `world-scheduler-debug`
- [ ] Update agent guide if new patterns introduced

---

## Related documents

- Tests: [09-testing.md](./09-testing.md)
- Agent guide: [11-agent-implementation-guide.md](./11-agent-implementation-guide.md)
- Config: [10-config-reference.md](./10-config-reference.md)
