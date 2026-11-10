# Liwa University Smart Registration Assistant

Every semester students pick sections by guesswork, and advisors spend the first weeks fixing them. Smart Registration
builds each student a **valid, graduation-optimised semester plan** — only classes they can actually take, no clashes,
inside their free time and credit limits, in the order that gets them to graduation soonest — lets them adjust it, and
sends it to their **academic advisor**, who approves it, sends it back, or proposes changes. An optional **AI
assistant** explains the plan in plain words but never decides anything. English and Arabic, right-to-left included.

**Version 0.1.0** (first release). What changed: [CHANGELOG.md](CHANGELOG.md). The original specification:
[docs/BRIEF.md](docs/BRIEF.md) (and `LIWA_FULL_BUILD_BRIEF.md`).

## Start here

| If you are… | Read |
|---|---|
| Showing it to faculty | [docs/demo-script.md](docs/demo-script.md) — a 10-minute walkthrough |
| The Registrar | [docs/assumptions.md](docs/assumptions.md) — the questions we need answered, with today's defaults · [docs/import-contract.md](docs/import-contract.md) — the data files |
| University IT (servers) | [docs/INSTALL.md](docs/INSTALL.md) — install on Windows Server or Ubuntu, backups, upgrades |
| University IT (identity) | [docs/sso.md](docs/sso.md) — "Sign in with your Liwa account" (Entra ID) |
| The PeopleSoft team | [docs/peoplesoft-integration.md](docs/peoplesoft-integration.md) — fields needed and a PS Query export spec |
| Reviewing security / privacy | [docs/security.md](docs/security.md) — in plain language |
| A developer | This page, then [docs/planner.md](docs/planner.md), [docs/ui.md](docs/ui.md), [docs/explainer.md](docs/explainer.md) |
| A native Arabic speaker | [translations/REVIEW.md](translations/REVIEW.md) — every machine-drafted Arabic string |

## What's in it

| Part | What it does |
|---|---|
| **Planner** (`src/Liwa.Domain/Planning`) | A pure, deterministic function: student record + catalog + timetable + free time → eligible courses with reasons, a clash-free recommended set prioritised by the critical path, a graduation projection with and without summer, and warnings. Codes + parameters only; the app turns them into sentences. Tested on the real seeded programs. |
| **Student** | Home (studies, next step, first-login walkthrough), Availability (four preset cards + hour grid), Planner (recommended plan first, Change courses, autosaved drafts, actionable checks), Graduation Path, My Plan (status, notes, proposed changes as a diff, history), Messages |
| **Advisor** | Home (counts, needs attention), Plans to review (filters, urgency markers), one-page review (summary, timetable, reasons, checks, graduation for this plan vs the recommendation, approve / send back / suggest changes), My students, read-only records, data changes |
| **Admin** | Catalog (every edit goes through the importer), Imports & exports (check → apply, 422 with line numbers, zip export that round-trips), Users (staff accounts, move students between advisors), Settings (university sign-in, AI provider, credit rules with a "who would change" test), AI usage |
| **AI explainer** | Explain my plan, Why not this course?, what-if questions that re-run the planner. Grounding enforced in code: no personal data sent, ungrounded replies discarded |

| Layer | Technology |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core, EF Core 10 + PostgreSQL. `Liwa.Domain` (rules, planner) → `Liwa.Application` (use cases, interfaces) → `Liwa.Infrastructure` (database, providers, imports, explainer) → `Liwa.Api` |
| Frontend | Angular 22 + Angular Material 22 (standalone, signals, zoneless), Transloco for English/Arabic, light and dark themes |
| Data sources | Through `IStudentRecordProvider`, `ICourseCatalogProvider`, `ITermScheduleProvider`; `Sis:Provider` = `Database` (filled by CSV imports) today, `PeopleSoft` stub documented |

## Run it on a development machine (Windows)

