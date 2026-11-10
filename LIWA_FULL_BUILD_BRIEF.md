# Liwa University Smart Registration Assistant — Full Build Brief

## 0. Rule that overrides everything else: NO DOWNLOADS

This machine runs corporate endpoint security that kills and quarantines any process that downloads or unpacks executables. It has already removed this tool once. Therefore:

- **Never** download, install, unpack or run an installer for any SDK, runtime, browser, CLI, tool, image or binary. No `dotnet-install`, no `winget`, no `choco`, no `Invoke-WebRequest`/`curl`/`wget` to fetch executables or archives, no `docker pull`, no `npx playwright install`, no global `npm install -g`, no `dotnet tool install`.
- **Allowed without asking:** ordinary package restores from the project's own manifests — `npm install` / `npm ci` in a project folder, `dotnet restore` / `dotnet build`, `pip install` from `requirements.txt`.
- **Also allowed:** fetching plain data files from the web — images (PNG/SVG/JPG), HTML, CSS, JSON, CSV, text. Logos, stylesheets and reference pages are fine. The line is: **nothing executable, nothing packed** — no `.exe`, `.msi`, `.zip`, `.7z`, `.tar.gz`, `.nupkg`, no installers, no browsers, no runtimes.
- **If anything executable or packed is missing** (an SDK, a CLI, a tool, a browser), **stop, print exactly what is needed and the exact command or URL to get it, and wait for me.** I will install it myself and tell you when it's done. Then continue.
- Check what is installed first (`dotnet --list-sdks`, `node --version`, `ng version`, `python --version`, `psql --version`) and work with the versions present.
- Work only inside this project folder. Never write to the user's home directory, `tools` folders or anywhere else.

Every other instruction in this brief is subordinate to this section.

---

You are building a production-quality web application for Liwa University (Abu Dhabi, UAE), in one continuous run. Read this whole brief first. Do not stop for approval between stages: every decision that needed a human has already been made and is written here. If you hit something this brief does not cover, pick the most reasonable option, record it in `docs/assumptions.md`, and keep going. Commit after each stage with a clear message. At the end, produce the handover summary described in §14.

---

## 1. The problem

Every semester, students rush to register for courses, choose sections by guesswork, and are then told by their academic advisor that the choices are wrong: missing prerequisites, courses not in their degree plan, time clashes, or an order that delays graduation. The first weeks of every semester are wasted for both sides.

## 2. What we're building

A web app where:

1. A **student** logs in. The system already knows their program, degree plan, completed courses, grades, standing, credits earned.
2. The student says **when they are free** (presets or a weekly grid) plus optional preferences (max credits, online vs on-campus).
3. The system shows **only the sections they can actually take** this semester: in their plan, prerequisites met, offered this term, inside their availability, no clashes, within credit limits.
4. It builds the **shortest path to graduation** — a semester-by-semester plan that minimises remaining semesters given prerequisites, offering patterns (Fall-only, Spring-only), and credit caps — and derives this semester's recommended sections from it.
5. The student reviews, adjusts within valid options, and **sends the plan to their advisor**.
6. The **advisor** sees the plan with reasoning and warnings, and approves, rejects with a comment, or proposes changes. The student is notified. Every decision is audit-logged.
7. An **AI explainer** explains the plan in plain language and answers "why can't I take X?" / "what if I skip summer?". It NEVER invents courses or overrides the rule engine; it only explains what the deterministic planner produced.

The university may adopt this. Build it as a real system: auth, roles, database, migrations, tests, clean separation so the data source can be swapped. Users are first-year students who do not know registration vocabulary — see §9.

## 3. Hard facts about the university (do not change)

