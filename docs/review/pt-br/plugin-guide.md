# Guia de plugins — World Scheduler

Como escrever plugins que respeitam threads, mundos e sessões no Basalt com scheduler multi-worker.

[English version](../en-US/plugin-guide.md)

Fonte dos exemplos: [`samples/plugins/`](../../../samples/plugins/)

---

## Compilar e carregar

```bash
dotnet build samples/plugins/JoinAnnouncer/JoinAnnouncer.csproj
dotnet build samples/plugins/SpawnWelcome/SpawnWelcome.csproj
dotnet build samples/plugins/BreakGuard/BreakGuard.csproj
dotnet build samples/plugins/SchedulerSnapshot/SchedulerSnapshot.csproj
```

Cada build copia a DLL para `plugins/` na raiz do repo.

Em `server.properties`:

```properties
plugins-directory=plugins
```

Reinicie o servidor e use `/plugins` para listar os carregados.

---

## Ciclo de vida

| Método | Quando |
|--------|--------|
| `OnLoad` | DLL carregada (registro leve) |
| `OnStart` | Servidor iniciando — **registre eventos aqui** |
| `OnDisable` | Shutdown |

Helpers na classe base [`Plugin`](../../../Basalt/Plugins/Plugin.cs):

- `Listen<T>(ServerEvent, Action<T>)` — registrar handler
- `RunOnWorld(World, Action)` — executar no worker do mundo
- `TryGetPlayerWorld(Player, out World?)` — resolver mundo ativo
- `ForEachOnlineSession(Action<PlayerSession>)` — iterar sessões (somente leitura)
- `Log(string)` — log com prefixo `[Plugin:Nome]`

**Limitação atual:** não existe `Server.Off`; handlers permanecem até restart (aceitável nos samples).

---

## Evento → thread

| Evento | Thread | Pode mutar mundo? |
|--------|--------|-------------------|
| `ServerStart` | Global (start) | Não diretamente — use `RunOnWorld` |
| `PlayerJoin` | Global (login) | Não — player sem spawn |
| `PlayerSpawn` | Worker do mundo | Sim (via handler) |
| `PlayerBreakBlock` | Worker do mundo | Sim (inclui `Cancel()`) |
| `PlayerChat` | Worker do mundo | Leitura segura; broadcast com cuidado |
| `PlayerLeave` | Worker do mundo | Sim |
| `EntityHurt` / `EntityDie` | Worker do mundo | Sim |

`Server.Emit` despacha eventos world-bound no worker correto via `RunOnWorldThread` quando necessário.

---

## Quatro plugins de exemplo

### JoinAnnouncer — thread global

- **Arquivo:** `samples/plugins/JoinAnnouncer/`
- **Evento:** `PlayerJoin`
- **Faz:** log do username e thread; **não** acessa blocos/entidades
- **Aprenda:** login acontece antes do spawn; `player.Dimension` ainda é `null`

### SpawnWelcome — thread do worker (automática)

- **Arquivo:** `samples/plugins/SpawnWelcome/`
- **Evento:** `PlayerSpawn`
- **Faz:** mensagem ao jogador com nome do mundo e `AttachedWorkerId`
- **Aprenda:** handler já está no worker certo; leitura de `player.Dimension.World` é segura

### BreakGuard — cancelamento síncrono

- **Arquivo:** `samples/plugins/BreakGuard/`
- **Evento:** `PlayerBreakBlock`
- **Faz:** cancela quebra de bedrock ou Y&lt;1; mensagem ao jogador
- **Aprenda:** `signal.Cancel()` deve rodar no worker; o core reverte o bloco se cancelado

### SchedulerSnapshot — `RunOnWorldThread` explícito

- **Arquivo:** `samples/plugins/SchedulerSnapshot/`
- **Evento:** `ServerStart`
- **Faz:** log do modo scheduler; para cada mundo ativo (ou default), chama `RunOnWorld` e loga `PresentPlayerCount` + worker no thread do worker
- **Aprenda:** iterar `Server.Worlds` é global; inspecionar simulação exige `RunOnWorld`

---

## Regras para plugins

| Faça | Não faça |
|------|----------|
| Use `Server.Sessions` para identidade online | Use `Server.Players` (removido) |
| Resolva `session.ActiveEntity` a cada uso | Cache `Player` em campo estático |
| Mutar mundo dentro de handler world-bound | Chamar `Dimension.SetBlock` em `PlayerJoin` |
| Use `RunOnWorld` partindo de código global/async | Assumir que todo handler roda na mesma thread |
| Verifique `session.ActiveEntity` após transfer | Assumir entidade válida entre ticks |

---

## Esqueleto mínimo

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

Projeto: `ProjectReference` para `Basalt/Basalt.csproj`, copiar DLL para `plugins/` (ver `samples/plugins/Directory.Build.props`).

---

## Anti-patterns

1. **Guardar `Player` após join** — invalida após transfer cross-world
2. **Iterar mundos e chamar `world.Tick()`** — só workers tickam mundos ativos
3. **Mutar inventário em `PlayerJoin`** — use `PlayerSpawn` no worker ou `RunOnWorld`
4. **Assumir thread única** — com `world-scheduler-enabled=true`, há N workers

---

## Referências

- [implementation-notes.md](./implementation-notes.md)
- [quick-test-checklist.md](./quick-test-checklist.md) — passo opcional de plugins
- [11-agent-implementation-guide.md](../../architecture/world-scheduler/11-agent-implementation-guide.md)
