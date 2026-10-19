# PlantOps

Equipment maintenance work-order tracker. Plant staff log equipment faults, supervisors approve them and
assign work orders to technicians, technicians work and close them, and every change to a work order is audited.

> Work in progress, built in phases. Full architecture notes, diagram and pattern map arrive in Phase 7.

## Tech stack

| Area | Choice |
| --- | --- |
| API | .NET 10 (LTS), ASP.NET Core Web API, C# |
| Data | Entity Framework Core 10, SQL Server 2022 (Docker locally, Azure SQL in the cloud) |
| Auth | ASP.NET Core Identity, JWT bearer tokens, role-based policies |
| Front end | Angular 22 (standalone components, signals, zoneless), Angular Material 3, RxJS, typed reactive forms |

## Solution layout

```
PlantOps/
├── src/
│   ├── PlantOps.Domain/          Entities, WorkOrder aggregate, Result/Error. No dependencies.
│   ├── PlantOps.Application/     CQRS commands/queries, MediatR behaviors, validators. Depends on Domain.
│   ├── PlantOps.Infrastructure/  EF Core, repositories, read queries, audit interceptor, migrations, seed data.
│   │   └── Identity/             ASP.NET Core Identity, JWT issuing, user directory (own DbContext, "auth" schema).
│   └── PlantOps.Api/             Controllers, ProblemDetails error handling, OpenAPI, composition root.
├── web/                          Angular front end (see "Front end" below).
├── PRODUCT.md                    Who the UI is for and the design principles it follows.
├── .node-version                 Node 24 for this repo (read by fnm and by CI).
├── Directory.Build.props         Shared compiler settings for every project
├── Directory.Packages.props      Central NuGet versions
├── docker-compose.yml            Local SQL Server
└── global.json                   Pins the .NET SDK
```

Dependencies point inward: `Api → Application → Domain`, and `Infrastructure → Application`.
The Api project references Infrastructure only to register its services at startup.

## Getting started

