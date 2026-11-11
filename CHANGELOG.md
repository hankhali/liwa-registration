# Changelog

All notable changes are listed here, newest first, grouped by milestone.

## Unreleased: unambiguous graduation dates (2026-11-11)

- **Month-first graduation dates** everywhere a graduation date appears (Graduation, Home, the planner, the advisor's
  review and student list, the assistant's figures): "May 2030", with the academic term beneath on Graduation
  ("Spring 2029-2030"). Term end months: Fall = December, Spring = May, Summer 1 = July, Summer 2 = August (registrar
  question K19). Notifications and emails don't contain graduation dates.
- "Semesters left" counts Fall and Spring only; with the summer toggle on it adds "+ N summers".
- One sentence under the headline: "Taking summer classes brings your graduation forward by N semester(s): May 2030
  → May 2029", or "Summer classes wouldn't change your graduation date" (the summer-only-course explanation stays
  when that is the reason).
- Evenings-only vs full-time is month-first too, and a single line when both dates are equal.
- **The evenings-only projection** now counts the evening slots plus about as many online classes as this term offers
  (K18), with "This assumes about N online classes a semester, like this semester" on Graduation.
- Fixed: in Arabic, academic years showed reversed ("2027-2026"); the range is now kept left-to-right.

## Unreleased: normal load, free-time-aware projection, "Take this first" (2026-11-11)

- **Normal load.** `CreditPolicy.NormalLoad` (default 15, in `appsettings.json`) can be set per credit rule (e.g. per
  standing) and per term (`normal_load` in the terms file); migration `NormalLoad`. The recommendation aims for it
  (`targetCredits`) and only goes higher if the student picks more in "Credits to aim for this semester". The rule's
  maximum stays the hard cap. The projection plans the normal load too; a semester that can finish the degree may go
  up to the maximum, so all three real plans graduate a fresh student in exactly 8 semesters again.
- **Only what fits.** When free time can't reach the normal load, the plan fills what fits and says "Only N credits
  fit your free time" (new reason `OnlyCreditsFit`).
- **Free-time-aware projection.** Future semesters assume the current free time continues: at most as many classes
  as it has weekly class times (evenings at Liwa: 4). Graduation shows both dates: "If you keep evenings only: X.
  With a full-time schedule: Y." (`fullTimeProjection`).
