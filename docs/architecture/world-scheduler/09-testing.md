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
│   ├── PlayerSessionTests.cs    # Phase 2
│   └── PlayerWorldTransferTests.cs  # Per-world transfer
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
| `WorldRegistrationDefaults_ForWorld_UsesAllWorkersWhenUnset` | Empty config → all workers |
| `WorldRegistrationDefaults_ForWorld_ParsesConfiguredWorkers` | Parses comma list |
| `WorldRegistrationLoader_LoadsFromWorldJson` | Reads `world.json` |

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

- [x] Logs show `[PacketIngress] inline packet=...` for Login / RequestNetworkSettings
- [x] Logs show `[PacketIngress] enqueue worker=N packet=...` for world-bound packets (Phase 3)
- [x] Logs show `[Worker:N] ProcessPacketMessage packet=...` on worker thread
- [x] No duplicate enqueue line per packet (single log in `WorldScheduler`)

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
- [ ] Operator permissions work **per world** (`/op`, `/deop` — status saved in that world's LevelDB)
- [ ] Player list packet on join shows skins

### Regression

- [ ] Duplicate login kick still works ([`Login.cs`](../../../Basalt/Network/Handlers/Login.cs))
- [ ] NBT load on login preserves gamemode and op status **for the default world only**

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

- [x] Config: `world-scheduler-enabled=true`, `additional-worlds=world_copy`
- [x] Login on `world` → `[Attach] world=world worker=0`
- [x] `/tp world_copy` → `[Transfer]` + `[Attach] world=world_copy worker=1` + gameplay on worker 1
- [ ] `/tp world` return transfer to worker 0
- [ ] **Default cross-world:** inventory and gamemode come from **target** world save (not source)
- [ ] `/tp world_copy --carry inventory` preserves source inventory
- [ ] `/tp world_copy --carry position` uses source coordinates; without flag uses target save or spawn

### Stress

- [ ] 50 connect/disconnect cycles across random worlds
- [ ] 10 players on same worker — measure worker TPS in metrics
- [ ] Run server 30 minutes — no memory leak from attach/detach cycles

### Regression

- [ ] `world-scheduler-enabled=false` restores Phase 1 single-thread behavior
- [x] Default world loads on startup with `world.json` registration

---

## Phase 4 — Cross-worker transfer

### Automated

| Test | Type | Assertion |
|------|------|-----------|
| `CrossWorkerTransfer_PickWorkerChoosesFreerThread` | unit | `[0,1]` picks worker 1 when worker 0 loaded |
| `CrossWorkerTransfer_TargetWorldAttachIfDormant` | integration | Dormant world gets `AttachedWorkerId` |
| `CrossWorkerTransfer_SessionHasOneEntityAfterComplete` | integration | Single entity on target worker after transfer |
| `SameWorkerCrossWorld_UsesPerWorldTransferWithoutSnapshotProtocol` | integration | Same worker cross-world uses `PlayerWorldTransfer` |
| `TransferFailure_SessionNotStuckTransferring` | integration | Abort clears `TransferState` |

| `BuildEntityNbtFromSnapshot_WithoutCarry_UsesTargetSave` | unit | Target LevelDB base, source ignored |
| `BuildEntityNbtFromSnapshot_WithCarryInventory_MergesSourceInventory` | unit | `--carry inventory` merge |
| `ResolveDestinationTransform_*` | unit | Saved position, carry position, explicit coords, default spawn |
| `SaveToWorld_PersistsPlayerDataOnProvider` | unit | Source saved before despawn |

Run: `dotnet test --filter PlayerWorldTransfer`

Run: `dotnet test --filter CrossWorkerTransfer`

### Manual

- [x] `/tp world_copy` from default world — verify worker 1 in debug logs
- [ ] Teleport between worlds on different workers via coordinates + dimension
- [ ] **Default:** target world save used (inventory isolated per world)
- [ ] `--carry inventory` and `--carry position` behave as documented
- [ ] Return `/tp world` works

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
