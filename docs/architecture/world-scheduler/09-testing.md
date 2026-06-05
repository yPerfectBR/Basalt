# 09 — Testing

Test matrix per phase: automated tests, manual verification, regression, and stress scenarios.

Suggested test project: `tests/Basalt.Tests/` (xUnit or NUnit). Create in Phase 1.

---

## Test infrastructure setup

```
tests/Basalt.Tests/
├── Basalt.Tests.csproj          # ProjectReference to Basalt
├── Scheduling/
│   ├── PickWorkerTests.cs       # Phase 3
│   ├── WorldRegistrationTests.cs
│   └── ActiveWorldTickTests.cs  # Phase 1
├── Player/
│   └── PlayerSessionTests.cs    # Phase 2
└── Integration/
    ├── AttachDetachTests.cs     # Phase 3
    └── CrossWorkerTransferTests.cs  # Phase 4
```

Run: `dotnet test tests/Basalt.Tests/Basalt.Tests.csproj`

---

## Phase 0 — Stubs

### Automated

| Test | Assertion |
|------|-----------|
| `WorldRegistration_Validate_RejectsEmptyAllowedWorkers` | Throws |
| `WorldRegistration_Validate_RejectsOutOfRangeWorker` | Index >= count throws |
| `WorldRegistrationDefaults_Light_ReturnsExpectedWorkers` | Matches config |

### Manual

- [ ] Server starts with no config changes
- [ ] `/list`, join, break block — unchanged behavior

---

## Phase 1 — Marshaling + active-only tick

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `ProcessPacketMessage_ProcessedOnMainThread` | unit | Handler runs on same thread as Tick drain |
| `DormantWorld_DoesNotIncrementTickValue` | unit | `PresentPlayerCount=0` → no Tick call |
| `ActiveWorld_IncrementsTickValue` | unit | One player → TickValue++ each server tick |
| `PacketIngress_GlobalHandler_DoesNotRequireEntity` | unit | Login path does not enqueue |

### Manual

- [ ] 2 players online simultaneously — break and place blocks, no ghost blocks
- [ ] Login/logout 10 consecutive times — no crash, no duplicate players in `/list`
- [ ] Empty secondary world (created but never joined) — confirm CPU/TPS unchanged when idle
- [ ] Chat and commands still work

### Regression

- [ ] `dotnet build -c Release` (note AOT constraints for plugins separately)
- [ ] Default commands: `tp`, `gamemode`, `give`, `list`, `status`
- [ ] Disconnect saves and reloads position/inventory ([`Network.HandleDisconnected`](../../../Basalt/Network/Network.cs))

### Debug verification

With `world-scheduler-debug=true`:

- [ ] Logs show `[PacketIngress] enqueue` for movement packets
- [ ] Logs show processing on main/tick thread, not network thread name

---

## Phase 2 — PlayerSession split

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `Session_SurvivesEntityDespawn` | unit | ActiveEntity null, session still in Sessions |
| `NpcPlayer_HasNoSession` | unit | `player.Session == null` |
| `Sessions_GetOnlineEntities_SkipsNullActiveEntity` | unit | Mid-transfer excluded |
| `LegacyPlayersAdapter_MatchesSessions` | unit | Compatibility shim |

### Manual

- [ ] `/list` shows correct count and names
- [ ] Global chat broadcast reaches all players
- [ ] `/tp @s @p` between two players
- [ ] Operator permissions still work (`/op`, `/deop`, `/gamemode`)
- [ ] Player list packet on join shows skins

### Regression

- [ ] Duplicate login kick still works ([`Login.cs`](../../../Basalt/Network/Handlers/Login.cs))
- [ ] NBT load on login preserves gamemode and op status

---