- **"Take this first"** on the top two chain-starting planned courses, with the chain in one line ("MTH105 → MTH203 →
  BUS306 → BUS307 & BUS309"). The ranking is unchanged.
- PRM422 is now `PRM410 AND CREDITS(105)`, so the final semester can hold it.
- Tests: 226 unit, 96 integration, 75 frontend.

## Unreleased: real catalog data (2025-2026) (2026-11-11)

- **The demo catalog is now the official University Catalog 2025–2026** (`docs/reference/`): 112 courses with real
  codes, titles, credits, prerequisites and descriptions, and three real 123-credit programs with their requirement
  groups, electives and Year/Semester sequence. They are the **BBA in Business Information Technology** (replacing
  "BBA in Management"), **Bachelor of Information Technology (BIT) — General Track** and **Bachelor of Mass
  Communication Concentration in Public Relations**. `courses.csv` and `degree-plans.csv` carry a `source` column
  (catalog page); the importer accepts it. Arabic titles stay machine-drafted.
- `scripts/extract_catalog.py` extracts the Course Descriptions section from the PDF. `scripts/generate-demo-data.py`
  now builds everything from the catalog and checks it: 123 credits per plan, every study-plan slot, unknown
  courses, cycles, and a prerequisite cross-check against the course descriptions (the differences are listed in
  docs/assumptions.md K7).
- Offering pattern: every course every term (not in the catalog; registrar question K1). The Spring 2027 timetable
  is regenerated for the real codes (264 sections).
- **Terms named by academic year**, as on Liwa transcripts: "Spring 2026-2027", "Fall 2026-2027", "Summer 1
  2025-2026" (term codes unchanged). Summer 2 is a registrar question (K3).
- **16 demo students**: the 15 stories re-mapped onto the real plans, plus `student.hanieh`, a real BBA-BIT record
  (anonymised) with weekday-evening free time.
- Tests updated for the real codes. "A fresh student graduates in 8 semesters" became "within 8, at least 7": the
  BBA-BIT plan projects 7 at the 18-credit limit (K13). The permission-assumed and lab-corequisite stories moved to
  synthetic tests (K12). Totals: 222 unit, 96 integration, 74 frontend.

## Unreleased: UX pass for first-year students (2026-11-11)

Based on screenshots of every student screen (EN/AR, desktop/phone). Findings and what changed for each are in
[docs/ux-findings.md](docs/ux-findings.md); before/after captures are in `docs/screenshots/` (contact sheet:
`docs/screenshots/ux-pass/index.html`).

- **Brand:** the brief's indigo `#3d3d5f` and gold `#f7bf0a` replace charcoal/sage everywhere. The official lu.ac.ae
  white wordmark (on indigo only) and indigo shield (favicon) replace the Blackboard-derived logos.
- **Fixed: the week grid put every 50-minute class, 12:30 class and 2:00–4:50 lab below 8 pm.** Rows are now
  5 minutes. Blocks show code and title. Phones get a per-day list with no hour grid.
- **Home** is one answer, "Spring 2027: your plan is *Sent back with comments*", with what it means, one gold button
  for the next step, and three big numbers: credits done, GPA, graduation date.
- **Every student screen** has one gold primary action, top right, and a first sentence saying what to do. There is
  no jargon: a test fails on "eligible", "prerequisite", "corequisite", "critical path", "section", "term",
  "validation" or "projection" in student text, or on any untranslated Arabic student string. Every course shows
  code and title, reasons included. Students see new status names (Sent to advisor, Advisor is reviewing, Sent back
  with comments, Advisor suggested changes, …), each with "what this means for you".
- **Planner:** one-tap free-time buttons (Any time / Daytime / Evenings only / Weekends + evenings) rebuild the plan
  in place; credits as a big number; one plain reason per class; "Classes you can't take yet" always visible, each
  with a one-sentence reason; the AI box is hidden when no provider is set. **Free time:** picking a preset saves and
  opens the rebuilt plan.
- **Graduation:** the date and semesters left as headline numbers, with one toggle (summer). Semesters stack on
  phones. "Why some classes wait" is collapsed.
- **My Plan:** the advisor's note first, with one button for the next step (Fix my plan / Accept the changes /
  Check my plan again); the classes in the plan; history with titles, no class codes or version numbers.
- **Phone bottom bar** for students: Home / Plan / Graduation / Messages.
- **Arabic:** semester names in Arabic everywhere (top bar, headings, messages).
- **Advisor review:** three columns (student | plan | graduation) with the decision bar at the bottom. **Suggest
  changes** opens the same two-panel planner the student uses (`shared/plan-editor.component.ts`).
