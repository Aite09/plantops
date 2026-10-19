# PlantOps

Equipment maintenance work-order tracker. Plant staff log equipment faults, supervisors approve them and
assign work orders to technicians, technicians work and close them, and every change is audited.

> Work in progress, built in phases. Full architecture notes, diagram and pattern map arrive in Phase 7.

## Tech stack

| Area | Choice |
| --- | --- |
| API | .NET 10 (LTS), ASP.NET Core Web API, C# |
| Data | Entity Framework Core 10, SQL Server 2022 (Docker locally, Azure SQL in the cloud) |
| Front end | Angular 22 (Phase 4) |

## Solution layout

```
PlantOps/
├── src/
│   ├── PlantOps.Domain/          Entities, WorkOrder aggregate, Result/Error. No dependencies.
│   ├── PlantOps.Application/     CQRS commands/queries, MediatR behaviors, validators. Depends on Domain.
│   ├── PlantOps.Infrastructure/  EF Core DbContext, repositories, read queries, migrations, seed data.
│   └── PlantOps.Api/             Controllers, ProblemDetails error handling, OpenAPI, composition root.
├── Directory.Build.props         Shared compiler settings for every project
├── Directory.Packages.props      Central NuGet versions
├── docker-compose.yml            Local SQL Server
└── global.json                   Pins the .NET SDK
```

Dependencies point inward: `Api → Application → Domain`, and `Infrastructure → Application`.
The Api project references Infrastructure only to register its services at startup.

## Getting started

Prerequisites: .NET 10 SDK, Docker Desktop (on Apple Silicon, keep "Use Rosetta for x86/amd64 emulation" on),
and the EF Core CLI (`dotnet tool install --global dotnet-ef`).

1. Create your local secrets file and choose a strong SQL Server password:

   ```bash
   cp .env.example .env
   ```

2. Start SQL Server and wait until it reports `healthy`:

   ```bash
   docker compose up -d sqlserver
   docker compose ps
   ```

3. Store the connection string in user-secrets (kept outside the repo, loaded only in Development):

   ```bash
   set -a; source .env; set +a
   dotnet user-secrets set "ConnectionStrings:PlantOps" "Server=localhost,${SQL_PORT};Database=PlantOps;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" --project src/PlantOps.Api
   ```

4. Create the database from the migrations:

   ```bash
   dotnet ef database update --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api
   ```

5. Run the API. In Development it also loads sample work orders on first start:

   ```bash
   dotnet run --project src/PlantOps.Api --launch-profile http
   ```

   Swagger UI is at http://localhost:5297/swagger (the raw OpenAPI document is at `/openapi/v1.json`).

### Trying the API in Swagger UI

Until Phase 3 adds real login, Development uses a temporary demo-user header. Click **Authorize** in Swagger UI
and enter one of `staff`, `supervisor`, `tech-a` or `tech-b`, then try a full lifecycle:

1. As `staff`: `GET /api/plant-units` to pick an equipment id, then `POST /api/work-orders`.
2. As `supervisor`: `POST /api/work-orders/{id}/approve`, then `/assign` with tech-a's id
   `3c9b7d52-0e41-4f8a-b6d3-0000000000c1`.
3. As `tech-a`: `POST /api/work-orders/{id}/start`, then `/close` with resolution notes.

Out-of-order steps return `409 Conflict`, the wrong technician gets `403 Forbidden`, and invalid input gets
`400` with per-field errors, all as RFC 7807 problem details.

`TrustServerCertificate=True` is only for the local container's self-signed certificate. Azure SQL uses a
trusted certificate, so deployed environments do not set it.

### Adding a migration

```bash
dotnet ef migrations add <Name> --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api --output-dir Persistence/Migrations
```

## API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/work-orders?status=&plantUnitId=&priority=&page=&pageSize=` | Paged list, highest priority and oldest first |
| GET | `/api/work-orders/{id}` | Work order details |
| POST | `/api/work-orders` | Report a fault (starts as Open) |
| PUT | `/api/work-orders/{id}` | Edit title, description, priority (not when Closed). Send back `version` from the GET; a stale version returns `409` |
| POST | `/api/work-orders/{id}/approve` | Open → Approved |
| POST | `/api/work-orders/{id}/assign` | Approved/Assigned/InProgress → Assigned (assign or hand over) |
| POST | `/api/work-orders/{id}/start` | Assigned → InProgress (assigned technician only) |
| POST | `/api/work-orders/{id}/close` | InProgress → Closed (assigned technician only) |
| GET | `/api/plant-units` | Plant units with their equipment |

Work orders are never deleted: they are maintenance history, and Closed is the end of the lifecycle.
