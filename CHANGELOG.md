# Changelog

All notable changes are listed here, newest first, grouped by milestone.

## M2: data model, database, demo data, CSV import (2026-11-09)

### Added
- **Data model:** terms (teaching days per term, a Friday-afternoon switch, one current term); courses (credits 0–6, Arabic titles, prerequisite rules, usual seasons); sections with weekly meeting times; programs; degree plans per catalog year (required groups, choose-N-credits groups, free electives); student records (transcript, in-progress courses, transfer credits, advisor, registrar status); a configurable credit policy; versioned plans; an audit log; notifications. Migration `AcademicModel`.
- **UAE calendar rules**, checked on every section: no Sunday classes, no Sunday/Tuesday/Thursday pattern, only the term's teaching days, and nothing after 12:00 on Friday unless the term allows it.
- **Prerequisite rules as text** (`CSC101 AND (MTH102 OR MTH110:C)`, `COREQ(…)`, `STANDING(…)`, `CREDITS(…)`, `PERMISSION`), with a reader that explains each mistake and where it is.
- **GPA, credits and standing** from a transcript. The latest or highest attempt counts; transfer and pass credit count toward the degree but not the GPA. **Credit limits** depend on season, standing, GPA band and probation, and are seeded from `appsettings.json`. They cover the brief's 3.50 boundary (21 credits) and the graduating-senior exemption from the minimum.
- **CSV import** for terms, courses, programs, degree plans, sections, students and transcripts. Dry run by default, all-or-nothing, safe to repeat, with plain-language problems by line and column; saved imports go to the audit log. The contract for the Registrar and IT is in `docs/import-contract.md`. Admin endpoint: `POST /api/admin/imports/{kind}`.
- **Demo data**, loaded through the real import: three programs at 120 credits each (including 1-, 2- and 4-credit courses), 97 courses, a Spring 2027 timetable of 169 classes (daytime, Monday/Wednesday/Friday mornings, evenings, online), and 10 students, including the five stories from the brief. The generator and its self-checks are in `scripts/generate-demo-data.py`.
- **Home pages show real data, in plain words.** Students see their program, a progress bar ("36 of 120 credits done"), year of study, GPA and advisor, with a supportive note when on probation. Advisors see their students with credits, GPA and an "On probation" tag. Admins see what data is loaded and when it was last uploaded (in 12-hour time).
- Tests: 116 .NET unit, 50 integration, 30 frontend. Screenshots in `docs/screenshots/M2/`.

### Changed
- API enums are sent as names (for example `"Probation"`), not numbers.

## Between M1 and M2: Liwa sign-in, branding, plain language (2026-11-09)

### Added
- **Sign in with your Liwa account** (Microsoft Entra ID, the same account as Blackboard and university email), using OpenID Connect with the code flow, PKCE, the account picker and only the `openid profile email` scopes. Microsoft's tokens are never stored, and our session token is handed to the app through a single-use 60-second code, never in a URL. Only people who already have a Smart Registration account get in: matched by university email on first sign-in, then linked permanently to their Entra object ID, and never re-linked to a different person. Plain-language messages (English and Arabic) for "not found", "switched off", "took too long" and "not available right now". If Microsoft can't be reached, people get a message instead of an error page. **Off by default** until university IT supplies the tenant ID, client ID and secret; the step-by-step guide for IT is in `docs/sso.md`.
- Username/password sign-in can be kept next to SSO (behind "Sign in with a username instead") or switched off (`SSO_LOCAL_SIGNIN_ENABLED`).
- Tests: 12 new unit tests (account matching and linking, single-use codes), 9 new integration tests (config, redirect to Microsoft with PKCE, unreachable provider, code exchange, local sign-in switch), 5 new frontend tests.

### Fixed
- nginx now forwards the original `Host` header with its port, and keeps a load balancer's `X-Forwarded-Proto`, so redirect addresses built by the API are correct.

