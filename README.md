# PurviewOps

PurviewOps is a multi-tenant Microsoft 365 admin platform for approved Purview/SCC/Exchange Online operations using a constrained, typed, PowerShell-orchestrated backend worker.

## Repo layout

- `frontend` - Next.js TypeScript App Router admin UI
- `backend` - ASP.NET Core Web API with typed operation contracts and async job APIs
- `worker` - .NET worker that executes allow-listed PowerShell adapters only
- `infra` - Azure Bicep and Container Apps deployment assets
- `docs` - architecture, auth, operations, deployment, security, limitations
- `.github/workflows` - CI/CD workflow

## Quick start

```bash
# API
cd backend
DOTNET_ENVIRONMENT=Development dotnet run --project src/PurviewOps.Api.csproj

# Worker
cd ../worker
DOTNET_ENVIRONMENT=Development dotnet run --project src/PurviewOps.Worker.csproj

# Frontend
cd ../frontend
npm install
npm run dev
```

## Design principles

- No arbitrary PowerShell execution
- Typed allow-list adapters only
- Delegated-first auth model with tenant context preservation
- Queue-backed async execution with correlation IDs
- Deterministic JSON output and normalized response envelopes
