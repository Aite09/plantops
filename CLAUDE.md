# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

PlantOps is an equipment-maintenance work-order tracker: an ASP.NET Core 10 Web API (Clean Architecture, CQRS with
MediatR, EF Core 10 on SQL Server) and an Angular 22 SPA in `web/`. Staff report faults, supervisors approve and assign,
technicians start and close; every change is audited. It is a portfolio project: `README.md` is the full reference
(architecture diagram, pattern map, security model, API table, design Q&A) and `PRODUCT.md` holds the UI design rules.
It is not deployed; `infra/` is deploy-ready Azure Bicep (see "Deployment" below).

Project rule: free/OSS dependencies and free-tier services only. Flag anything that could bill before adding it.
This is also why FluentAssertions is pinned to 7.x (8.x is commercially licensed).

## Setup

- .NET SDK 10.0.402 (pinned in `global.json`, which also opts tests into Microsoft Testing Platform), Node 24
  (`.node-version`), Docker (local SQL Server and the integration tests).
- Local secrets live in user-secrets for `src/PlantOps.Api`: `ConnectionStrings:PlantOps`, `Jwt:SigningKey`,
  `Seed:DemoUserPassword`. Docker Compose reads the git-ignored `.env` (template: `.env.example`).
- `dotnet tool restore` installs the pinned tools (`dotnet-ef`, ReportGenerator) from `.config/dotnet-tools.json`.

## Commands

Backend (repo root):

```bash
docker compose up -d sqlserver                                   # local SQL Server only
dotnet run --project src/PlantOps.Api --launch-profile http      # http://localhost:5297, Swagger UI at /swagger
dotnet build                                                     # warnings are errors (Directory.Build.props)
dotnet test                                                      # all four test projects; Docker must be running
dotnet test --project tests/PlantOps.Domain.UnitTests --filter-class "*ErrorTests"
dotnet test --project tests/PlantOps.Api.IntegrationTests --filter-method "*Some_test_method_name"
```

In Development the API applies both migration sets and seeds demo users and work orders on startup. Tests run on
Microsoft Testing Platform (xUnit v3), so `dotnet test` takes MTP options such as `--filter-class`, `--filter-method`
and `--filter-namespace` (run `dotnet test --project <test project> --help` for the full list).

Migrations: there are two DbContexts with separate migration sets.

```bash
dotnet ef migrations add <Name> --context PlantOpsDbContext --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api --output-dir Persistence/Migrations
dotnet ef migrations add <Name> --context AuthDbContext --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api --output-dir Identity/Migrations
```

Coverage, as CI runs it (CI fails below 80% line coverage):

```bash
dotnet test --results-directory TestResults --coverlet --coverlet-output-format cobertura --coverlet-include "[PlantOps.*]*" --coverlet-exclude-by-file "**/Migrations/*.cs"
dotnet reportgenerator -reports:"TestResults/**/*.cobertura*.xml" -targetdir:coverage-report -reporttypes:"Html;TextSummary"
```

Front end (`web/`):

```bash
npm ci
npm start                                                        # ng serve on http://localhost:4200
npx ng test --watch=false                                        # Vitest
npx ng test --watch=false --include src/app/core/app-config.spec.ts
npx ng build --configuration production                          # bundle budgets are enforced
```

There is no ESLint. `web/.prettierrc` exists but is not enforced and much of the code predates it: format only the
lines you change, never mass-reformat files.

Whole stack in containers: `docker compose up -d --build` (web on :8081, API on :8080).

## Architecture

Layers, with dependencies pointing inward (enforced by `tests/PlantOps.ArchitectureTests`):

- `PlantOps.Domain`: entities and the `WorkOrder` aggregate. All state changes are methods (`Approve`, `Assign`,
  `Start`, `Close`) checked against one `AllowedTransitions` table; private setters; `Result`/`Error` types live here.
- `PlantOps.Application`: commands and queries (MediatR `ICommand`/`IQuery`) with `internal sealed` handlers, FluentValidation
  validators, pipeline behaviors (`LoggingBehavior`, `ValidationBehavior`), and ports in `Abstractions/` (persistence,
  authentication, AI). No EF Core or ASP.NET Core references.
- `PlantOps.Infrastructure`: EF Core (`PlantOpsDbContext`), repositories and unit of work, read-side queries, the
  `AuditingInterceptor`, ASP.NET Core Identity in its own `AuthDbContext` (schema `auth`, own migrations history), JWT
  issuing, and the AI adapter.