Prerequisites: .NET 10 SDK, Docker Desktop (on Apple Silicon, keep "Use Rosetta for x86/amd64 emulation" on),
the EF Core CLI (`dotnet tool install --global dotnet-ef`), and Node 24 for the front end. With
[fnm](https://github.com/Schniz/fnm), `fnm install` in this folder picks the version from `.node-version`.

1. Create your local secrets file and choose a strong SQL Server password:

   ```bash
   cp .env.example .env
   ```

2. Start SQL Server and wait until it reports `healthy`:

   ```bash
   docker compose up -d sqlserver
   docker compose ps
   ```

3. Store the secrets in user-secrets (kept outside the repo, loaded only in Development): the connection string,
   the key that signs JWTs, and the password the demo accounts will share (10+ characters with upper and lower
   case, a digit and a symbol):

   ```bash
   set -a; source .env; set +a
   dotnet user-secrets set "ConnectionStrings:PlantOps" "Server=localhost,${SQL_PORT};Database=PlantOps;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" --project src/PlantOps.Api
   dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/PlantOps.Api
   dotnet user-secrets set "Seed:DemoUserPassword" "<your demo password>" --project src/PlantOps.Api
   ```

   Forgot the demo password? `dotnet user-secrets list --project src/PlantOps.Api` shows it.

4. Create the database. There are two DbContexts, business data and Identity, so there are two migration sets:

   ```bash
   dotnet ef database update --context AuthDbContext --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api
   dotnet ef database update --context PlantOpsDbContext --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api
   ```

5. Run the API. In Development it also applies migrations and creates the demo accounts and sample work orders:

   ```bash
   dotnet run --project src/PlantOps.Api --launch-profile http
   ```

   Swagger UI is at http://localhost:5297/swagger (the raw OpenAPI document is at `/openapi/v1.json`).

6. Run the web app in a second terminal, then open http://localhost:4200 and sign in with a demo account:

   ```bash
   cd web
   npm install
   npm start
   ```

### Trying the API in Swagger UI

Demo accounts (Development only), all using your `Seed:DemoUserPassword`:

| Email | Name | Role |
| --- | --- | --- |
| `staff@plantops.test` | Sam Staff | Staff |
| `supervisor@plantops.test` | Pat Supervisor | Supervisor |
| `tech.a@plantops.test` | Alex Technician | Technician |
| `tech.b@plantops.test` | Blair Technician | Technician |
| `admin@plantops.test` | Ada Admin | Admin |

To act as someone: call `POST /api/auth/login`, copy `accessToken` from the response, click **Authorize** and
paste it. Then try a full lifecycle:

1. As staff: `GET /api/plant-units` to pick an equipment id, then `POST /api/work-orders`.
2. As supervisor: `POST /api/work-orders/{id}/approve`, then `GET /api/users?role=Technician` and
   `POST /api/work-orders/{id}/assign` with a technician's id.
3. As that technician: `POST /api/work-orders/{id}/start`, then `/close` with resolution notes.
4. As anyone: `GET /api/work-orders/{id}/history` shows every change, who made it and when.

Out-of-order steps return `409 Conflict`, a missing permission or the wrong technician gets `403 Forbidden`, and
invalid input gets `400` with per-field errors, all as RFC 7807 problem details.

`TrustServerCertificate=True` is only for the local container's self-signed certificate. Azure SQL uses a
trusted certificate, so deployed environments do not set it.

### Adding a migration

```bash
dotnet ef migrations add <Name> --context PlantOpsDbContext --project src/PlantOps.Infrastructure --startup-project src/PlantOps.Api --output-dir Persistence/Migrations
```

For Identity schema changes use `--context AuthDbContext --output-dir Identity/Migrations`.

## Front end

Angular 22 with standalone components, signals and zoneless change detection, Angular Material 3, RxJS and typed
reactive forms. Design intent and principles are in [PRODUCT.md](PRODUCT.md): calm and neutral by default, with
colour reserved for Critical/High priority, ageing work and errors.

```
web/src/app/
├── core/
│   ├── auth/            AuthStore (signals), JWT interceptor, route guards
│   ├── layout/          Shell: top bar, navigation, user menu
│   └── problem-details  RFC 7807 helpers, incl. mapping API field errors onto form controls
├── shared/              Priority and status badges, time helpers
└── features/
    ├── login/
    ├── work-orders/     list (filters, table), detail (action bar, facts, history), form (create, edit)
    └── users/           Admin-only account management
```

- **Smart and presentational components**: pages (`*-page.ts`) inject services, load data and handle events;
  tables, forms, badges and the action bar only take inputs and emit outputs.
- **State**: signals for UI state, RxJS for streams. The list's filters live in the URL query string (shareable,
  back button works), and `switchMap` cancels stale requests when filters change quickly.
- **Auth**: the JWT is kept in `sessionStorage`: per tab and survives a refresh, but browsers restore it when a
  closed tab is reopened, so on shared PCs people use Sign out (the token itself expires within 60 minutes). Expiry
  is timed from the login response's `expiresInSeconds` on the device's own clock. The interceptor adds the token
  only to requests for the PlantOps API, and signs the user out on a `401`. An HttpOnly cookie set by a
  backend-for-frontend would protect the token from XSS better; that is the next step if the threat model needs it.
- **Guards**: `authGuard`, `roleGuard('Admin')` and `guestGuard` decide navigation. They are convenience only;
  the API enforces every rule, and `availableActions()` mirrors those rules just to hide buttons that would fail.
- **Every view handles loading, error and empty states**: skeletons while loading, a retry on errors, and empty
  states that say what to do next.
- **Accessibility**: WCAG 2.2 AA target; axe-core reports no violations on any screen. Status and priority are
  never colour alone, focus is always visible, and there is a skip link.

## Security model

CORS: only the origins in the `Cors:AllowedOrigins` setting may call the API from a browser (in Development,
`http://localhost:4200`), with only the methods and headers the web app uses.