Nothing is downloaded by the scripts. You need (versions used here):

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.403 | `C:\Program Files\dotnet` |
| Node.js + npm | 26.13 / 11.20 | `C:\Program Files\nodejs`; the Angular CLI comes from `web/node_modules` |
| PostgreSQL | 18 (16+) | Windows service on port 5432 |
| Git | 2.55 | On this machine at `%LOCALAPPDATA%\Programs\Git\cmd\git.exe` (not on PATH; the scripts add it) |
| Python | 3.10+ | For the checks and demo scripts |

```powershell
# once: create the database role and database (as the postgres superuser)
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U postgres -f scripts/create-db.sql
# once: restore packages
dotnet restore; cd web; npm ci; cd ..
# every time
.\scripts\run-dev.ps1          # API on http://localhost:5080 + web on http://localhost:4200 (proxies /api, same origin)
```

Open **http://localhost:4200**. On start the API applies migrations and loads the demo data. The sign-in page lists the
practice accounts in Development; every password is **`Liwa#2027`**.

The scripts put the tools on PATH for their own process (`scripts/common.ps1`) and keep NuGet/npm caches in the
git-ignored `.local/` folder. Projects build without a native `.exe` launcher (`UseAppHost=false`); run the API with
`dotnet Liwa.Api.dll` or `dotnet run`. If Windows **Smart App Control** reports "An Application Control policy has
blocked this file", turn it off or ask IT for an exception for the repository folder.

### Scripts

| Command | What it does |
|---|---|
| `.\scripts\run-dev.ps1 [-NoWeb]` | Starts the API (and the web dev server) against the local database |
| `.\scripts\test.ps1 [-UnitOnly]` | All tests: .NET unit, .NET integration (each run creates `liwa_test_…` on the local PostgreSQL and drops it), Angular unit |
| `.\scripts\reset-db.ps1 [-Force]` | Drops and recreates the local `liwa` database (local hosts only) |
| `python scripts/verify_install.py [--web URL] [--web-dir dist/web]` | Clean-install check: migrations in order, seed loaded, every demo account signs in, the planner runs for all 15 students, advisor and admin pages load. Drops nothing |
| `python scripts/demo_plan_states.py` | Puts demo plans into every status (draft → needs review) for manual review |
| `python scripts/check_contrast.py` | Every text/background pair of the theme tokens, light and dark, against WCAG AA |
| `python scripts/generate-demo-data.py src/Liwa.Infrastructure/Seeding/Demo` | Regenerates the demo CSV files with self-checks (120-credit plans, UAE timetable rules, student stories) |
| `python scripts/translation_review.py` | Regenerates `translations/REVIEW.md` |

Release build: `dotnet publish src/Liwa.Api -c Release -o dist/api` and
`cd web; npx ng build --configuration production --output-path ../dist/web` (see [docs/INSTALL.md](docs/INSTALL.md)).

## Demo accounts

Only when `DemoSeed:Enabled` is true (Development); never on a real server. Password **`Liwa#2027`** for all.

| Username | Program | Story (brief §5) |
|---|---|---|
| `student.new` | IT | New first-year; AP English credit; CSC101 + lab corequisite pair |
| `student.evening` | IT | Works during the day: free time saved as evenings only |
| `student.repeat` | Mass Communication | Failed MCM201, which MCM301 needs with at least a C |
| `student.transfer` | Business | Three transfer credits (count toward the degree, not the GPA) |
| `student.probation` | Business | GPA 1.40, on probation (12-credit cap); repeated BUS120 |
| `student.senior` | IT | 6 credits left, not forced to the 12-credit minimum |
| `student.hold` | Mass Communication | FINANCE hold: can plan, can't register until cleared |
| `student.honors` | IT | GPA exactly 3.50 (21-credit cap); Fall 2026 grades not posted yet |
| `student.parttime` | Business | Part-time: saved cap of 9 credits |
| `student.intern` | IT | Internship needs department permission (assumed in the projection, with a note) |
| `student.switch` | Business | Changed major from Mass Communication |
| `student.mid1`, `student.mid2` | Business, IT | Mid-programme |
| `student.first`, `student.media` | IT, Mass Communication | Steady progress |
| `advisor.it`, `advisor.business`, `advisor.media` | | Advisors (7, 5 and 3 students) |
| `admin` | | Registrar administrator |

