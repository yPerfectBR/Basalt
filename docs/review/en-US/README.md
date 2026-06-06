# Review — World Scheduler Branch

Guide for developers familiar with the **original Basalt** who want to validate the `world-scheduler` branch without reading the full architecture documentation.

[Versão em português](../pt-br/README.md)

---

## What changed (2 minutes)

| Before | After |
|--------|-------|
| One tick thread for all worlds | Worker pool (`world-thread-count`); each active world runs on a worker |
| `Server.Players` (entities keyed by connection) | `Server.Sessions` + `session.ActiveEntity` |
| Teleport within same process/world only | `/tp world_copy` across worlds, including different workers |
| Global player inventory | **Per-world** state (each world's LevelDB); `--carry` flags on `/tp` |
| No per-worker metrics | `/worldscheduler` (alias `/scheddebug`) |

**Dormant** worlds (zero players) are not ticked. The first player to enter triggers **attach**; the last to leave triggers **detach**.

---

## Where to start

| Doc | Purpose |
|-----|---------|
| [quick-test-checklist.md](./quick-test-checklist.md) | ~15 min smoke tests — commands and expected results |
| [implementation-notes.md](./implementation-notes.md) | Implementation details, phases, files, plugins |
| [../../architecture/world-scheduler/README.md](../../architecture/world-scheduler/README.md) | Full architecture reference (12 documents) |

---

## Minimum config for testing

In [`server.properties`](../../../server.properties):

```properties
world-scheduler-enabled=true
world-thread-count=4
world-scheduler-debug=true
additional-worlds=world_copy
```

Example worlds:

- `worlds/world/world.json` — `allowedWorkers: [0, 1]`
- `worlds/world_copy/world.json` — `allowedWorkers: [0, 1]`

---

## Automated tests

```bash
dotnet test tests/Basalt.Tests/Basalt.Tests.csproj
```

38 tests cover world registration, scheduler, sessions, cross-worker transfer, and observability (Phase 5).