- University: **Liwa University (LU)**, Abu Dhabi and Al Ain. Arabic name **جامعة ليوا**. Website https://lu.ac.ae (Arabic: https://lu.ac.ae/ar/).
- SIS: **Oracle PeopleSoft Campus Solutions** (`https://sis.lu.ac.ae/psp/sisprd/`). LMS: **Blackboard Learn Ultra** (`https://lms.lu.ac.ae/ultra/`). No API access yet.
- Identity: most likely Microsoft Entra ID (the university uses a SharePoint tenant). No app registration yet.
- Colleges: College of Business; College of Engineering and Computing; College of Media and Public Relations; College of Medical and Health Sciences; General Education Department.
- The three programs to seed, with exact titles:
  - **Bachelor of Information Technology (BIT) — General Track** (College of Engineering and Computing), course prefixes ITC/CSC
  - **BBA in Management** (College of Business), prefixes BUS/MGT/ACC/FIN
  - **Bachelor of Mass Communication Concentration in Public Relations** (College of Media and Public Relations), prefix MCM
  Take the official Arabic program and college names from lu.ac.ae/ar.
- **Work week is Monday–Friday.** The UAE moved to Mon–Fri in 2022. Sunday is a weekend day — never use Sun/Tue/Thu patterns. Friday afternoon (after 12:00) is never a teaching slot unless the term config overrides it (Jumu'ah). Teaching patterns: Mon/Wed, Tue/Thu, Mon/Wed/Fri mornings, Mon–Thu evening blocks (e.g. 5–8 pm) for working students. Teaching days are configurable per term; Saturday is supported as an optional teaching day but off by default.
- Terms: Fall, Spring, Summer. Current term for the demo: **Spring 2027**.

## 4. Decisions already made (apply all)

- **.NET 10** (LTS) Web API, C#, layered: Domain / Application / Infrastructure / API. EF Core + migrations. **PostgreSQL** installed locally on this machine (no Docker in this phase — do not create Docker files); keep SQL portable (university may prefer SQL Server).
- **Angular 22** + Angular Material 22, standalone components, signals. **Arabic/RTL from day one** (`en` and `ar`).
- Local accounts first (PBKDF2 hashed), roles `Student` / `Advisor` / `Admin`, JWT, server-side authorisation on every endpoint. Auth behind interfaces so SSO (Entra ID via OIDC; SAML alternative) can be configured later. SSO can be configured and tested in Settings but **password sign-in stays on and SSO cannot be the only way in** until an admin explicitly enables it — a settings change must never lock students out.
- Demo users: one shared demo password documented in README (`Liwa#2027`), accounts like `student1@lu.ac.ae`, `advisor1@lu.ac.ae`, `admin@lu.ac.ae`. Demo credentials shown on the login page **only in Development**.
- Credit policy, fully configurable (`CreditPolicy`, seeded from config): min 12 / max 18 in Fall or Spring; max 9 in Summer; max 21 for GPA ≥ 3.5; max 12 on probation (GPA < 2.0). Standing by credits earned: Freshman < 30, Sophomore 30–59, Junior 60–89, Senior ≥ 90. A student whose remaining credits are below the minimum (e.g. a senior with 6 left) is NOT held to the minimum.
- Grading: letters A–F with +/- on a 4.0 scale. Passing is D or above unless a prerequisite specifies a minimum grade. Repeat rule configurable: "latest grade counts" (default) or "highest".
- Transfer credits: status Transferred, satisfy prerequisites and requirements, excluded from GPA.
- In-progress prerequisites: policy flag `AllowInProgressPrerequisites` (default on; per-term override). On → course eligible with a `ConditionalPrerequisite` warning carried into the plan. Off → blocked, "grade must be posted first".
- Summer: planner computes projections with and without Summer; UI shows both; Summer is never assumed.
- Projections ignore sections and use offering patterns only. A course whose pattern says "this term" but has no section raises a `NotScheduled` soft warning.
- Projections assume department permission (capstone, internship) is granted in later terms, with a note saying so. Record this in assumptions.md as a question for the registrar.
- Critical-path depth is computed over the student's own remaining plan courses only.
- Advisor modification creates a new plan version with status **Changes proposed**; the student accepts (→ Approved) or resubmits. Every version is kept.
- Statuses: Draft → Submitted → Under review → Approved / Rejected / Changes proposed; plus **Needs review** when a later grade posting invalidates an approved plan (prerequisite/corequisite failure only — a section filling up after approval does NOT flip a plan).
- Opening a submitted plan marks it Under review. Approve is disabled while current checks show errors; Reject and Propose stay available. Reject requires a comment; Propose requires a note. An advisor cannot propose an invalid plan.
- Notifications: in-app for both sides on every status change (bell with unread count); email stubs generated in the recipient's language and written to the log until a mail server exists.
- Section capacity: full sections excluded; warn at 90%. Keep a `Waitlist` flag on Section, unused.
- One catalog year seeded (2025–2026); multi-year supported in the model.
- Digits: Western (0–9) in both languages, one deployment-wide setting `UI_ARABIC_DIGITS`. Dates Gregorian with Arabic month names. 12-hour times with am/pm in English UI.
- Every catalog edit in admin — even one field — goes through the same importer/validator as a file upload (same validation, preview, error list, catalog version).
- Student uploads are additive (students missing from a file are not deleted).
- Secrets (AI key, SSO client secret) encrypted at rest; UI shows only last four characters.
- AI default model `claude-sonnet-5-5`; timeout 20 s, one retry, then fallback; provider/model/base URL configurable so OpenAI-compatible endpoints work. The mock LLM provider exists for demos but is only offered when the API runs in Development; any other environment rejects it at startup.

## 5. Mock data (there is no real data yet)

Seed from CSV files under `seed/` (this is also the future PeopleSoft import path). Document the formats in `seed/README.md`.

- The three programs above, ~120 credits each, 4-year plans, prerequisite chains at least 4 levels deep, requirement groups (Core / Major electives / General Education / Free electives), some courses Fall-only or Spring-only, and course credits of **1, 2, 3 and 4** (labs, seminars, capstone, internship) — not all 3.
- Machine-drafted Arabic titles for every course (`ArabicTitle`, nullable), listed for native review in `translations/REVIEW.md`.
- Spring 2027 timetable: ~200 sections, multiple sections per popular course, evening blocks, asynchronous online sections, labs, some full and some nearly full, every meeting respecting the Friday-afternoon rule.
- **15 demo students** with transcripts, each mapped to a login: new first-year; evening-only working student; failed prerequisite; transfer credits; probation; graduating senior with 6 credits left; hold on account; ungraded in-progress courses; repeated course; part-time; pending internship permission; corequisite pair; major change; and two mid-programme students. **3 advisors** (one per college) and **1 admin**.
- The planner tests must run against these real seeded plans, not toy data, and give identical output whether the data comes from CSV or from the database.

## 6. Data provider abstraction

All student / catalog / timetable data goes through interfaces (`IStudentRecordProvider`, `ICourseCatalogProvider`, `ITermScheduleProvider`). Implement `DatabaseSisProvider` now. Leave a documented `PeopleSoftSisProvider` stub. Nothing outside the provider layer may know which backend is in use. A config setting chooses the provider.

## 7. Domain model (minimum)

Student, Major/DegreePlan, RequirementGroup, Course, Prerequisite (expression tree), CourseOfferingPattern, Term (teaching days, Friday cutoff, credit policy override), Section (+ Waitlist flag), MeetingTime (end-exclusive clash rule), StudentCourseHistory, AvailabilityWindow + StudentPreferences, RegistrationPlan (versions, status, selected sections, planner explanation, graduation projection), AdvisorReview, Notification, CatalogVersion, AuditLog.

**Prerequisite expression tree** supports AND, OR, MinGrade(course, grade), Corequisite(course), Standing(level), CreditsEarned(n), DepartmentPermission. Text format with parser and canonical printer, e.g. `CSC101 AND (MTH102 OR MTH110:C)`, so prerequisites load from CSV. Seed validation catches unknown courses, prerequisite cycles, duplicate plan rows and wrong credit totals.

## 8. The planner (the most important code — deterministic, pure, tested)

A pure function: no DB, no clock; current term and today's date are inputs; same input → identical output. Used both to build recommendations and to re-validate every submitted plan on the server (never trust client filtering).

Write `docs/planner.md` first (student state → remaining requirements → prerequisite graph with critical-path depth → eligibility → greedy priority-ordered packing under credit policy, offering patterns and availability → term-by-term projection with and without Summer → selection validation with an issue table), then implement to it.

Outputs:
1. **Eligible courses** with reason codes, and **ineligible courses** with the specific code (PrerequisiteMissing, NotInPlan, NotOffered, NoSectionFitsAvailability, SectionFull, StandingRequired, CreditsRequired, DepartmentPermission, NotScheduled…) and parameters.
2. **Recommended section set**: clash-free, inside availability, within credit range, priority = critical path (courses unlocking the most downstream courses first). A **decision log** records priority order, the chosen section, and every alternative with why it was rejected or ranked lower.
3. **Graduation projection**: minimum remaining semesters and sequence; also the projection for the student's actual selection, with the delta ("this choice delays graduation by one semester"). Near the end, fill up to the term max rather than leaving a 3-credit extra term.
4. **Warnings** as codes + parameters (OverCreditLimit, BelowMinimum, FallOnlyMissed, NearlyFull, ConditionalPrerequisite, NotScheduled, CorequisiteMissing…).
5. A structured, serialisable explanation consumed by the UI and the AI explainer.

**All reasons and warnings are codes + parameters, never English prose from the server**, so the frontend renders them in the user's language.

**Required unit tests** (all must pass): C satisfies a C minimum and C- does not; corequisite satisfied by same-term selection; back-to-back sections (10:00–10:50 and 10:50–11:40) are NOT a clash; one-minute overlap IS a clash; asynchronous online section fits any availability; Fall-only course missed now adds a full year; probation cap of 12 applied and a 15-credit selection fails; GPA exactly 3.5 gets 21 and 3.35 gets 18; corequisite pair where only one section fits is rejected as a pair with a clear issue, and selecting one alone is CorequisiteMissing; senior with 6 credits left is not forced to the 12 minimum; each seeded program graduates a fresh student in 8 terms; in-progress flag on and off; NotScheduled warning; holds; "section 02 beat 01 because of seats left" appears in the decision log; determinism.

## 9. Design and usability (apply from the first screen, not as a later pass)

**Brand.** The colours are already known: indigo `#3d3d5f` for header/headings, gold `#f7bf0a` for buttons/active states (from lu.ac.ae's stylesheets; the logo pixels are `#3b3a59`). Logo files: the white wordmark https://lu.ac.ae/wp-content/uploads/2025/05/liwa-university-scaled.png (use unmodified, only on indigo backgrounds) and the icon https://lu.ac.ae/wp-content/uploads/2025/05/cropped-icon-300x300-C-270x270.png (indigo shield for light backgrounds and favicon). Download them (plain image files — allowed by §0) into `frontend/src/assets/brand/`, unmodified; record source URLs and checksums in `BRAND.md`. If lu.ac.ae's browser check blocks a plain fetch, ask me for the files instead of working around it. Fonts: the site uses Frutiger, which is licensed — use **Open Sans** (also Blackboard Ultra's font) + **Noto Sans Arabic**, self-hosted via the npm packages `@fontsource/open-sans` and `@fontsource/noto-sans-arabic` (a normal `npm install`, allowed). Official Arabic names: fetch https://lu.ac.ae/ar/ (plain HTML, allowed) for the Arabic university, program and college names; if blocked, use جامعة ليوا and ask me for the rest. Record sources and checksums in `frontend/src/assets/brand/BRAND.md`. For structure, read Oracle's public PeopleSoft Fluid docs and Anthology's Blackboard Ultra docs (HTML pages, allowed) and write `design/reference/portal-patterns.md` on what we take (Ultra's left rail; Fluid's header/account bar) and what we deliberately don't copy. The goal is to look like Liwa and feel familiar, not to clone a vendor UI.

**Shell:** indigo top bar with a gold rule — logo, page title, term selector, notifications bell, EN/AR toggle, user menu. Collapsible left rail per role (student: Home, Availability, Planner, Graduation Path, My Plan, Messages). Phone: rail becomes a bottom bar + drawer. Material 3 theme as CSS custom properties, light and dark. Login page: brand panel with the university name in English and Arabic, "Sign in with university account" as a primary button that routes to the email form until SSO is enabled.

**Shared components** in `frontend/src/app/shared/`, documented in `docs/ui.md`: page header, stat card, status chip (fixed colours per status), empty state, confirm dialog, weekly timetable grid (shows clashes, online courses, blocked Friday afternoons; doubles as the availability editor).

**Usability rules — the users are 17-year-olds on their first day. Every string must pass that test:**
- Planner first visit: build the recommended plan automatically; main content is "Here is the plan we recommend for Spring 2027" + the timetable + one primary button **"Send to my advisor"**. The full course list sits behind a secondary "Change courses" action that opens the two-panel view (left: eligible courses grouped by requirement group, each with code, title, credits, a gold critical-path badge and a plain-language "Why?"; right: timetable with clash highlighting and a credit counter against the student's limits). Drafts autosave.
- Plain-language message for every reason/warning code, in the i18n files: PrerequisiteMissing → "You need to pass {courses} first"; Conditional → "You can take this if you pass {course} this semester"; CriticalPath → "Take this soon — {n} other courses depend on it"; Corequisite → "Must be taken together with {course}"; NotOffered → "Not offered this semester"; SectionFull → "All seats are taken"; NoFit → "No class times match when you're free". Technical ranking detail (priority, depth, seat comparison) sits under a "Details" link for students and is open by default for advisors.
- "Not eligible this semester" is its own collapsed panel grouped by reason with counts.
- Availability first run: if the student has no saved availability, show four preset cards (Full-time daytime / Evenings only / Weekends + evenings / Custom) with one line each; the grid appears after a choice. Copy: "Pick the times you're free, or skip this to see every class."
- Checks box shows problems before submit as actionable sentences ("Add 9 more credits to reach the 12-credit minimum, or ask your advisor to approve a lighter load"). The submit dialog requires an "I understand" checkbox per warning; errors block.
- Graduation Path: terms as columns, courses as cards, gold = critical path with the legend as a sentence ("Gold courses must be taken on time, or you graduate later"); Summer toggle; "My selection / Recommended" toggle showing both graduation dates inline when they differ; banner when the selection delays graduation; a "Why some courses wait" list.
- My Plan: status tracker with timestamps and who acted; advisor notes; proposed changes as a diff with Accept; every version; full history.
- Phone: replace the hour grid with a per-day list of selected classes ("Tue · ITC201 5:00–6:15 pm"); week grid behind a toggle.
- Vocabulary: "semester" not "term" in student text; "3 credits" not "3 cr"; standing with year ("Sophomore (2nd year)").
- Dismissible one-time walkthrough on first student login (set free times → review recommended plan → send to advisor), stored per user.
- Accessibility: WCAG 2.2 AA contrast, visible focus, keyboard-navigable rail and dialogs, Arabic labels on icon-only buttons. Course codes on Arabic lines use bidi isolation (isolate only the code, not code + title).

## 10. Advisor portal

- Home: counts (awaiting review, needs review after grade posting, approved this term) and a "Needs attention" list.
- Queue: own students' plans, filter by status and program, three sort orders, counts on filters and the nav item, urgency markers (acknowledged warnings, probation, hold, within two terms of graduation). Cards on phone.
- One-page review, no tabs: left = student summary (program, standing, credits, GPA, holds, availability mini-grid); centre = submitted sections on the timetable + course list with the planner's reasons and the student's acknowledged warnings + today's checks; right = graduation projection for this selection vs the recommendation with the delta. Bottom: Approve (optional note), Reject (comment required), Propose changes (opens the same planner the student uses, run on the student's record, note required; invalid plans cannot be sent).
- Advisees list (standing, GPA, plan status, projected graduation, last activity) → read-only student record with transcript and plan history.
- Audit: every status change logged with plan version before/after; plan history shows "Version 1 → 2" with classes added/removed, visible to both sides.
- Grade posting (and a whole-term re-check endpoint) re-validates approved plans; prerequisite/corequisite failures flip the plan to Needs review and notify both sides.

## 11. Admin

Denser screens for registrar staff, same shell and components:
- **Catalog**: courses, degree plans, programs, terms, sections — row-by-row editing through the importer; prerequisite editor with live rule check (unknown courses, cycles); credit-rule test that lists every student whose limits, standing or probation would change.
- **Imports**: catalog files and student/transcript upload; validate → preview (dry run with added/updated/unchanged/removed per entity) → apply; errors listed by file and line as HTTP 422; whole file rejected on any error; idempotent re-import; refuse removing a degree plan still assigned to students; exports (single file or zip) that round-trip; `CatalogVersion` history (number, source, time, who, row counts), visible to advisors.
- **Users**: staff accounts, passwords, bulk advisor reassignment with notification.
- **Settings**: university sign-in (Entra ID / SAML) and AI provider, each with "Test connection"; credit rules; AI usage page (daily requests, tokens, estimated cost, recent fallbacks and discards).

## 12. AI explainer

- **Grounding enforced in code, not in the prompt.** The payload is built only from planner output plus catalog entries for courses that appear in it. It is refused at the service boundary if it contains a name, university ID, email or advisor name; refer to "you"/"the student". Questions are stripped of those first.
- **Reply checks**: discard any reply naming a course code not in the payload, contradicting a reason code, or in the wrong language; show the fixed fallback "I can't explain that part — ask your advisor"; log every discard.
- Three entry points: "Explain my plan" (4–6 sentences a first-year understands); "Why not this course?" from any ineligible row; a question box limited to the plan. What-if questions ("skip summer", "drop STA201") re-run the pure planner with the modified input; the model explains only the diff and the planner's own figures are shown under the answer. Unrecognised questions are classified by one model call into a fixed set (drop / add / summer / why) and the code checks the result; anything the planner can't compute is declined.
- Language follows the UI. Fully optional: no key → panel shows "Not available" and nothing else changes. Per-user hourly rate limit; cache identical plan+question pairs.
- Advisors get the same explainer on the review page plus a "draft note to student" they edit before sending — never auto-sent.
- Tests: grounding filter (PII stripped, unknown code rejected, contradiction rejected), what-if re-run, mocked provider for timeout, retry, fallback, cache, rate limit.

## 13. Infrastructure, tests, checks

- **No Docker.** Run against the locally installed PostgreSQL (ask me for the connection details if they are not in `.env`; default database `liwa`, user `liwa`). API on port 5080, Angular dev server on 4200 with a proxy config to the API so the app is same-origin. `.env.example` / `appsettings` with every value explained. Provide `scripts/run-dev.ps1` that starts API and frontend together, and `scripts/create-db.sql` that creates the database and user.
- Demo seeder runs only when `DemoSeed:Enabled` is true (on in Development, off otherwise).
- `scripts/verify_install.py`: drops nothing, but from a fresh database checks migrations apply in order, seed loads, every demo account signs in, the planner runs for all 15 students, and advisor/admin pages load. Run it after the final stage from a **clean install** (drop and recreate the database, run migrations and seed).
- Tests: planner unit tests (§8), prerequisite parser/evaluator, clash and availability logic, credit policy, importer (idempotency, rejection, dry run), explainer grounding, and API integration tests that boot the real app on a throwaway Postgres container. Frontend unit tests for the shared components and the planner view.
- **No screenshot routine and no Playwright in this phase.** Instead: a contrast check script over the theme tokens (all pairs AA), and frontend unit tests for RTL rendering of the shared components (course codes isolated on Arabic lines). Provide `scripts/demo_plan_states.py` to put demo plans into each status for manual review.
- Never send PII to any external service. Keep secrets out of the repo.

## 14. Documentation and handover (final stage)

- `README.md` for a reader arriving cold, linking everything below.
- `docs/planner.md`, `docs/ui.md`, `docs/explainer.md`, `docs/CHANGELOG.md`.
- `docs/INSTALL.md`: registrar-IT runbook for a plain server install (Windows Server or Ubuntu): PostgreSQL, .NET 10 runtime, the published API as a service, the built Angular app behind IIS or nginx, every config value, TLS via a reverse proxy example, backups (pg_dump schedule + restore test), upgrade (publish, migrate, verify_install.py), logs. Note that a Docker packaging can be added later.
- `docs/peoplesoft-integration.md`: for each path (Integration Broker REST, Component Interface via middleware, PS Query CSV export) the exact fields needed from which records (student, program/plan, term, class schedule, enrollment/transcript), mapped to our CSV columns; what `PeopleSoftSisProvider` must implement; a sample PS Query export spec the university can hand to its team.
- `docs/sso.md`: Entra ID app-registration steps, exact redirect URI, required claims (upn/email, oid), claim → student mapping; SAML alternative in one paragraph.
- `docs/security.md`: roles and authorisation, secret storage, what goes to the LLM and what never does, audit log, data retention, explainer grounding and discard rules — in plain language for a non-technical reviewer.
- `docs/assumptions.md`: a checklist of questions for the registrar, each with our current default and a blank for their answer (Saturday teaching? real credit limits? in-progress prerequisites? department permission assumption? repeat rule? etc.).
- `docs/demo-script.md`: a 10-minute faculty walkthrough — accounts in order, what to click, what to say, and the three "wow" moments (recommended plan in one click; graduation-path delay banner; advisor proposes changes and the student sees the diff).
- `translations/REVIEW.md`: every machine-drafted Arabic string for native review.
- Tag `v0.1.0`, release notes in CHANGELOG, `dotnet publish` and `ng build --configuration production` into `dist/`, run `verify_install.py` against the published build.

## 15. Build order (continuous — do not pause between stages)

1. Scaffold: solution, Angular app, local PostgreSQL connection, `run-dev.ps1`, auth with roles, seeded demo users, hello end-to-end, first tests. Commit.
2. Brand research + shell + shared components (incl. timetable grid) + login + role home pages. Commit.
3. Domain model, migrations, prerequisite tree + parser, credit policy, seed CSVs for the three programs, loader with validation. Commit.
4. `docs/planner.md`, planner DTOs, pure planner, all required tests green against seeded data. Commit.
5. Database persistence through the importer, idempotent import/export, catalog versions, 15 demo students + timetable, provider interfaces. Commit.
6. Student flow: availability, planner (recommended-first), graduation path, submit with server re-validation, My Plan, notifications, grade-posting re-check, first-login walkthrough. Commit.
7. Advisor portal. Commit.
8. Admin. Commit.
9. AI explainer + Arabic completion (all codes rendered in both languages; tests fail if any code lacks a translation; Arabic course titles). Commit.
10. Contrast and RTL checks, full test run, fix what they find. Commit.
11. Clean-install verification, docs, tag v0.1.0. Commit.

Finish with a handover message: what runs where, demo accounts, test counts, check results, every decision you made that is not in this brief, and known gaps.
