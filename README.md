# Liwa University Smart Registration Assistant

A web app that builds each student a valid, graduation-optimised semester plan, lets them adjust it, and sends it to their academic advisor for approval. The full specification is in [docs/BRIEF.md](docs/BRIEF.md). Every assumption that still needs the university's confirmation is listed in [docs/assumptions.md](docs/assumptions.md).

**Status:** milestone **M1** (scaffold, Docker, sign-in, role-based landing pages). See [CHANGELOG.md](CHANGELOG.md).

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
| `.\scripts\reset-db.ps1` | Deletes the local database volume and starts an empty database. Asks you to type `reset` first; `-Force` skips the prompt |
| `cd web; npm run screenshots -- --base http://localhost:4200 --milestone M1` | Captures English/Arabic screenshots at desktop and phone widths, runs the axe accessibility check, and writes `docs/screenshots/<milestone>/index.html` |

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

The academic records behind these students (transcripts, standing, credits) arrive with the domain model in M2.

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
docs/                   brief, assumptions, screenshots (later: planner, SSO, PeopleSoft notes)
```

## API surface (M1)

| Endpoint | Access |
|---|---|
| `POST /api/auth/login` | Anyone (rate-limited) |
| `GET /api/auth/me` | Signed in |
| `GET /api/health` | Anyone (liveness) |
| `GET /api/health/ready` | Anyone (readiness: database reachable) |
| `GET /api/admin/system` | Admin only (version, environment, database status) |

Every endpoint requires sign-in unless it is explicitly marked anonymous. Errors are returned as RFC 7807 problem details with a `traceId`.
