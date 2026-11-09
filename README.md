# Liwa University Smart Registration Assistant

A web app that builds each student a valid, graduation-optimised semester plan, lets them adjust it, and sends it to their academic advisor for approval. The full specification is in [docs/BRIEF.md](docs/BRIEF.md). Every assumption that still needs the university's confirmation is listed in [docs/assumptions.md](docs/assumptions.md).

**Status:** milestone **M2** (data model, database, demo data, CSV import). See [CHANGELOG.md](CHANGELOG.md).

| Part | Technology |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core, EF Core 10 + PostgreSQL 17. Clean architecture: `Liwa.Domain`, `Liwa.Application`, `Liwa.Infrastructure`, `Liwa.Api` |
| Frontend | Angular 22 + Angular Material 22 (standalone components, signals, zoneless). English and Arabic (RTL) switch in the running app (Transloco) |
| Hosting | Docker Compose: `db`, `api`, `web` (nginx serves the app and proxies `/api`, so everything is same-origin) |

## Prerequisites (Windows)

| Tool | Version used | Notes |
|---|---|---|
| Git | 2.55 | |
| .NET SDK | 10.0.302 | `dotnet --version` |
| Node.js + npm | 26.13 LTS / 11.20 | |
| Angular CLI | 22.2 | `npm install -g @angular/cli@latest` |
| Docker | Engine 29.9, Compose 5.7 | Docker Desktop **or** Docker Engine inside WSL (see below) |

**Docker on this development machine.** Docker Desktop needs admin rights, so Docker Engine runs inside the WSL Ubuntu distro instead. The Windows `docker` CLI lives in `%USERPROFILE%\.docker\bin` and talks to it through `DOCKER_HOST=tcp://127.0.0.1:2375` (loopback only). `%USERPROFILE%\.wslconfig` turns on mirrored networking and turns off the idle timeout; without them, the engine stops after each command and container output never reaches PowerShell. The scripts start the engine for you if it is down. Only containers run in WSL. The code, the API and the Angular dev server run natively on Windows. On a machine with Docker Desktop, nothing changes: unset `DOCKER_HOST` and the scripts use Docker Desktop.

## Ports

These ports were checked as free on 2026-11-06. To change them, edit `.env`.

| Port | What | Bound to |
|---|---|---|
| 5433 | PostgreSQL (container port 5432). 5433 avoids clashing with a local Postgres | 127.0.0.1 only |
| 5080 | API dev server (`dotnet watch`). Swagger UI at `/api/swagger` | localhost |
| 4200 | Angular dev server (`ng serve`), proxies `/api` to 5080 | localhost |
| 8088 | Full Docker stack: nginx serving the app and `/api` | 127.0.0.1 only |

In the Docker stack, the API is not published to the host. It is reachable only through nginx.

## Run it

All commands are PowerShell, run from the repository root.

### Development (hot reload)

```powershell
Copy-Item .env.example .env      # first time only; scripts do this automatically if missing
.\scripts\dev.ps1                # Postgres in Docker + API (5080) + web (4200), each in its own window
```

Open http://localhost:4200. On start, the API applies migrations and creates the demo accounts.

`.\scripts\dev.ps1 -NoWeb` starts only the database and the API.

### Full stack in Docker (how a deployment runs)

```powershell
docker compose up -d --build
docker compose ps                # db, api and web should all be "healthy"
```

Open http://localhost:8088. Before any real deployment, set a real `JWT_SIGNING_KEY` and a strong `POSTGRES_PASSWORD` in `.env`, and set `DEMO_SEED_ENABLED=false`.

### Other scripts

