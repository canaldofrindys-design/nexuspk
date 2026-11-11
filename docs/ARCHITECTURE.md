# Arquitetura

O documento completo da Fase 0 (arquitetura, fases, infraestrutura, contas e segurança) foi
aprovado no projeto. Este arquivo resume o que está implementado no código.

## Fluxo

Navegador → IIS (HTTPS) → Next.js (módulo ASP.NET Core do IIS, `127.0.0.1`) → API ASP.NET Core (serviço Windows, `127.0.0.1:5080`).
O navegador nunca fala com a API: páginas, formulários e anexos passam pelo Next. Detalhes em `INSTALLATION.md`.
A API e o Worker são os únicos que acessam bancos:

- **Banco do portal** (`PortalDB`): `Priston.Infrastructure`, login de aplicação sem DDL.
- **Banco do jogo** (`accountdb`, `ServerDB`, `UserDB`, `ClanDB`, `GameLogDB`): `Priston.GameDatabaseIntegration`, login somente leitura.

## Projetos .NET

| Projeto | Responsabilidade | Depende de |
| --- | --- | --- |
| Priston.Domain | Regras puras (classes, períodos de ranking em America/Sao_Paulo) | — |
| Priston.Application | Casos de uso e contratos internos | Domain |
| Priston.Infrastructure | Banco do portal, relógio, health check | Application, Domain |
| Priston.GameDatabaseIntegration | Acesso ao banco do jogo, resolução de shards `<letra>GameUser`, health check | Application |
| Priston.Contracts | DTOs compartilhados entre API e Worker | — |
| Priston.Api | Endpoints REST, health, segurança HTTP | todos acima |
| Priston.Worker | Serviço Windows, jobs Quartz.NET | Infrastructure, GameDatabaseIntegration |
| Priston.DbMigrator | Aplica `database/migrations` no banco do portal | — |

## Decisões

- Ranking v1 por nível (`ServerDB.dbo.LevelTable`). EXP e online dependem de o servidor do jogo
  passar a gravá-los no SQL (decisão de 10/11/2026).
- Senha do jogo em texto puro no banco original (`<letra>GameUser.Passwd`): o portal compara em
  tempo constante e nunca registra o valor.
- Quartz.NET 3.22 (API estável e documentada); migrar para 4.x é uma tarefa separada.
