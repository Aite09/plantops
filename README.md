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
│   ├── PlantOps.Application/     Use cases (CQRS handlers arrive in Phase 2). Depends on Domain.
│   ├── PlantOps.Infrastructure/  EF Core DbContext, mappings, migrations, seed data. Depends on Application.
│   └── PlantOps.Api/             ASP.NET Core host and composition root.
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

   The OpenAPI document is at http://localhost:5297/openapi/v1.json.

`TrustServerCertificate=True` is only for the local container's self-signed certificate. Azure SQL uses a
trusted certificate, so deployed environments do not set it.

### Adding a migration

```bash
dotnet ef migrations add <Name> --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api --output-dir Persistence/Migrations
```