- **Follow-ups:** the student menu is Home / My free time / My plan / Graduation / Messages, matching the page
  headings (staff menus unchanged). My Plan shows suggested changes side by side (what you sent, with removed classes
  struck through, next to the advisor's version, with added classes highlighted); the API adds
  `saved.previousSectionIds`. Graduation explains when a summer-only required class makes the summer toggle change
  nothing.
- Dev: `playwright` devDependency driving the installed Chrome (`channel: 'chrome'`, no browser download);
  `web/scripts/ux-screenshots.mjs` and `web/scripts/advisor-review-shot.mjs`.
- Tests: 220 unit (+2: student-text jargon, Arabic completeness), 95 integration (previous version returned), 72
  frontend (+9: grid rows, plan editor, student history wording, one-tap free time, menu labels, side-by-side
  changes, summer-only note).

## v0.1.0 — first release (2026-11-10)

The first complete version of Smart Registration: students get a valid, graduation-optimised plan and send it to their
advisor; advisors review it on one page; registrar staff load and edit the data; an optional AI assistant explains
plans without deciding anything. English and Arabic throughout.

**Release notes**
- **Students:** recommended plan first; free time with four presets or an hour grid; Change courses with live checks;
  autosaved drafts; Graduation Path with and without summer and a delay banner; My Plan with status, advisor notes,
  proposed changes and history; messages; first-login walkthrough.
- **Advisors:** counts and a Needs-attention list; queue with filters and urgency markers; one-page review with the
  student's free time, timetable, reasons, checks and graduation comparison; approve / send back / suggest changes;
  plans move to Needs review when a failing grade is posted.
- **Registrar staff:** catalog editing through the importer; imports with a dry run and line-by-line errors; exports
  that round-trip; data change history; staff accounts and advisor reassignment; credit rules with a "who would change"
  test; university sign-in and AI settings with connection tests.
- **Planner:** deterministic and pure, tested on the three seeded programs (a fresh student graduates in 8
  semesters), identical output from CSV and from the database.
- **Install:** plain server install (no Docker) with PostgreSQL, the .NET 10 runtime and IIS or nginx
  ([docs/INSTALL.md](docs/INSTALL.md)); `scripts/verify_install.py` checks a clean install end to end.

### Added in this milestone (M8: documentation and release)
- **Data provider layer** (brief §6): `IStudentRecordProvider`, `ICourseCatalogProvider`, `ITermScheduleProvider`;
  `DatabaseSisProvider` in use, `PeopleSoftSisProvider` documented stub; `Sis:Provider` chooses. The planner reads
  only through these interfaces.
- `dotnet Liwa.Api.dll create-admin <username> "<Name>"` (password from `LIWA_ADMIN_PASSWORD`) for the first
  administrator on a server without demo data.
- Scripts: `verify_install.py` (clean-install check, drops nothing), `demo_plan_states.py` (a demo plan in every
  status), `check_contrast.py` (theme tokens against WCAG AA, light and dark).
- Docs: README for a cold reader, [INSTALL.md](docs/INSTALL.md), [peoplesoft-integration.md](docs/peoplesoft-integration.md),
  [security.md](docs/security.md), [demo-script.md](docs/demo-script.md), [explainer.md](docs/explainer.md),
  [ui.md](docs/ui.md), the Registrar question checklist in [assumptions.md](docs/assumptions.md), SAML alternative and
  in-app settings in [sso.md](docs/sso.md), [BRAND.md](web/public/assets/brand/BRAND.md),
  [design/reference/portal-patterns.md](design/reference/portal-patterns.md).

### Checks at release
- Tests: **218 unit, 95 integration, 63 frontend — all pass**.
- `check_contrast.py`: all theme pairs pass WCAG AA in light and dark.
- Clean install (database dropped and recreated, Release build from `dist/`): `verify_install.py` — all checks pass.

## M7: AI explainer and Arabic completion (2026-11-10)

### Added
- **"Ask about your plan"** on the Planner page: **Explain my plan** (4–6 plain sentences), **Why?** next to every not-eligible course, and a question box limited to the plan. On the advisor's review page the same explainer runs on the submitted plan, plus **Draft a note to the student**, which goes into the advisor's note box to edit and is never sent automatically. See [docs/explainer.md](docs/explainer.md).
- **Grounding enforced in code** (`ExplainerGuard`): the payload is built only from planner output and the catalog entries of the courses in it, and is refused if it contains a name, ID or email; questions are stripped of those first; replies that name an unknown course, contradict the planner or are in the wrong language are discarded and replaced by "I can't explain that part — ask your advisor"; every discard is logged.
- **What-if questions re-run the pure planner** (drop / add / summer / why), recognised by rules in English and Arabic, otherwise classified by one model call that the code checks; the model explains only the difference and the planner's own figures are shown under the answer. Anything the planner can't compute is declined.
- Providers: Anthropic Messages API (default `claude-sonnet-5-5`), OpenAI-compatible endpoints, and a mock for demos that only runs in Development (any other environment refuses to start with it). 20-second timeout, one retry, then the fallback; 30-minute cache of identical plan + question pairs; per-person hourly limit; **Test connection** in Settings; the **AI usage** page fills in (requests, tokens, estimated cost, cache hits, fallbacks, discards). The log stores counts and reasons, never content. Migration `AiRequests`.
- **Arabic completion:** every reason and warning code has an English and an Arabic sentence (a test fails if one is missing, or if both files don't have exactly the same keys); Arabic course titles for all 97 courses; [translations/REVIEW.md](translations/REVIEW.md) lists every machine-drafted Arabic string for native review.
- API: `POST /api/students/me/explain`, `POST /api/advisor/plans/{id}/explain`, `GET /api/me/explainer`, `POST /api/admin/settings/ai/test`, `GET /api/admin/ai-usage`.
- Tests: 21 unit tests for the grounding rules and question parsing; 9 integration tests with a scripted model (not available without a provider, no personal data sent, discarded reply, timeout → retry → fallback, cache, what-if re-run, declined question, hourly limit, advisor draft note). Totals: 218 unit, 95 integration, 63 frontend.

## M6: administration (2026-11-10)

### Added
- **Catalog** (`/admin/catalog`): courses, degree plans, programs, semesters and class times as searchable tables. **Every edit, even one field, goes through the importer** (brief §4): Preview shows exactly what a file would do, Save creates a catalog version. Degree plans are edited as a whole plan and can be removed only when no student follows them (409 otherwise). The **prerequisite editor** checks the rule live: can it be read, unknown courses, a course needing itself, cycles.
- **Imports & exports** (`/admin/imports`): choose a file → Check (dry run: added / changed / unchanged / removed, every problem by line and column) → Apply. **Any error rejects the whole file with HTTP 422.** Imports are idempotent and never delete students. **Exports** (one file or a zip of all seven, numbered in import order) import again with nothing changed (tested for every kind).
- **Catalog versions:** every saved upload, edit and removal gets a number, source, time, who and row counts, listed on the Imports page and for advisors (`/advisor/versions`, `GET /api/catalog/versions`).
- **Users** (`/admin/users`): advisor and administrator accounts, new passwords (10+ characters), switching accounts on and off (never your own, never the last administrator), and **moving students to another advisor** with notifications to the students and both advisors.
- **Settings** (`/admin/settings`): university sign-in (Entra ID) and the AI provider, each with **Test connection**; secrets are encrypted at rest (AES-256-GCM, `Secrets:Key`) and only ever shown as their last four characters. Username/password sign-in can only be switched off with SSO on, a passing test and an explicit confirmation; saved sign-in settings apply at the next start. **Credit rules** with a test that lists every student whose limits would change, and a check that refuses rules that could never apply.
- **AI usage** page (`/admin/ai-usage`), filled by the explainer in M7.
- Admin menu items in the rail: Catalog, Imports & exports, Users, Settings, AI usage.
- API: `GET /api/admin/catalog/{kind}`, `POST /api/admin/catalog/{kind}/rows`, `POST /api/admin/catalog/prerequisites/check`, `DELETE /api/admin/catalog/degree-plans/{program}/{year}`, `GET /api/admin/exports[/{kind}]`, `GET|PUT /api/admin/credit-policy`, `POST /api/admin/credit-policy/preview`, `GET|POST /api/admin/users`, `PUT /api/admin/users/{id}/password|active`, `POST /api/admin/users/reassign`, `GET|PUT /api/admin/settings/sso|ai`, `POST /api/admin/settings/sso/test`, `GET /api/catalog/versions`. Migration `CatalogVersionsAndSettings`.
- Tests: 8 admin integration tests (export round trip, 422 rejection, edits through the importer and versions, prerequisite check, degree-plan removal guard, credit-rule test, staff and reassignment, settings and secrets) and 3 frontend tests. Totals: 190 unit, 86 integration, 62 frontend.

## Audit of M1–M5 against the brief (2026-11-10)

Gaps found by checking M1–M5 against brief §4 (decisions), §5 (data), §8 (required tests) and §9 (usability), now fixed.

### Added
- **App frame** (brief §9): page title and term selector in the top bar, light/dark/device theme, a collapsible left rail per role, a bottom bar and drawer on phones. **Shared components** documented in [docs/ui.md](docs/ui.md): page header, stat card, status chip (fixed colour per status), empty state, confirm dialog, weekly timetable grid (clashes, online, Friday afternoon blocked, free-time editor, list view on phones), plan history ("Version 1 → 2").
- **Student pages:** Home with a first-login walkthrough (dismissed per user), **Availability** (four preset cards, "skip this to see every class", hour grid, saved on the server), **Planner** ("Here is the plan we recommend for Spring 2027", one primary "Send to my advisor", gold "Take on time" badge, Details, actionable checks, "Not eligible this semester" panel grouped by reason with counts, two-panel Change courses with timetable and credit counter, draft autosave), **Graduation Path** (semesters as columns, Summer toggle, My selection / Recommended, delay banner, "Why some courses wait"), **My Plan** (status tracker, advisor notes, proposed changes as a diff with Accept, history) and **Messages**.
- **Advisor:** stat cards on the home page, "Need review after new grades" filter and markers in the queue (probation, hold, graduating soon, confirmed notes), **My students** page, the review page's timetable, free-time grid and "this plan delays graduation by one semester".
- **Planner rules:** `NearlyFull` (≥ 90% of seats taken), `FallOnlyMissed` (a single-season course offered now but left out waits a year), `PermissionAssumed` (the projection assumes department permission later), per-term override of in-progress prerequisites (terms CSV `in_progress_prerequisites`) with `GradeMustBePosted`, and prerequisite **cycle detection** in the course import. The planner puts its input in a canonical order, so database and CSV give identical output.
- **Demo data:** 15 students, each with a sign-in and one of the brief's stories (evening-only, transfer credits, hold, part-time, pending internship permission, major change, two mid-programme, …); 196 Spring 2027 sections with 13 full; brief §3 program titles. Saved free time for the evening-only and part-time students.
- **Demo credentials on the sign-in page in Development only** (`GET /api/auth/demo-accounts`, 404 elsewhere). **`UI_ARABIC_DIGITS`** setting (Western digits by default). `translations/REVIEW.md` (every machine-drafted Arabic string, regenerated by `scripts/translation_review.py`).
- API: `GET/PUT /api/students/me/preferences`, `PUT /api/students/me/draft`, `POST /api/students/me/projection`, `GET /api/me/settings`, `GET /api/me/terms`, `PUT /api/me/language`, `POST /api/me/walkthrough/dismiss`. Migrations `StudentPreferences`, `TermInProgressOverride`.
- **Tests on the real seeded data** (`CsvPlannerSource`): every seeded program graduates a fresh student in 8 semesters, section 02 beats 01 on seats, the senior isn't forced to the minimum, holds, permission note, evening-only student, transfer credit, full sections never recommended, determinism; and an integration test that every demo student's plan is identical from CSV and from the database. The remaining §8 tests (10:00–10:50/10:50–11:40 not a clash, one-minute overlap is, probation 15-credit selection fails, GPA 3.35 → 18, a corequisite alone is `CorequisiteMissing`) and translation coverage (every reason code in English and Arabic; both files have the same keys). Frontend tests for the shared components, RTL rendering and the planner view.

### Changed
- "semester" instead of "term" in everything students read; standing with the year ("Sophomore (2nd year)"); `WaitingForGrade` names the course first; reasons without a course ("All seats are taken") read correctly.
- Tests: 190 unit, 78 integration, 59 frontend. The initial bundle budget warning is raised to 800 kB (Material + both fonts).

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
