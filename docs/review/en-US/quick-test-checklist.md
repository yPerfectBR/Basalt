# Quick Test Checklist

Direct smoke tests to validate the `world-scheduler` branch. Estimated time: **~15 minutes**.

Reference config: [`server.properties`](../../../server.properties) with scheduler enabled and `world_copy` loaded.

[Versão em português](../pt-br/quick-test-checklist.md)

---

## 1. Prerequisites

- [ ] `world-scheduler-enabled=true`
- [ ] `world-thread-count=4`
- [ ] `world-scheduler-debug=true`
- [ ] `additional-worlds=world_copy`
- [ ] Build: `dotnet build Basalt/Basalt.csproj`
- [ ] Server running; Bedrock client connects on configured port

---

## 2. Basic smoke (general regression)

| Step | Action | Expected |
|------|--------|----------|
| Join | Connect to server | Spawn in overworld, no crash |
| Movement | Walk ~20 blocks | Chunks load, no rubber-banding |
| Block | Break and place a block | Block updates for everyone |
| Chat | Send a message | Other players see it (if any) |
| List | `/list` | Correct name and count |
| Disconnect | Leave | Leave message; no console error |

---

## 3. Scheduler and metrics

| Step | Command | Expected |
|------|---------|----------|
| Metrics | `/worldscheduler` or `/scheddebug` | Table with 4 workers; worker with active `world` shows ≥1 world and ≥1 player |
| Debug log | (console) | `[Attach] world=world worker=N` lines after join |

Aligned columns: `W`, `Wrlds`, `Plrs`, `TPS`, `WorkMs`, `LagMs`.

Idle workers: TPS ~20.0, WorkMs and LagMs ~0.

---

## 4. Cross-world transfer

| Step | Command | Expected |
|------|---------|----------|
| TP out | `/tp world_copy` | Client loads world (no infinite "building terrain") |
| Metrics | `/worldscheduler` | `world_copy` appears on some worker (may differ from worker 0) |
| Debug | (console) | `[Transfer]` / `[Attach] world=world_copy` if world was dormant |
| TP back | `/tp world` | Return to default world works |

---

## 5. Per-world state (inventory)

| Step | Action | Expected |
|------|--------|----------|
| Setup | In `world`, `/give @s diamond 64` | Hotbar with diamonds |
| Default TP | `/tp world_copy` (no flags) | **world_copy** save inventory (diamonds **do not** carry over) |
| Carry | `/tp world` → `/give @s diamond 64` → `/tp world_copy --carry inventory` | Diamonds **carried** to world_copy |
| Position | `/tp world_copy --carry position` | Relative position kept (when applicable to save) |

---

## 6. Single-thread regression (optional)

| Step | Action | Expected |
|------|--------|----------|
| Config | `world-scheduler-enabled=false`, restart | Server starts normally |
| Smoke | Join, move, block, `/list` | Phase 1 behavior (queue on tick thread) |
| Metrics | `/worldscheduler` | Single row (worker 0, single-thread mode) |

---

## 7. Automated

```bash
dotnet test tests/Basalt.Tests/Basalt.Tests.csproj
```

All 38 tests should pass.

Useful filters:

```bash
dotnet test --filter WorldSchedulerObservability
dotnet test --filter CrossWorkerTransfer
dotnet test --filter PlayerWorldTransfer
```

---

## Common issues

| Symptom | Check |
|---------|-------|
| Endless "building terrain" after `/tp` | Transfer logs; `ChangeDimensionAck`; chunk resync |
| Wrong inventory after TP | Default uses destination save; use `--carry inventory` |
| World never detaches | `PresentPlayerCount` and `[Detach]` logs |
| Command permission denied | `/op <nick>` in correct world (op is per-world in LevelDB) |

More details: [implementation-notes.md](./implementation-notes.md).
