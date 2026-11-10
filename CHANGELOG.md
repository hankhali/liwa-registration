# Changelog

All notable changes are listed here, newest first, grouped by milestone.

## Local PostgreSQL instead of Docker (2026-11-10)

### Changed
- **No Docker.** Development and tests use the locally installed PostgreSQL 18 on `localhost:5432` (role and database `liwa`). `scripts/create-db.sql` creates them (run once as `postgres`; safe to re-run).
- **Integration tests** no longer start Postgres containers: each test fixture creates a uniquely named database (`liwa_test_xxxxxxxx`) on the local server, the API migrates and seeds it, and it is dropped afterwards (`DROP DATABASE … WITH (FORCE)`). The server comes from `LIWA_TEST_PG` (default `liwa`/`liwa` on localhost). Testcontainers was removed.
- `scripts/run-dev.ps1` (replaces `dev.ps1`) starts the API and the Angular dev server against the local database. `test.ps1` (new `-UnitOnly`) and `reset-db.ps1` (drop and recreate the local database, local hosts only) no longer touch Docker. `scripts/common.ps1` puts the installed .NET, Node, Git and PostgreSQL on PATH and keeps NuGet/npm caches in the git-ignored `.local/` folder.
- Projects build without a native `.exe` launcher (`UseAppHost=false`), because endpoint Application Control blocks unsigned local executables.
- `.env.example`, README and `docs/sso.md` describe the local setup (development SSO redirect is now `http://localhost:4200/api/auth/sso/callback`).

### Removed
- `docker-compose.yml`, both Dockerfiles, `nginx.conf`, `.dockerignore` files.
- The Playwright screenshot script (`web/e2e/screenshots.mjs`, `npm run screenshots`) and its `@playwright/test` and `@axe-core/playwright` packages (brief §13). Old screenshots stay in `docs/screenshots/`.

### Known issue
- On the development machine, Windows Smart App Control blocked some builds of `Liwa.Api.dll`. It has since been turned off; the API, the integration tests and `reset-db.ps1` were then verified against the local PostgreSQL.

### Tests
- 153 unit, 70 integration (now on the local PostgreSQL), 38 frontend: all pass.

## M5: advisor portal, plan history, notifications (2026-11-09)

