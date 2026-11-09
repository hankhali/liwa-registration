# Assumptions register

Each item is something we assumed or decided without confirmation. The **Confirm with** column says who should check it. Update an item's status when it is confirmed or changed.

Status key: **Open** = needs confirmation · **Decided** = project decision, change on request · **Confirmed** = agreed with the university.

## Platform and tooling

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| A1 | Backend targets **.NET 10** (latest LTS installed: SDK 10.0.302). | The brief asks for the latest LTS. | University IT (hosting) | Decided |
| A2 | Frontend is **Angular 22** with Angular Material 22, on Node 26 LTS. | The brief asks for the latest Angular. | — | Decided |
| A3 | On the development machine, **Docker Engine runs inside WSL Ubuntu** instead of Docker Desktop. The code and the dev servers still run natively on Windows. | Docker Desktop needs admin rights, which this machine doesn't have. Nothing in the project depends on this, so it works unchanged with Docker Desktop. | — | Decided |
| A4 | UI language switches **in the running app** (Transloco) rather than through Angular's compile-time i18n. | One build serves both English and Arabic, and the language can change without reloading the page. | — | Decided |
| A5 | Fonts (Open Sans, Noto Sans Arabic) and icons are **self-hosted**, not loaded from Google's CDN. | Lets a strict Content-Security-Policy work, needs no third-party requests, and works on locked-down campus networks. | University IT | Decided |
| A6 | Ports: Postgres **5433**, API dev **5080**, Angular dev **4200**, full stack **8088**. | All were checked as free on this machine. 5433 avoids clashing with a local Postgres. | — | Decided |
| A7 | Production images: PostgreSQL **17**, nginx serving the built app, and the API on the .NET 10 ASP.NET runtime image. | Current supported versions. | University IT | Open |

## Authentication and security

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| B1 | Local sign-in uses a **username** (for example `student.new`), not an email or university ID. | Demo accounts have no email addresses. SSO will replace this anyway. | Registrar / IT | Open |
| B2 | A sign-in lasts **8 hours** (`Jwt:LifetimeMinutes` = 480), with no refresh token. | Covers a working day of registration. Configurable. | University IT | Open |
| B3 | The browser keeps the access token in `localStorage`. | Simple, and works with the bearer interceptor. With SSO (M8), this should move to the identity provider's flow or to secure cookies. | University IT (security) | Open |
| B4 | Sign-in is limited to **10 attempts per minute per IP** (200 in Development, where automated screenshots sign in repeatedly). There is no per-account lockout yet. | Slows down password guessing without locking out shared campus IPs. | University IT (security) | Open |
| B5 | Passwords use PBKDF2-HMAC-SHA256 with **600,000 iterations** (the OWASP 2023 minimum). | Configurable. Each hash records its own iteration count, so the number can be raised later. | — | Decided |
| B6 | Swagger is **off** outside Development. When it is off, `/api/swagger` gets the same "sign-in required" response as any unknown API path. | Don't advertise the API surface in production. | — | Decided |
| B7 | Demo accounts exist only when `DemoSeed:Enabled` is true. It defaults to false, and `.env.example` sets it to true for local Docker only, with a warning comment. | Required by the brief. | — | Decided |
| B8 | Sign-in uses the **university Microsoft account (Entra ID)**, the same one behind "LU-AZURE" on Liwa's Blackboard sign-in page. It's built and switched off until IT registers the app ([docs/sso.md](sso.md)). The project owner chose this on 2026-11-09 over launching from inside Blackboard (LTI 1.3), which can still be added later. | Students use one account everywhere; we never handle their passwords. | University IT | Open |
| B9 | On first SSO sign-in, people are matched to their Smart Registration account by **university email** (the `email` claim, then `preferred_username`). After that, the account is linked permanently to their Entra object ID and is never re-linked to another person. People without an account are refused with a plain message pointing to the Registrar. | Accounts and roles come from registrar data, not from Entra. | Registrar + IT (which email format accounts carry) | Open |
| B10 | Username/password sign-in stays available next to SSO (`SSO_LOCAL_SIGNIN_ENABLED=true`) for demo accounts and emergency admin access. It can be switched off entirely. | Keeps a way in if Entra is unavailable. | University IT (security) | Open |
| B11 | Single-use SSO handoff codes are kept in the API's memory (60 seconds). That's fine for one API instance; several instances need sticky sessions or a shared store. | Simplest safe option for the current single-server deployment. | University IT (hosting) | Open |