## Phase 3 — Worker pool

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `PickWorker_RespectsAllowedWorkers` | unit | Never returns disallowed index |
| `PickWorker_ChoosesLowerLoadScore` | unit | Mock metrics, verify pick |
| `PickWorker_UsesPreferredWorkerWhenSet` | unit | Skips scoring |
| `PickWorker_TieBreaksByFewerWorlds` | unit | Equal score → fewer ActiveWorldCount |
| `Attach_OnFirstPlayer_SetsAttachedWorkerId` | integration | World attached |
| `Detach_OnLastPlayer_ClearsAttachedWorkerId` | integration | World dormant |
| `EnqueuePacket_RoutesToCorrectWorker` | integration | Message inbox on expected worker |

### Manual

- [ ] Config: `world-thread-count=4`, `world-scheduler-enabled=true`
- [ ] Create 2 Light worlds (allowedWorkers `[1]`) with 1 player each — both on worker 1
- [ ] Create 1 Heavy world (allowedWorkers `[2]`) — on worker 2
- [ ] Artificial lag in Heavy world (many entities) — Light worlds maintain playable TPS
- [ ] Last player leaves Light world — `[Detach]` log, world stops ticking
- [ ] 20 Light worlds, 1 player each, allowedWorkers `[1,2]` — scheduler distributes (~10/10 or by load)

### Stress

- [ ] 50 connect/disconnect cycles across random worlds
- [ ] 10 players on same worker — measure worker TPS in metrics
- [ ] Run server 30 minutes — no memory leak from attach/detach cycles

### Regression

- [ ] `world-scheduler-enabled=false` restores Phase 1 single-thread behavior
- [ ] Default world (Hub) still loads on startup

---

## Phase 4 — Cross-worker transfer

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `CrossWorkerTransfer_SessionHasOneEntityAfterComplete` | integration | Single ActiveEntity |
| `CrossWorkerTransfer_SourceWorldCountDecrements` | integration | PresentPlayerCount |
| `CrossWorkerTransfer_TargetWorldAttachIfDormant` | integration | PickWorker called |
| `SameWorkerTransfer_SkipsSnapshotProtocol` | unit | Direct Teleport path |
| `TransferFailure_SessionNotStuckTransferring` | integration | Abort resets state |

### Manual

- [ ] Teleport player from Light world (worker 1) to Heavy world (worker 2) via `/tp`
- [ ] Player sees correct position and dimension client-side
- [ ] Inventory preserved after transfer
- [ ] Return teleport Heavy → Light works
- [ ] Transfer to dormant island — world attaches, chunks load
- [ ] Two players transfer to same dormant world sequentially — count=2, single attach

### Regression

- [ ] Same-world dimension teleport (overworld → nether if exists) still works
- [ ] Disconnect during transfer — no hung session (abort or cleanup)

---

## Phase 5 — Observability and plugins

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `GetMetrics_ReturnsAllWorkers` | unit | Count == world-thread-count |
| `Metrics_ActiveWorldCount_UpdatesOnAttachDetach` | integration | |
| `RunOnWorldThread_ExecutesOnWorkerThread` | unit | Thread id match |

### Manual

- [ ] `/worldscheduler` (or debug command) prints worker load table
- [ ] Test plugin registers `PlayerBreakBlock` handler — fires on correct worker
- [ ] Test plugin global join handler fires on session thread
- [ ] `world-scheduler-debug` logs readable under load

---

## Continuous regression checklist

Run before merging any scheduling PR:

```
[ ] dotnet build
[ ] dotnet test (if test project exists)
[ ] Manual smoke: join, move, break block, chat, disconnect
[ ] No new concurrent access warnings in debug build (optional: ThreadGuard assert)
```

---

## ThreadGuard (optional debug helper)

Implement in Phase 1 for debug builds:

```csharp
public static void AssertWorldThread(World world)
{
    Debug.Assert(world.AttachedWorkerId == WorldWorker.CurrentWorkerId);
}
```

Call at start of world mutation methods during development.

---

## Related documents

- Phases: [08-phased-implementation.md](./08-phased-implementation.md)
- Agent checklist: [11-agent-implementation-guide.md](./11-agent-implementation-guide.md)