### Added
- **Review queue** (`/advisor/plans`): the advisor's own students' plans, filtered by status and program with a count on each filter, three sort orders (oldest first, newest first, student name), and an "urgent" marker (needs review, on probation, or waiting more than 3 days). A table on computers, cards on phones.
- **One-page review** (`/advisor/plans/{id}`), no tabs: the student's program, year, credits, GPA, holds and free time on the left; the plan on a weekly timetable and every class with the planner's reasons, why other class times lost, the warnings the student confirmed and today's live checks in the centre; graduation for this plan against the recommendation, with the difference, on the right; and the plan's history.
- **Decisions:** Approve (optional note; switched off while today's checks show errors), Send back (comment required) and Suggest changes (the same course choices the student sees, run on the student's record, with a live check and a required note; a plan with errors can't be sent). Suggesting changes saves a new version as "Changes suggested". **Opening a newly sent plan marks it "Advisor reviewing".**
- **Student record** (`/advisor/students/{id}`): read-only transcript and plan history for the advisor's own students. The advisor home lists plans needing attention first.
- **Student side:** the advisor's reply and note, "Accept changes" (the plan becomes approved) or change and send again, and the plan history ("Version 1 → 2" with classes added and removed).
- **Audit and notifications:** every status change is written to the audit log with the plan versions before and after, and notifies the other side. Notification bell with an unread count in the top bar for every role.
- **API:** `GET /api/advisor/plans`, `GET /api/advisor/plans/{planId}` (marks a sent plan as under review), `POST /api/advisor/plans/{planId}/check`, `POST /api/advisor/plans/{planId}/decision`, `GET /api/advisor/students/{studentUserId}`, `POST /api/students/me/plan/accept`, `GET /api/notifications`, `POST /api/notifications/read`. Advisors can only see and act on their own students (403 otherwise).
- All text in English and Arabic.
- Tests: 5 more integration tests for the advisor flow (totals: 153 unit, 70 integration, 38 frontend).

### Filled after checking M5 against brief §10 (2026-11-10)
- **Grade posting** (`POST /api/admin/grades`) and a **whole-term re-check** (`POST /api/admin/recheck`); saving a transcripts import also re-checks. An approved plan whose prerequisite or corequisite is no longer met moves to **Needs review**, is audited (`plan.needsReview`, with the problems found) and notifies the student and the advisor. A section filling up never moves a plan (`Planner.RevalidationCodes`).
- **Account holds:** students CSV column `hold` (e.g. `FINANCE`). The student can still plan; the plan carries an `AccountHold` warning to confirm, and the advisor sees a hold marker.
- **Both sides are notified** of every status change: the other person gets the existing message, the person who acted gets a confirmation (`notifications.you.*`).
- **Email stubs:** every notification also writes an email to the log in the recipient's language (`User.PreferredLanguage`, English or Arabic) until a mail server exists. The stub carries the user id, never the address.
- **Advisor overview** (`GET /api/advisor/overview`): plans awaiting review, needing review, approved this term, changes suggested.
- **Queue markers:** hold and "close to graduating" (two semesters or fewer left, from the planner's projection) now also mark a plan urgent.
- **Advisee list** (`GET /api/advisor/students`) adds standing, hold, this term's plan status (drafts too), projected graduation and last activity. The read-only **student record** adds the plan history.
- Migration `HoldsAndUserPreferences` (hold, preferred language, walkthrough dismissed). Migrations can now be added without the dotnet-ef CLI (`tests/Liwa.UnitTests/Tools/MigrationScaffolderTool.cs`).
- Tests: 12 more unit tests (holds, revalidation rule, grade posting, languages, email templates match the app's messages) and 2 more integration tests (failed prerequisite → Needs review for both sides, approve blocked by errors, propose rules; grade-posting errors). Totals: 165 unit, 72 integration, 38 frontend.

## M4: student planning screens (2026-11-09)

### Added
- **The recommended plan shows straight away** on the student's home page: "Here's the plan we recommend. Happy with it? Send it to your advisor."
- **"When are you free?"** uses big preset cards (Any time, Evenings only, Mornings only, Daytime, Let me choose). The day-and-time grid appears only for "Let me choose". "How do you want to study?" (on campus / online) and "How many credits?" are optional.
- **Every course says why it's there, in plain words:** "Needed for your degree", "Take this soon: 5 other courses depend on it", "Only offered in Fall, so take it now", "Waiting for a grade: you can take ITC301 if you pass ITC201", "Taken together with CSC101L". Ranking details stay for advisors.
- **"Change courses"** lists the courses the student can take, each with a choice of class time. Courses they can't take sit in a folded "Not available this term" panel, grouped by reason with counts (for example "Need to pass another course first (14)", "Not offered this term (5)").
- **Live checks while building the plan:** problems to fix (clashes, missing lab, over the limit, full class, not eligible yet) and things good to know (below the minimum, waiting for a grade, outside your free time).
- **"Your week":** a Monday–Friday view on computers that becomes a simple day-by-day list on phones ("Tue — ITC201, 5:00–6:15 pm"), plus the graduation estimate with and without summer classes.
- **Sending to the advisor:** each remaining warning needs its own "I understand" tick. The plan is saved as a new version, with the confirmed warnings and the student's free time stored for the advisor; the change goes to the audit log, and the advisor gets a notification. "Save for later" keeps a draft. After sending, the screen shows the plan as with the advisor.
- **API:** `POST /api/students/me/planning`, `POST /api/students/me/plan/check`, `POST /api/students/me/plan`. Planner check (`Planner.Check`) uses the same rules as the recommendation. Migration `PlanDetails` (confirmed warnings and free time on each plan version).
- All text in English and Arabic, 12-hour times (Arabic ص/م), right-to-left layout.
- Tests: 5 more integration tests for the plan flow and 8 more frontend tests (totals: 153 unit, 65 integration, 38 frontend).

### Changed
- Milestone screenshots are no longer captured, at the project owner's request.

## M3: the planner (2026-11-09)

### Added
- **Planner contract and design first** (`src/Liwa.Domain/Planning/PlannerContract.cs`, `docs/planner.md`), committed before the implementation, as the brief asks.
- **Deterministic, pure planner** (`Planner.Plan`): no database, no clock; the same input always gives the same plan.
  - Works out what's left: required groups, choose-N-credits groups and free electives.
  - Ranks courses by **critical path**: longest chain, then how many courses each unlocks.
  - Checks **eligibility**: AND/OR rules, minimum grades, corequisites, standing, credits earned, department permission, the course's usual seasons and this term's sections. A prerequisite in progress counts "with a condition" and is flagged; a failed prerequisite is named as failed.
  - Applies the **credit policy**: 3.50 boundary, probation, summer, the student's own cap, and the graduating-student exemption from the minimum.
  - **Packs sections greedily without clashes**, inside the student's free time and delivery preference, with seats left and one section per course. It records why every losing section lost (outside free time, clashes with a course, full, fewer seats). **Corequisite pairs are placed together or rejected together**, each naming the other.
  - Projects **graduation with and without summer** terms.
- Every decision carries a **reason code** with course and value parameters, ready to become plain sentences for students and detailed ones for advisors (M4/M5).
- **API:** `POST /api/students/me/recommendation` runs the planner on the student's real data, with optional free-time windows, a credit cap and a delivery preference.
- **Tests:**
  - All five tests the brief requires: Fall-only course adds a year, probation cap, GPA 3.50 gets 21 credits, a corequisite pair rejected together, and the graduating senior not forced to 12 credits.
  - 32 more planner unit tests.
  - 10 integration tests that run the planner on the demo students through the API.
  - Totals: 153 unit, 60 integration, 30 frontend.

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