- `PlantOps.Api`: thin controllers and the composition root (`Program.cs`). It references Infrastructure only to
  register services; controllers may only use `ISender`.

A write request: controller → `ISender.Send(command)` → `LoggingBehavior` → `ValidationBehavior` (returns a `Result`
failure, does not throw) → handler → aggregate method returns `Result` → repository + `IUnitOfWork.SaveChangesAsync` →
`AuditingInterceptor` writes the audit row in the same transaction. `ApiControllerBase.Problem(Error)` maps `ErrorType`
to 400/401/403/404/409/503 as RFC 7807 ProblemDetails with a `code` extension. Expected business failures are
`Result`s, not exceptions; `GlobalExceptionHandler` covers the rest.

Reads skip the repositories: `IWorkOrderQueries` and `IUserDirectory` (Infrastructure) use `AsNoTracking` projections
straight into DTOs. Work-order updates use optimistic concurrency (`RowVersion`, sent back as `version`; stale → 409).

Cross-cutting rules that span several files:

- Time comes from the injected `TimeProvider` (`FakeTimeProvider` in tests), never `DateTime.UtcNow`.
- Configuration uses the Options pattern; `JwtOptions`, `RateLimitingSettings` and `AiOptions` are validated at
  startup (`ValidateOnStart`), so a bad setting stops the API instead of failing later.
- Authorization: a fallback policy requires authentication everywhere; role policies are in
  `Api/Authorization/Policies.cs`; rules about a specific resource (for example "only the assigned technician may start
  work") live in handlers via `ICurrentUser`. The Angular `availableActions()` only mirrors these rules to hide buttons.
- Middleware order in `Program.cs` is load-bearing: CORS → authentication → authorization → rate limiter → output
  cache. The dashboard (`GET /api/dashboard`) is output-cached and shared by every Supervisor/Admin through
  `Api/Caching/SharedResponseCachePolicy` (the built-in default policy is deliberately excluded), so
  `GetDashboardQueryHandler` must never depend on `ICurrentUser` (an architecture test checks this). Controllers whose
  successful writes change dashboard data carry `[EvictDashboardCache]`.
- Health: `/health/live` has no dependencies (what platforms should probe); `/health` checks the database and is
  rate-limited.
- "Explain this fault" is optional and off unless `Ai:Enabled` is true: `IFaultExplainer` port,
  `ChatCompletionsFaultExplainer` typed `HttpClient` for any OpenAI-compatible endpoint (default: Ollama `phi4-mini` on
  localhost). The reporter's text is untrusted: see `FaultExplanationPrompt` before changing the prompt.
- One-off maintenance commands run instead of the web host: `dotnet run --project src/PlantOps.Api -- seed-demo` and
  `-- create-admin` (see `DatabaseInitializer`).

Front end (`web/src/app`): standalone components, signals, zoneless change detection, lazy routes in `app.routes.ts`.
`core/` holds the auth store, interceptor and guards, the runtime config (`config.json` is fetched before bootstrap:
`public/config.json` in development, generated by nginx in Docker), ProblemDetails helpers and the layout shell.
`features/<area>/` pairs smart `*-page.ts` components (inject services, own state) with presentational ones (inputs and
outputs only). UI changes must follow `PRODUCT.md`: neutral by default, colour only for attention, tables over card
grids, WCAG 2.2 AA.

## Tests

- Integration tests share one `PlantOpsApiFactory` per run (`[Collection(ApiCollection.Name)]`): a Testcontainers SQL
  Server, both migration sets applied, `TestUsers` created once, and Respawn clearing `WorkOrders` and `AuditEntries`
  before each test. Use `WorkOrderSteps` to drive work orders through the API, `FakeClockApi` for time-dependent cases,
  and `factory.WithWebHostBuilder(b => b.UseSetting(...))` for tests that need different configuration.
- Application handler tests use Moq and `FakeTimeProvider`. Architecture rules are tests, so a layering violation
  fails CI like any other test.

## CI and deployment

`.github/workflows/ci.yml` runs three checks on every push and pull request (backend with the coverage gate and a
vulnerable-package check, frontend with `npm audit`, Docker image builds); actions are pinned to commit SHAs. The two
Azure deploy jobs are skipped unless the repository variable `AZURE_DEPLOY` is `true`. `infra/` holds the Bicep, the
Azure Policy guardrails that allow only free tiers, and the one-time setup runbook (`infra/README.md`); keep every SKU
on its free tier.