### Changed
- **Re-branded to match Liwa University's real identity:** charcoal `#272727` and sage `#b5b59e` (from the official logo) replace indigo and gold. A deep olive `#6b6a52` is the accent and focus colour on light backgrounds. Material palettes were regenerated from the new colours.
- **Official logo:** the sign-in page shows the full bilingual lockup on a charcoal band (as on Liwa's Blackboard sign-in), and the top bar shows the shield mark. The favicon and touch icon are now the shield on charcoal. The placeholder `logo.svg` was removed.
- **Plain language for non-technical users (all roles):** the admin "System status" card is now "Is everything working?", with one plain sentence ("Everything is working normally", or what to do if not) and a warning when practice accounts are on. Database, environment and version are folded under "Details for IT". Admin areas are reworded ("Courses and class times", "How many credits students can take", "Upload and download data (spreadsheets)"). Sign-in fields no longer show asterisks, the username hint explains what to type, and the "too many tries" message is friendlier. English and Arabic.
- The Docker sign-in rate limit can now be set with `SIGNIN_PERMITS_PER_MINUTE` (default 10), so screenshots can be taken against the real production build.

### Fixed
- **Docker/production build showed an unstyled page** (icon names as text, see-through card). Angular's critical-CSS inlining loads the main stylesheet with an inline `onload` script, which the nginx Content-Security-Policy (`script-src 'self'`) correctly blocks. Turned off `inlineCritical` for production builds so the stylesheet loads normally and the strict policy stays. The M1 screenshots came from the dev server, which sends no CSP, so they didn't catch this.

## M1: scaffold, Docker, sign-in, role-based landing pages (2026-11-09)

### Added
- **Backend (.NET 10):** four-project clean architecture (`Liwa.Domain`, `Liwa.Application`, `Liwa.Infrastructure`, `Liwa.Api`).
  - Local sign-in behind interfaces (`IAuthService`, `IPasswordHasher`, `ITokenIssuer`, `IUserRepository`), so SSO can replace it without changing callers.
  - PBKDF2-HMAC-SHA256 password hashing (600,000 iterations, iteration count stored per hash so it can be raised later).
  - JWT (HMAC-SHA256) with role claims and `Student`, `Advisor`, `Admin` and `Staff` policies. Every endpoint requires sign-in unless explicitly anonymous.
  - Per-IP rate limit on sign-in, RFC 7807 problem-details errors with a trace ID, liveness and readiness health checks, and Swagger (off outside Development).
  - EF Core + PostgreSQL with the `InitialUsers` migration. Optional migrate-on-startup.
  - Demo seeder (only when `DemoSeed:Enabled`): 5 students, 3 advisors and 1 admin.
  - Admin-only system status endpoint. Students never see engineering information.
- **Frontend (Angular 22 + Material 22):** standalone, zoneless, signals.
  - Indigo and gold theme. Open Sans, Noto Sans Arabic and Material Icons are self-hosted (no CDN). Placeholder logo at `web/public/assets/brand/logo.svg`.
  - English and Arabic switch in the running app (Transloco), with RTL applied to the page and to Material overlays. Page titles are translated too.
  - A single `AuthService`, a bearer interceptor that signs out on 401, role guards, and a landing page per role (student, advisor, admin). Not-allowed and not-found pages.
- **Docker:** `docker-compose.yml` with `db` (Postgres 17), `api` and `web` (nginx serves the app and proxies `/api` same-origin, with CSP, `X-Frame-Options` and other security headers). Settings come from `.env`, and `.env.example` is committed. The database and web ports are bound to loopback, and the API is not published.
- **Scripts (PowerShell):** `scripts/dev.ps1`, `scripts/reset-db.ps1`, `scripts/test.ps1`.
- **Tests:** 28 .NET unit tests, 16 .NET integration tests (real Postgres in throwaway containers), 21 Angular unit tests.
- **Screenshots:** `docs/screenshots/M1/` has 7 screens × English/Arabic × desktop/phone (28 images), plus an axe-core WCAG 2.1 AA report with 0 violations and no horizontal scrolling on phones.
- **Docs:** `README.md`, this changelog, `docs/assumptions.md`.
