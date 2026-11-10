# Liwa University Smart Registration Assistant

A web app that builds each student a valid, graduation-optimised semester plan, lets them adjust it, and sends it to their academic advisor for approval. The full specification is in [docs/BRIEF.md](docs/BRIEF.md). Every assumption that still needs the university's confirmation is listed in [docs/assumptions.md](docs/assumptions.md).

**Status:** milestone **M5** (advisor portal) is done; M6 (admin), M7 (AI explainer) and M8 (docs, v0.1.0) are in progress. How plans are built: [docs/planner.md](docs/planner.md). See [CHANGELOG.md](CHANGELOG.md).

| Part | Technology |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core, EF Core 10 + PostgreSQL 18 (installed locally). Clean architecture: `Liwa.Domain`, `Liwa.Application`, `Liwa.Infrastructure`, `Liwa.Api` |
| Frontend | Angular 22 + Angular Material 22 (standalone components, signals, zoneless). English and Arabic (RTL) switch in the running app (Transloco) |
| Hosting | No Docker. The API and the Angular dev server run natively; a server install is described in [docs/INSTALL.md](docs/INSTALL.md) |

## Prerequisites (Windows)

Nothing is downloaded by the scripts. Install these yourself (or ask IT), then run the scripts.

| Tool | Version used | Where it is on this machine |
|---|---|---|
| Git | 2.55 | `%LOCALAPPDATA%\Programs\Git\cmd\git.exe` (not on PATH; the scripts add it) |
| .NET SDK | 10.0.403 | `C:\Program Files\dotnet` |
| Node.js + npm | 26.13 LTS / 11.20 | `C:\Program Files\nodejs` (the Angular CLI comes from the project's own `node_modules`, so no global install) |
| PostgreSQL | 18.6 | `C:\Program Files\PostgreSQL\18` (Windows service `postgresql-x64-18`, port 5432) |
| Python | 3.x | Only for `scripts/generate-demo-data.py` |

The scripts put these on PATH for their own process (`scripts/common.ps1`) and keep NuGet, npm and dotnet caches in the git-ignored `.local/` folder inside the repository.

**Smart App Control.** On this development machine, Windows Smart App Control sometimes blocks loading a freshly built, unsigned `Liwa.Api.dll` ("An Application Control policy has blocked this file"). It judges each build separately; when it happens, the API and the integration tests can't run until a rebuild is accepted, or until it is turned off or IT adds an exception for the repository folder. Unit tests and the Angular app are not affected. What still needs checking is listed in [docs/assumptions.md](docs/assumptions.md#pending-verification--smart-app-control). The projects build without a native `.exe` launcher (`UseAppHost=false`); run the API with `dotnet Liwa.Api.dll` or `dotnet run`.

## Database (first time)

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U postgres -f scripts/create-db.sql
```

This creates the `liwa` role (password `liwa`, with `CREATEDB` so the integration tests can create and drop their own databases) and the `liwa` database. It is safe to run again.

## Ports

| Port | What | Bound to |
|---|---|---|
| 5432 | Local PostgreSQL | localhost |
| 5080 | API dev server (`dotnet watch`). Swagger UI at `/api/swagger` | localhost |
| 4200 | Angular dev server (`ng serve`), proxies `/api` to 5080, so the app is same-origin | localhost |

## Run it

All commands are PowerShell, run from the repository root.

```powershell
Copy-Item .env.example .env      # first time only; scripts do this automatically if missing
.\scripts\run-dev.ps1            # API (5080) + web (4200), each in its own window
```

Open http://localhost:4200. On start, the API applies migrations and loads the demo data.

`.\scripts\run-dev.ps1 -NoWeb` starts only the API.

### Other scripts

| Script | What it does |
|---|---|
| `.\scripts\test.ps1` | Runs every test: .NET unit tests, .NET integration tests (each run creates a uniquely named database on the local PostgreSQL, migrates it and drops it afterwards) and Angular unit tests. `-UnitOnly` skips the integration tests |
| `.\scripts\reset-db.ps1` | Drops and recreates the local `liwa` database (local hosts only). Asks you to type `reset` first; `-Force` skips the prompt |
| `python scripts/generate-demo-data.py src/Liwa.Infrastructure/Seeding/Demo` | Regenerates the illustrative demo CSV files and checks them: each program adds up to 120 credits, the timetable follows UAE rules, and each demo student matches their story |

## Demo accounts

Demo accounts are created only when `DemoSeed:Enabled` is true. It is on in Development and off everywhere else by default. Every account uses the password **`Liwa#2027`**.

| Username | Role | Who |
|---|---|---|
| `student.new` | Student | New first-year |
| `student.probation` | Student | On academic probation |
| `student.honors` | Student | High GPA |
| `student.senior` | Student | Graduating senior, 6 credits left |
| `student.repeat` | Student | Failed a prerequisite |
| `advisor.it` | Advisor | Information Technology |
| `advisor.business` | Advisor | Business |
| `advisor.media` | Advisor | Mass Communication |
| `admin` | Admin | Registrar administrator |

Each demo student has a full academic record, loaded from the demo CSV files through the real import:

| Student | Program | Story |
|---|---|---|
| `student.new` | Information Technology | First term (Spring 2027), only an AP English credit |
| `student.probation` | Business (Management) | GPA 1.40, on academic probation |
| `student.honors` | Information Technology | GPA exactly 3.50 (the high-GPA boundary), with Fall 2026 grades not yet posted |
| `student.senior` | Information Technology | 114 of 120 credits: 6 left to graduate |
| `student.repeat` | Mass Communication | Failed MCM201, which MCM301 needs with at least a C |

Five more students (with no sign-in) give the advisors fuller lists. The data is illustrative: the three program names are real, but the courses, degree plans and timetable are invented until the Registrar supplies real data.

## Configuration

Script settings come from `.env`, which is git-ignored. [.env.example](.env.example) is the committed template. The API reads standard ASP.NET Core configuration (`appsettings.json`, then environment variables such as `Jwt__SigningKey`).

| Setting | Default | Meaning |
|---|---|---|
| `Jwt:SigningKey` | none (required) | HMAC-SHA256 key, at least 32 characters. The API refuses to start without one |
| `Jwt:LifetimeMinutes` | 480 | How long a sign-in lasts |
| `Security:PasswordHashing:Iterations` | 600000 | PBKDF2-HMAC-SHA256 iterations |
| `RateLimiting:SignInPermitsPerMinute` | 10 (200 in Development) | Sign-in attempts allowed per IP per minute |
| `DemoSeed:Enabled` | false (true in Development) | Creates the demo accounts |
| `Swagger:Enabled` | false (true in Development) | Swagger UI at `/api/swagger` |
| `ConnectionStrings:Liwa` | `localhost:5432`, `liwa`/`liwa` in Development | PostgreSQL connection (Npgsql format) |
| `Database:MigrateOnStartup` | false (true in Development) | Applies EF Core migrations on start |
| `Sso:Enabled` (`SSO_ENABLED`) | false | Shows "Sign in with your Liwa account" (Microsoft Entra ID, the same account as Blackboard). See [docs/sso.md](docs/sso.md) |
| `Sso:TenantId`, `Sso:ClientId`, `Sso:ClientSecret` | empty | From the university's Entra app registration. Required when SSO is on; the API refuses to start without them |
| `Sso:LocalSignInEnabled` (`SSO_LOCAL_SIGNIN_ENABLED`) | true | Keep username/password sign-in next to SSO (demo accounts, emergency admin access) |

### Sign in with your Liwa account

This is ready to use, but stays off until university IT registers the app. [docs/sso.md](docs/sso.md) is the step-by-step guide to hand to IT. To try it locally once you have the values:

```powershell
$env:Sso__Enabled='true'; $env:Sso__TenantId='<tenant id>'; $env:Sso__ClientId='<client id>'; $env:Sso__ClientSecret='<secret>'
.\scripts\run-dev.ps1
```

The redirect URI to register for local development is `http://localhost:4200/api/auth/sso/callback` (the dev server proxies it to the API).

## Project layout

```
src/
  Liwa.Domain/          entities and rules, no dependencies
  Liwa.Application/     use cases and interfaces (e.g. IAuthService, IPasswordHasher, ITokenIssuer)
  Liwa.Infrastructure/  EF Core, PostgreSQL, PBKDF2 hasher, JWT issuer, demo seeder
  Liwa.Api/             controllers, auth policies, problem-details errors, health checks, Swagger
tests/
  Liwa.UnitTests/         xUnit
  Liwa.IntegrationTests/  real API against a throwaway database on the local PostgreSQL
web/                    Angular app
scripts/                PowerShell helpers, create-db.sql, demo-data generator
docs/                   brief, assumptions, planner design, import contract, SSO guide, screenshots (later: PeopleSoft notes)
```

## API surface

| Endpoint | Access |
|---|---|
| `POST /api/auth/login` | Anyone (rate-limited) |
| `GET /api/auth/me` | Signed in |
| `GET /api/auth/sso/config` | Anyone (which sign-in options to show) |
| `GET /api/auth/sso/start` | Anyone (sends the browser to Microsoft sign-in; 404 while SSO is off) |
| `GET /api/auth/sso/callback`, `GET /api/auth/sso/complete` | Microsoft's redirect back; ends with a single-use code |
| `POST /api/auth/sso/exchange` | Anyone (rate-limited; trades the single-use code for a session) |
| `GET /api/health` | Anyone (liveness) |
| `GET /api/health/ready` | Anyone (readiness: database reachable) |
| `GET /api/admin/system` | Admin only (version, environment, database status) |
| `GET /api/admin/overview` | Admin only (how much data is loaded, last upload) |
| `POST /api/admin/imports/{kind}?dryRun=true` | Admin only. Uploads one CSV file (`file` form field). Checks only, unless `dryRun=false`. Kinds: terms, courses, programs, degree-plans, sections, students, transcripts. Format: [docs/import-contract.md](docs/import-contract.md) |
| `GET /api/students/me/summary` | Student only (own program, credits, GPA, advisor) |
| `POST /api/students/me/planning` | Student only. Everything the plan screen needs: the recommendation, course titles (English and Arabic), class times and the saved plan. Same optional body as `recommendation` |
| `POST /api/students/me/plan/check` | Student only. Live check of hand-picked sections (`sectionIds`, optional `availability`): errors that block saving and warnings to confirm |
| `POST /api/students/me/plan` | Student only. Saves a draft (`submit: false`) or sends to the advisor (`submit: true`, with every warning's key in `acknowledgedWarnings`). Every save is a new version, audited; sending notifies the advisor |
| `POST /api/students/me/recommendation` | Student only. The planner's recommendation for the current term. Optional body: `availability` (free-time windows per day, e.g. `{"day":"Monday","start":"17:00","end":"20:00"}`), `maxCredits`, `delivery` (`Any`, `OnCampusOnly`, `OnlineOnly`). See [docs/planner.md](docs/planner.md) |
| `GET /api/advisor/students` | Advisor only (own students) |

Every endpoint requires sign-in unless it is explicitly marked anonymous. Errors are returned as RFC 7807 problem details with a `traceId`.