## Users and roles

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| C1 | There are three roles: **Student, Advisor, Admin**. Each user has exactly one role. | Enough for the workflows in the brief. A staff member who is both advisor and admin would need two accounts for now. | Registrar | Open |
| C2 | Students see no engineering or status pages. System status (version, environment, database health) is for admins only. | From the brief (M1). | — | Decided |
| C3 | Demo people's names and university-ID formats (for example `202700101`, staff `E1001`) are invented. | Illustrative only, until real data arrives. | Registrar (ID format) | Open |

## Branding

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| D1 | Brand colours are **charcoal `#272727`** and **sage `#b5b59e`**, sampled from the official logo on Liwa's Blackboard sign-in page (lms.lu.ac.ae). Sage is used only on charcoal (7.2:1). On light backgrounds, a deep olive `#6b6a52` is used for accents and focus rings (5.5:1). This **replaces the brief's indigo and gold**, at the project owner's request on 2026-11-09, so the app matches the university's real identity. The main site (www.lu.ac.ae) blocks automated browsers, so it couldn't be checked; exact values should come from the brand guide. | Match Liwa's real look. | University marketing (brand guide) | Open |
| D2 | The app uses the **official Liwa University logo** (shield "LU" mark with the bilingual wordmark, "Est 1993"), taken from the Blackboard sign-in page and converted to transparent PNGs: `logo.png` (full lockup, on the sign-in page), `logo-mark.png` (shield, in the top bar), and `icon-64.png`, `icon-192.png` and `favicon.ico` (shield on charcoal). The source is a 500 px JPEG, so these should be replaced with vector files from marketing. | Approved by the project owner on 2026-11-09. | University marketing (logo files, usage rules) | Open |

## Academic data and rules (M2)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| E1 | **Grade scale** on 4.0: A 4.0, A- 3.7, B+ 3.3, B 3.0, B- 2.7, C+ 2.3, C 2.0, C- 1.7, D+ 1.3, D 1.0, F 0. **P** earns credit without affecting the GPA and meets any minimum-grade prerequisite. **W** never counts. F counts in the GPA and earns no credit. | Common UAE/US scale. | Registrar | Open |
| E2 | GPA is credit-weighted and **rounded to 2 decimals** before it is compared with limits, so 3.495 rounds to 3.50 and gets the high-GPA cap. GPA boundaries include their lower value ("3.50 and above"). | Matches how GPAs are printed on transcripts. | Registrar | Open |
| E3 | **Standing** by credits earned: Freshman under 30, Sophomore 30–59, Junior 60–89, Senior 90+. | Typical for a 120-credit degree. | Registrar | Open |
| E4 | **Default credit limits** (in `appsettings.json`, editable by admins in M6): Summer 0–9 (0–6 on probation); on probation 9–12; GPA 3.50+ 12–21; otherwise 12–18. A student who needs fewer credits than the minimum to graduate isn't forced up to it. | Placeholders from the brief's examples. | Registrar | Open |
| E5 | **Repeated courses:** the latest attempt counts by default; "highest counts" can be chosen in the credit policy. | From the brief. | Registrar | Open |
| E6 | **Term codes** are year + SP/SU/FA (e.g. `2027SP`). Within a year the order is Spring, Summer, Fall. | Readable, and sorts correctly. PeopleSoft `STRM` values will be mapped to this format. | Registrar / IT | Open |
| E7 | **The demo data is illustrative.** The three program names are real; their courses, prerequisites, degree plans (120 credits each), the Spring 2027 timetable and all students are invented. Everything can be regenerated with `scripts/generate-demo-data.py`. | Until the Registrar supplies real data. | Registrar | Open |
| E8 | All demo students follow **catalog year 2024**. | One plan per program keeps the demo simple; the model supports a plan per catalog year. | — | Decided |
| E9 | The demo **Summer 2027** term teaches Monday–Thursday. | Shows that teaching days are set per term. | Registrar | Open |
| E10 | **Imports never delete.** Courses and sections missing from a later file stay. A transcript file replaces the full history of each student in it; a degree-plan file replaces the whole plan for each program + catalog year in it. | Deleting by omission is risky (a partial export would wipe data). Removal will be an explicit admin action. | Registrar / IT | Open |
| E11 | Students see their own **GPA, year of study, progress and advisor** on their home page. Students on probation see a short, supportive note saying they can take fewer credits and their advisor can help. | Plain, useful context before planning. | Student Affairs (wording) | Open |
| E12 | Courses in progress count toward prerequisites "with a condition" by default (`AllowInProgressPrerequisites`), as the brief says. The planner (M3) will carry this as a warning. | From the brief. | Registrar | Decided |

