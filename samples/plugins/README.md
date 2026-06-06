# Sample plugins

Example plugins for the Basalt world scheduler. Each project produces one DLL copied to `plugins/` on build.

## Build

```bash
dotnet build samples/plugins/JoinAnnouncer/JoinAnnouncer.csproj
dotnet build samples/plugins/SpawnWelcome/SpawnWelcome.csproj
dotnet build samples/plugins/BreakGuard/BreakGuard.csproj
dotnet build samples/plugins/SchedulerSnapshot/SchedulerSnapshot.csproj
```

Or build all from the solution:

```bash
dotnet build Basalt.sln
```

Ensure `server.properties` has `plugins-directory=plugins`.

## IDE / autocomplete (Cursor / VS Code)

Open the **repo root** (`Basalt-1/`), not an isolated file. The language server uses [`Basalt.sln`](../../Basalt.sln), which includes all sample plugin projects.

1. Install **C#** + **C# Dev Kit** extensions (see [`.vscode/extensions.json`](../../.vscode/extensions.json)).
2. Reload window after first open (`Developer: Reload Window`).
3. Run once: `dotnet restore Basalt.sln`
4. In the status bar, confirm the solution is **Basalt.sln** and the active project for plugin files is e.g. **BreakGuard**.

If IntelliSense is still empty, run **`.NET: Restore All Projects`** from the command palette.

## Plugins

| Plugin | Event | Thread lesson |
|--------|-------|---------------|
| JoinAnnouncer | `PlayerJoin` | Global / login thread |
| SpawnWelcome | `PlayerSpawn` | World worker via `Emit` |
| BreakGuard | `PlayerBreakBlock` | Cancel on worker |
| SchedulerSnapshot | `ServerStart` | `RunOnWorldThread` from global code |

See [docs/review/en-US/plugin-guide.md](../../docs/review/en-US/plugin-guide.md).