| Script | What it does |
|---|---|
| `.\scripts\test.ps1` | Runs every test: .NET unit tests, .NET integration tests (throwaway Postgres containers) and Angular unit tests |
| `python scripts/generate-demo-data.py src/Liwa.Infrastructure/Seeding/Demo` | Regenerates the illustrative demo CSV files and checks them: each program adds up to 120 credits, the timetable follows UAE rules, and each demo student matches their story |
| `.\scripts\reset-db.ps1` | Deletes the local database volume and starts an empty database. Asks you to type `reset` first; `-Force` skips the prompt |
| `cd web; npm run screenshots -- --base http://127.0.0.1:8088 --milestone M1` | Captures English/Arabic screenshots at desktop and phone widths, runs the axe accessibility check, and writes `docs/screenshots/<milestone>/index.html`. Point it at the Docker stack (8088), not the dev server: only the production build runs under the real Content-Security-Policy. The script signs in about 24 times, so start the stack with `$env:SIGNIN_PERMITS_PER_MINUTE=200; docker compose up -d api` first, then run plain `docker compose up -d api` afterwards to restore the limit of 10 |

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

Docker settings come from `.env`, which is git-ignored. [.env.example](.env.example) is the committed template. The API reads standard ASP.NET Core configuration (`appsettings.json`, then environment variables such as `Jwt__SigningKey`).

| Setting | Default | Meaning |
|---|---|---|
| `Jwt:SigningKey` | none (required) | HMAC-SHA256 key, at least 32 characters. The API refuses to start without one |
| `Jwt:LifetimeMinutes` | 480 | How long a sign-in lasts |
| `Security:PasswordHashing:Iterations` | 600000 | PBKDF2-HMAC-SHA256 iterations |
| `RateLimiting:SignInPermitsPerMinute` | 10 (200 in Development) | Sign-in attempts allowed per IP per minute |
| `DemoSeed:Enabled` | false (true in Development) | Creates the demo accounts |
| `Swagger:Enabled` | false (true in Development) | Swagger UI at `/api/swagger` |
| `Database:MigrateOnStartup` | false (true in Development and in compose) | Applies EF Core migrations on start |
| `Sso:Enabled` (`SSO_ENABLED`) | false | Shows "Sign in with your Liwa account" (Microsoft Entra ID, the same account as Blackboard). See [docs/sso.md](docs/sso.md) |
| `Sso:TenantId`, `Sso:ClientId`, `Sso:ClientSecret` | empty | From the university's Entra app registration. Required when SSO is on; the API refuses to start without them |
| `Sso:LocalSignInEnabled` (`SSO_LOCAL_SIGNIN_ENABLED`) | true | Keep username/password sign-in next to SSO (demo accounts, emergency admin access) |

### Sign in with your Liwa account

This is ready to use, but stays off until university IT registers the app. [docs/sso.md](docs/sso.md) is the step-by-step guide to hand to IT. To try it locally once you have the values:

```powershell
$env:SSO_ENABLED='true'; $env:SSO_TENANT_ID='<tenant id>'; $env:SSO_CLIENT_ID='<client id>'; $env:SSO_CLIENT_SECRET='<secret>'
docker compose up -d api
```

The redirect URI to register for the local Docker stack is `http://localhost:8088/api/auth/sso/callback`.

## Project layout

```
src/
  Liwa.Domain/          entities and rules, no dependencies
  Liwa.Application/     use cases and interfaces (e.g. IAuthService, IPasswordHasher, ITokenIssuer)
  Liwa.Infrastructure/  EF Core, PostgreSQL, PBKDF2 hasher, JWT issuer, demo seeder
  Liwa.Api/             controllers, auth policies, problem-details errors, health checks, Swagger
tests/
  Liwa.UnitTests/         xUnit
  Liwa.IntegrationTests/  real API against a throwaway Postgres container
web/                    Angular app, nginx image, Playwright screenshot script
scripts/                PowerShell helpers
docs/                   brief, assumptions, import contract, SSO guide, screenshots (later: planner, PeopleSoft notes)
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
| `GET /api/advisor/students` | Advisor only (own students) |

Every endpoint requires sign-in unless it is explicitly marked anonymous. Errors are returned as RFC 7807 problem details with a `traceId`.