## Planner (M3)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| P1 | When several sections of a course fit, the planner picks the one with the **most seats left** (then the lowest section code). Advisors see why the others lost. | Spreads students across sections and is easy to explain. | Registrar | Open |
| P2 | Priority: the **longest chain** of courses that depend on a course first, then how many courses it **unlocks**, then required before elective, then courses offered in **fewer seasons**, then the suggested semester. | Minimises semesters to graduation (brief §8). | Academic advisors | Open |
| P3 | The planner recommends only **degree-plan courses**. Free electives aren't picked for the student (any course counts), but they are counted in the graduation projection. | The brief says plans contain only courses in the degree plan. | Academic advisors | Open |
| P4 | Graduation projection assumptions: courses in progress are passed; the GPA and probation status stay as they are; department permission (for example for internships) is obtainable later; future terms follow each course's usual seasons (no future timetable exists yet). | A projection needs a stated baseline. | Registrar | Open |
| P5 | **"Without summer"** means no regular summer classes. A summer term is still used for courses offered **only** in summer (such as internships); otherwise a degree that requires one could never be finished. | A strict "never summer" projection would be unreachable for every program with a summer-only internship. | Registrar | Open |
| P6 | Department permission is **not** assumed for the term being planned: a course that needs it shows "needs permission" until it is recorded. | Avoids recommending a course the student can't register for. | Registrar | Open |
| P7 | If no credit rule matches a student, the planner recommends nothing and says why, rather than guessing a limit. | Never invent policy numbers. | — | Decided |

## Student planning screens (M4)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| S1 | Free-time presets: **Any time**; **Evenings only** (Mon–Thu 5–8 pm, the brief's example); **Mornings only** (Mon–Fri before 12 noon); **Daytime** (Mon–Fri before 5 pm, Friday until 12 noon); **Let me choose** (a grid of morning / afternoon / evening per day; Friday afternoon and evening are never offered). | Big, simple choices for a first-year (brief §9). | Student Affairs | Open |
| S2 | When checking a hand-picked plan, these **block saving**: classes at the same time, the same course twice, a course the student can't take yet, a missing lab or corequisite, a full class, going over the credit limit. These are **allowed, but need "I understand" when sending**: below the minimum, waiting for a grade, a class outside the times the student said they're free. | Errors would make registration fail; warnings are informed choices. | Registrar | Open |
| S3 | Once a plan is sent, the student can't change it until the advisor responds (M5 adds approve / reject / propose changes, after which the student can edit and resend). | Avoids the advisor reviewing a moving target. | Academic advisors | Open |
| S4 | Changing courses shows only courses from the degree plan. Free electives outside the plan aren't offered on this screen yet. | The brief says plans contain only degree-plan courses. | Academic advisors | Open |
| S5 | **Milestone screenshots are no longer captured** (from M4 onward), at the project owner's request on 2026-11-09. Automated tests and the production-build check still run. | Faster milestones. | — | Decided |
