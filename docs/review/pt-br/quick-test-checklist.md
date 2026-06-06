# Checklist rápido de testes

Smoke tests diretos para validar o branch `world-scheduler`. Tempo estimado: **~15 minutos**.

Config de referência: [`server.properties`](../../../server.properties) com scheduler habilitado e `world_copy` carregado.

[English version](../en-US/quick-test-checklist.md)

---

## 1. Pré-requisitos

- [ ] `world-scheduler-enabled=true`
- [ ] `world-thread-count=4`
- [ ] `world-scheduler-debug=true`
- [ ] `additional-worlds=world_copy`
- [ ] Build: `dotnet build Basalt/Basalt.csproj`
- [ ] Servidor iniciado; cliente Bedrock conecta na porta configurada

---

## 2. Smoke básico (regressão geral)

| Passo | Ação | Esperado |
|-------|------|----------|
| Join | Conectar ao servidor | Spawn no overworld, sem crash |
| Movimento | Andar ~20 blocos | Chunks carregam, sem rubber-band |
| Bloco | Quebrar e colocar um bloco | Bloco some/aparece para todos |
| Chat | Enviar mensagem | Outros jogadores veem (se houver) |
| List | `/list` | Nome e contagem corretos |
| Disconnect | Sair | Mensagem de leave; sem erro no console |

---

## 3. Scheduler e métricas

| Passo | Comando | Esperado |
|-------|---------|----------|
| Métricas | `/worldscheduler` ou `/scheddebug` | Tabela com 4 workers; worker com `world` ativo mostra ≥1 world e ≥1 player |
| Debug log | (console) | Linhas `[Attach] world=world worker=N` após join |

Colunas alinhadas: `W`, `Wrlds`, `Plrs`, `TPS`, `WorkMs`, `LagMs`.

Workers ociosos: TPS ~20.0, WorkMs e LagMs ~0.

---

## 4. Transfer cross-world

| Passo | Comando | Esperado |
|-------|---------|----------|
| TP out | `/tp world_copy` | Cliente carrega mundo (sem "building terrain" infinito) |
| Métricas | `/worldscheduler` | `world_copy` aparece em algum worker (pode ser worker ≠ 0) |
| Debug | (console) | `[Transfer]` / `[Attach] world=world_copy` se mundo estava dormant |
| TP back | `/tp world` | Retorno ao mundo default funciona |

---

## 5. Estado per-world (inventário)

| Passo | Ação | Esperado |
|-------|------|----------|
| Setup | No `world`, `/give @s diamond 64` | Hotbar com diamantes |
| TP default | `/tp world_copy` (sem flags) | Inventário do **save de world_copy** (diamantes **não** vêm) |
| Carry | `/tp world` → `/give @s diamond 64` → `/tp world_copy --carry inventory` | Diamantes **carregados** para world_copy |
| Posição | `/tp world_copy --carry position` | Posição relativa mantida (se aplicável ao save) |

---

## 6. Regressão single-thread (opcional)

| Passo | Ação | Esperado |
|-------|------|----------|
| Config | `world-scheduler-enabled=false`, reiniciar | Servidor sobe normalmente |
| Smoke | Join, mover, bloco, `/list` | Comportamento Phase 1 (fila na tick thread) |
| Métricas | `/worldscheduler` | Uma linha (worker 0, single-thread mode) |

---

## 7. Automatizado

```bash
dotnet test tests/Basalt.Tests/Basalt.Tests.csproj
```

Todos os 38 testes devem passar.

Filtros úteis:

```bash
dotnet test --filter WorldSchedulerObservability
dotnet test --filter CrossWorkerTransfer
dotnet test --filter PlayerWorldTransfer
```

---

## 8. Plugins de exemplo (opcional)

| Passo | Ação | Esperado |
|-------|------|----------|
| Build | `dotnet build samples/plugins/JoinAnnouncer/JoinAnnouncer.csproj` (ou build de todos) | DLLs em `plugins/` |
| List | `/plugins` | JoinAnnouncer, SpawnWelcome, BreakGuard, SchedulerSnapshot |
| Join | Conectar | Log `[Plugin:JoinAnnouncer] join user=...` |
| Spawn | Após spawn | Mensagem `[SpawnWelcome] world=... worker=...` |
| Bedrock | Tentar quebrar bedrock (Y=0) | `[BreakGuard] This block is protected` |
| Start | Reiniciar servidor | Log `[Plugin:SchedulerSnapshot] server start ...` e snapshot do default world |

Detalhes: [plugin-guide.md](./plugin-guide.md).

---

## Problemas comuns

| Sintoma | Verificar |
|---------|-----------|
| "Building terrain" eterno após `/tp` | Logs de transfer; `ChangeDimensionAck`; chunk resync |
| Inventário errado após TP | Default usa save do destino; use `--carry inventory` |
| Mundo nunca detach | `PresentPlayerCount` e logs `[Detach]` |
| Comando sem permissão | `/op <nick>` no mundo correto (op é per-world no LevelDB) |

Mais detalhes: [implementation-notes.md](./implementation-notes.md).