Authentication: `POST /api/auth/login` checks the password with ASP.NET Core Identity and returns a JWT signed with
HMAC-SHA256, valid for 60 minutes. Every other endpoint requires a valid token (a fallback policy, so new
endpoints are protected by default). Five wrong passwords lock an account for 15 minutes, and login is
rate-limited per IP. Wrong email, wrong password and a locked account all return the same `401`, and an unknown
email still runs a password-hash check, so neither the response nor its timing reveals which accounts exist.

Authorization happens at two levels:

| Rule | Where | How |
| --- | --- | --- |
| Who may approve/assign, start/close, manage users | API | Named policies (`Policies.cs`) mapped to roles |
| Only the assigned technician may start/close | Domain | `WorkOrder.Start` / `Close` |
| Reporters may edit their own report, and only while it is Open | Application | `UpdateWorkOrderCommandHandler` |
| Assignee must be a technician | Application | `AssignWorkOrderCommandHandler` via `IUserDirectory` |

| Action | Staff | Supervisor | Technician | Admin |
| --- | :---: | :---: | :---: | :---: |
| View work orders and history | ✓ | ✓ | ✓ | ✓ |
| Report a fault | ✓ | ✓ | ✓ | ✓ |
| Edit a work order | own, while Open | ✓ | own, while Open | ✓ |
| Approve, assign | | ✓ | | ✓ |
| Start, close | | | assigned only | |
| List users | | ✓ | | ✓ |
| Create users | | | | ✓ |

Audit log: an EF Core `SaveChangesInterceptor` on the business `PlantOpsDbContext` writes an `AuditEntries` row
for every created, updated or deleted work order, plant unit or piece of equipment, with old and new values, the
user and the time, in the same transaction as the change. Accounts live in the separate Identity store, which the
interceptor deliberately never sees (no password hashes in the audit table); creating an account is instead
recorded as a structured security log event with the acting admin's id.

### Swapping to Microsoft Entra ID

Identity-specific code sits behind `AddIdentityAuthentication(...)` and the `IAccountService` / `IUserDirectory`
ports, and policies use role names that Entra app roles can match. The move is contained but not free:

1. Replace `AddIdentityAuthentication(...)` with Microsoft.Identity.Web's `AddMicrosoftIdentityWebApi(...)` and
   create Entra app roles named Staff, Supervisor, Technician and Admin.
2. Read the user id from Entra's `oid` claim in `CurrentUser`.
3. Implement `IUserDirectory` over Microsoft Graph.
4. Remove what Entra now owns: the login and create-user endpoints, their commands, `IAccountService`,
   `Infrastructure/Identity` and the demo-user seeding.
5. Run a one-off data migration: work orders and audit rows store Identity user ids, and Entra assigns its own
   object ids, so map each old id to the matching Entra `oid` (by email). Storing ids rather than foreign keys
   into the Identity tables keeps this to an `UPDATE`, not a schema change.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/auth/login` | Email and password in, JWT access token out (anonymous, rate-limited) |
| GET | `/api/users?role=` | List users, e.g. technicians to assign (Supervisor, Admin) |
| POST | `/api/users` | Create an account with a role (Admin) |
| GET | `/api/work-orders?status=&activeOnly=&plantUnitId=&priority=&assignedToUserId=&page=&pageSize=` | Paged list, highest priority and oldest first |
| GET | `/api/work-orders/{id}` | Work order details, with user names |
| GET | `/api/work-orders/{id}/history` | Audit trail: every change, who and when |
| POST | `/api/work-orders` | Report a fault (starts as Open) |
| PUT | `/api/work-orders/{id}` | Edit title, description, priority (not when Closed; see rules above). Send back `version` from the GET; a stale version returns `409` |
| POST | `/api/work-orders/{id}/approve` | Open → Approved |
| POST | `/api/work-orders/{id}/assign` | Approved/Assigned/InProgress → Assigned (assign or hand over) |
| POST | `/api/work-orders/{id}/start` | Assigned → InProgress (assigned technician only) |
| POST | `/api/work-orders/{id}/close` | InProgress → Closed (assigned technician only) |
| GET | `/api/plant-units` | Plant units with their equipment |

Work orders are never deleted: they are maintenance history, and Closed is the end of the lifecycle.
