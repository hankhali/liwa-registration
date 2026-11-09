# Changelog

All notable changes are listed here, newest first, grouped by milestone.

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
