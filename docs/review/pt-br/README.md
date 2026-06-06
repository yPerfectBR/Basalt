# Review — Branch World Scheduler

Guia para quem conhece o **Basalt original** e quer validar o branch `world-scheduler` sem ler toda a documentação de arquitetura.

[English version](../en-US/README.md)

---

## O que mudou (2 minutos)

| Antes | Agora |
|-------|-------|
| Um tick thread para todos os mundos | Pool de workers (`world-thread-count`); cada mundo ativo roda em um worker |
| `Server.Players` (entidades por conexão) | `Server.Sessions` + `session.ActiveEntity` |
| Teleport só no mesmo processo/world | `/tp world_copy` entre mundos, inclusive workers diferentes |
| Inventário global do jogador | Estado **por mundo** (LevelDB de cada world); flags `--carry` no `/tp` |
| Sem métricas por worker | `/worldscheduler` (alias `/scheddebug`) |

Mundos **dormant** (zero jogadores) não são tickados. O primeiro jogador que entra dispara **attach**; o último a sair dispara **detach**.

---

## Por onde começar

| Doc | Para quê |
|-----|----------|
| [quick-test-checklist.md](./quick-test-checklist.md) | Smoke tests em ~15 min — comandos e resultados esperados |
| [plugin-guide.md](./plugin-guide.md) | Plugins, threads, 4 exemplos em `samples/plugins/` |
| [implementation-notes.md](./implementation-notes.md) | Detalhes de implementação, fases, arquivos |
| [../../architecture/world-scheduler/README.md](../../architecture/world-scheduler/README.md) | Referência completa de arquitetura (12 documentos) |

---

## Config mínima para testar

Em [`server.properties`](../../../server.properties):

```properties
world-scheduler-enabled=true
world-thread-count=4
world-scheduler-debug=true
additional-worlds=world_copy
```

Mundos de exemplo:

- `worlds/world/world.json` — `allowedWorkers: [0, 1]`
- `worlds/world_copy/world.json` — `allowedWorkers: [0, 1]`

---

## Testes automatizados

```bash
dotnet test tests/Basalt.Tests/Basalt.Tests.csproj
```

38 testes cobrem registro de mundos, scheduler, sessões, transfer cross-worker e observabilidade (Phase 5).
