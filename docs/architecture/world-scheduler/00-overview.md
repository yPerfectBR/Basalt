# 00 — Overview

## Problem

Basalt today runs all world simulation on a **single tick thread** while network packet handlers mutate world state from a **separate network thread**. There is no scheduling layer: every loaded world is ticked every frame, even when empty.

Key pain points:

1. **No workload isolation** — a heavy dungeon instance can stall the entire server TPS.
2. **No Skyblock scaling model** — hundreds of island worlds cannot each get their own thread, but active islands still compete with unrelated worlds on one loop.
3. **Mixed responsibilities in `Player`** — connection data, identity, and in-world entity state live in one class, complicating cross-world transfers and future worker boundaries.
4. **Implicit threading** — code assumes single-threaded simulation, but RakNet handlers, console, chunk workers, and the tick loop run concurrently without marshaling.

Relevant current code:

- Tick loop: `Basalt/Server.cs` — `Tick()` iterates all worlds sequentially.
- Player registry: `Basalt/Server.cs` — `Dictionary<NetworkConnection, Player>`.
- Network handlers: `Basalt/Network/Handlers/*.cs` — 15 handlers, most mutate player/world state directly.

## Target solution

Introduce a **World Scheduler** with a fixed **worker pool**:

- Server config sets `world-thread-count` (e.g. `6`).
- Each world is registered with a **profile** (`Light`, `Heavy`, `Hub`) and **allowed workers** (e.g. `[1, 2]`).
- When the **first player enters** a world, the scheduler **attaches** it to the **least loaded allowed worker**.
- When the **last player leaves**, the world is **detached** — it stops ticking on any worker.
- **Player sessions** remain at server level (`server.Sessions`) for plugins, chat, and console.
- **Player entities** live on the worker that owns their current world.

This is **not** one thread per world. It is a **fixed pool** with **profile-aware load balancing**.

## Goals

| Goal | Description |
|------|-------------|
| Workload isolation | Heavy worlds (dungeons) do not block light worlds (Skyblock islands) when on different workers |
| Skyblock-friendly | Many island worlds share light-profile workers; only worlds **with players present** are active |
| Plugin ergonomics | Central session registry — `GetOnlinePlayers()` does not require iterating every world |
| Configurable assignment | Per-world registration defines which workers may host that world |
| Incremental delivery | Phased implementation; feature flags preserve legacy behavior until ready |
| AI-implementable | Clear invariants, file map, tests per phase |

## Non-goals

| Non-goal | Reason |
|----------|--------|
| One thread per world | Does not scale for Skyblock; rejected by design |
| Per-world player registry | Plugins and global commands need server-level session list |
| Full plugin sandbox / separate API project | Related but out of scope for this scheduler doc |
| Native AOT-compatible dynamic plugins | Documented as constraint; not solved here |
| Automatic worker migration mid-session | Optional future work; not in initial phases |

## Use cases

### Skyblock (Light profile)

- Hundreds of island worlds exist on disk.
- Only islands with at least one online player are attached to a worker.
- Profile `Light`, `allowedWorkers: [1, 2]` — scheduler picks the freest among workers 1 and 2.
- Many active islands can coexist on one worker because each island is cheap.

### Dungeon / event (Heavy profile)

- Few concurrent instances (e.g. 4).
- Complex mechanics, many entities, heavy tick cost per world.
- Profile `Heavy`, `allowedWorkers: [2, 3]` — isolated from Skyblock workers.
- Even 4 worlds can saturate a worker; that is acceptable because other profiles are unaffected.

### Hub / spawn (Hub profile)

- Single always-active world when players are online.
- Profile `Hub`, `allowedWorkers: [0]` — pinned to worker 0.

## Glossary

| Term | Definition |
|------|------------|
| **Worker** | Dedicated simulation thread with a message queue and a set of attached active worlds |
| **WorldProfile** | Category describing expected tick cost: `Hub`, `Light`, `Heavy` |
| **AllowedWorkers** | Worker indices (0..N-1) that may host a given world |
| **ActiveWorld** | World currently attached to a worker because it has ≥1 player present |
| **PlayerSession** | Server-level, thread-safe representation of a connected client (xuid, skin, connection, sendPacket) |
| **WorldEntity** | In-world `Player` entity (position, inventory, traits); lives on one worker |
| **Attach** | Scheduler assigns an active world to a worker and begins ticking it |
| **Detach** | Last player left; world is saved and removed from worker tick loop |
| **PickWorker** | Algorithm choosing the least loaded worker from `allowedWorkers` |

## Document map

See [README.md](./README.md) for the full index and implementation order.

## Related documents

- Current baseline: [01-current-state.md](./01-current-state.md)
- Core invariants: [02-core-concepts.md](./02-core-concepts.md)
- Implementation phases: [08-phased-implementation.md](./08-phased-implementation.md)
