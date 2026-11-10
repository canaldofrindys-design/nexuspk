# NexusPK — Portal Priston Tale

Portal web do servidor NexusPK: site público, painel do jogador e painel administrativo,
integrado ao banco original do Priston Tale em modo somente leitura.

| Parte | Tecnologia | Pasta |
| --- | --- | --- |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4, Framer Motion, Lucide | `frontend/` |
| API | ASP.NET Core 10, Dapper, Microsoft.Data.SqlClient | `backend/src/Priston.Api` |
| Integração com o jogo | Camada isolada, só leitura por padrão | `backend/src/Priston.GameDatabaseIntegration` |
| Tarefas agendadas | Serviço Windows + Quartz.NET | `backend/src/Priston.Worker` |
| Migrações do banco do portal | DbUp, scripts versionados | `database/migrations`, `backend/src/Priston.DbMigrator` |

Documentação: [`docs/`](docs/). O mapeamento de cada funcionalidade para as tabelas reais
está em [`docs/DATABASE_MAPPING.md`](docs/DATABASE_MAPPING.md).

## Desenvolvimento

Pré-requisitos: .NET SDK 10, Node.js 22 LTS, SQL Server acessível (pode ser uma cópia do banco do jogo).

```bash
# Backend
cd backend
dotnet build
dotnet test

# Connection strings: sempre por variável de ambiente ou user-secrets, nunca no appsettings versionado.
export PortalDatabase__ConnectionString="Server=...;Database=PortalDB;..."
export GameDatabase__ReadConnectionString="Server=...;Database=accountdb;..."
dotnet run --project src/Priston.Api --urls http://127.0.0.1:5080

# Migrações do banco do portal (login de deploy)
dotnet run --project src/Priston.DbMigrator -- --create-database \
  --ConnectionStrings:PortalDbDeploy="Server=...;Database=PortalDB;..."

# Frontend
cd ../frontend
cp .env.example .env.local
npm install
npm run dev
```

Endpoints da Fase 3:

- `GET /health/live` — processo no ar (público).
- `GET /health/ready` — estado dos dois bancos (só de localhost).
- `GET /api/v1/system/info` — versão e fuso horário.

## Regras do projeto

- O banco original do jogo **não é alterado** sem autorização. A API só lê; a escrita
  (cadastro e troca de senha) usará um login SQL separado, com permissão apenas nessas tabelas.
- Nenhum nome de tabela ou coluna é inventado: tudo vem do `DATABASE_MAPPING.md`.
- Nenhum dado simulado: status, online e rankings mostram só o que vier de fontes reais.