The data is illustrative: the three program titles are real, the courses, plans, 196-section Spring 2027 timetable and
students are invented until the Registrar supplies real data.

## Configuration

The API reads `appsettings.json`, `appsettings.{Environment}.json` and environment variables (`Section__Key`). The full
list with production values is in [docs/INSTALL.md](docs/INSTALL.md#4-configuration). The essentials:
`ConnectionStrings__Liwa`, `Jwt__SigningKey`, `Secrets__Key`, `Database__MigrateOnStartup`, `DemoSeed__Enabled`;
university sign-in and the AI provider are set by an administrator in **Settings** (secrets encrypted).
`.env` (from [.env.example](.env.example)) only feeds the development scripts.

## Project layout

```
src/
  Liwa.Domain/          entities, rules, prerequisite parser, credit policy, the pure planner
  Liwa.Application/     use cases and interfaces (auth, planning, admin, SIS providers, explainer grounding)
  Liwa.Infrastructure/  EF Core + PostgreSQL, imports/exports, providers, explainer, seeding, security
  Liwa.Api/             controllers, auth policies, problem details, health checks
tests/
  Liwa.UnitTests/         planner (incl. the brief's required tests on the seeded data), rules, grounding
  Liwa.IntegrationTests/  the real API against a throwaway database per run
web/                    Angular app (shared components in src/app/shared, docs/ui.md)
scripts/                run, test, reset, verify, demo states, contrast, demo-data generator
docs/                   planner, UI, explainer, install, SSO, PeopleSoft, security, assumptions, demo script
design/reference/       what we take from PeopleSoft Fluid and Blackboard Ultra
translations/           Arabic strings for native review
```

## API

Every endpoint requires sign-in unless marked anonymous, and every role check happens on the server. Errors are
RFC 7807 problem details with a `traceId`; import files with errors are rejected whole with HTTP 422. In Development
the full, browsable list is at http://localhost:5080/api/swagger.

| Area | Endpoints |
|---|---|
| Sign-in | `POST /api/auth/login`, `GET /api/auth/me`, `GET /api/auth/sso/config\|start\|callback\|complete`, `POST /api/auth/sso/exchange`, `GET /api/auth/demo-accounts` (Development only) |
| Me | `GET /api/me/settings`, `GET /api/me/terms`, `PUT /api/me/language`, `POST /api/me/walkthrough/dismiss`, `GET /api/me/explainer`, `GET\|POST /api/notifications[/read]` |
| Student | `GET /api/students/me/summary`, `GET\|PUT /api/students/me/preferences`, `PUT /api/students/me/draft`, `POST /api/students/me/planning`, `POST /api/students/me/recommendation`, `POST /api/students/me/plan/check`, `POST /api/students/me/plan`, `POST /api/students/me/plan/accept`, `POST /api/students/me/projection`, `POST /api/students/me/explain` |
| Advisor | `GET /api/advisor/overview`, `GET /api/advisor/students[/{id}]`, `GET /api/advisor/plans[/{id}]`, `POST /api/advisor/plans/{id}/check\|decision\|explain`, `GET /api/catalog/versions` |
| Admin | `GET /api/admin/system\|overview`, `POST /api/admin/imports/{kind}?dryRun=`, `GET /api/admin/catalog/{kind}`, `POST /api/admin/catalog/{kind}/rows`, `POST /api/admin/catalog/prerequisites/check`, `DELETE /api/admin/catalog/degree-plans/{program}/{year}`, `GET /api/admin/exports[/{kind}]`, `GET\|PUT /api/admin/credit-policy`, `POST /api/admin/credit-policy/preview`, `GET\|POST /api/admin/users`, `PUT /api/admin/users/{id}/password\|active`, `POST /api/admin/users/reassign`, `GET\|PUT /api/admin/settings/sso\|ai`, `POST /api/admin/settings/sso/test\|ai/test`, `GET /api/admin/ai-usage`, `POST /api/admin/grades`, `POST /api/admin/recheck` |
| Health | `GET /api/health`, `GET /api/health/ready` (anonymous) |
