# Assumptions register

Each item is something we assumed or decided without confirmation. The **Confirm with** column says who should check it. Update an item's status when it is confirmed or changed.

Status key: **Open** = needs confirmation · **Decided** = project decision, change on request · **Confirmed** = agreed with the university.

## Questions for the Registrar (checklist)

Each question shows what the app does today. Write the university's answer in the last column; most answers are a
setting (Admin → Settings / Catalog) rather than a code change.

| ✓ | Question | Current default | Answer |
|---|---|---|---|
| [ ] | Do any terms teach on **Saturday**? | No; a term can switch it on (teaching days per term) | |
| [ ] | Is Friday afternoon ever a teaching slot? | Never after 12:00 (Jumu'ah), unless a term allows it | |
| [ ] | What are the real **credit limits**? | Fall/Spring 12–18; GPA ≥ 3.50 → 21; probation 9–12; Summer up to 9 (6 on probation) | |
| [ ] | Are students close to graduating held to the minimum? | No: a student with fewer credits left than the minimum isn't forced up | |
| [ ] | Do **courses in progress** count as prerequisites (with a warning) when planning the next term? | Yes, globally; each term can override | |
| [ ] | Can the projection assume **department permission** (capstone, internship) will be granted later? | Yes, with a note to the student (S11) | |
| [ ] | **Repeated courses:** latest grade or highest grade? | Latest grade counts | |
| [ ] | Passing grade and minimum grades for prerequisites? | D or above passes; a rule can ask for a minimum (e.g. C) | |
| [ ] | Grade scale and GPA rounding? | A 4.0 … F 0, P and W don't count; GPA rounded to 2 decimals (E1, E2) | |
| [ ] | **Standing** thresholds? | Freshman < 30, Sophomore 30–59, Junior 60–89, Senior ≥ 90 credits | |
| [ ] | Is **probation** taken from PeopleSoft, or worked out from the GPA? | From the registrar's status (students file `status`) | |
| [ ] | Do **holds** stop a student sending a plan, or only registering? | Only registering; the plan carries a warning (R1) | |
| [ ] | When should an approved plan go back to the advisor after **grades are posted**? | Only when a prerequisite or corequisite fails (R2) | |
| [ ] | How do grades reach the system: per grade, or a file per term? | Both are supported (R3) | |
| [ ] | Is a **full section** a hard stop, and at what point is it "nearly full"? | Full sections are never recommended; warning at 90% | |
| [ ] | Should the **waitlist** be used? | Not yet (the flag exists, unused) | |
| [ ] | Which **email format** do accounts use for sign-in matching? | The `email` column of the students file must match Entra ID | |
| [ ] | Term code mapping from PeopleSoft `STRM`? | `YYYYSP/SU/FA` (E6); mapping table needed | |
| [ ] | Official Arabic titles of the university, colleges, programs and courses? | Machine drafts in [translations/REVIEW.md](../translations/REVIEW.md) | |
| [ ] | How long should plans, history and AI usage counts be kept? | Plans for the student's studies; AI counts 30 days on screen | |

## Platform and tooling

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| A1 | Backend targets **.NET 10** (latest LTS installed: SDK 10.0.302). | The brief asks for the latest LTS. | University IT (hosting) | Decided |
| A2 | Frontend is **Angular 22** with Angular Material 22, on Node 26 LTS. | The brief asks for the latest Angular. | — | Decided |
| A3 | **No Docker.** Development and tests use the locally installed **PostgreSQL 18** (`localhost:5432`, role and database `liwa`, created by `scripts/create-db.sql`). Integration tests create a uniquely named database per fixture and drop it afterwards. Docker files and the screenshot script were removed on 2026-11-10 (brief §0/§13). A Docker packaging can be added later. | Endpoint security on the development machine kills downloads of images and binaries. | — | Decided |
| A4 | UI language switches **in the running app** (Transloco) rather than through Angular's compile-time i18n. | One build serves both English and Arabic, and the language can change without reloading the page. | — | Decided |
| A5 | Fonts (Open Sans, Noto Sans Arabic) and icons are **self-hosted**, not loaded from Google's CDN. | Lets a strict Content-Security-Policy work, needs no third-party requests, and works on locked-down campus networks. | University IT | Decided |
| A6 | Ports: Postgres **5432** (local service), API dev **5080**, Angular dev **4200** (proxies `/api`, same origin). | Brief §13. | — | Decided |
| A7 | Server install: PostgreSQL 16 or later, the .NET 10 ASP.NET Core runtime, the API as a service on localhost, and the built Angular app behind IIS or nginx ([docs/INSTALL.md](INSTALL.md)). | Brief §14. | University IT | Open |
| A8 | Built projects have **no native `.exe` launcher** (`UseAppHost=false`). Run the API with `dotnet Liwa.Api.dll`. | Application Control on the development machine blocks unsigned local executables. Production is unaffected (services run `dotnet Liwa.Api.dll` anyway). | — | Decided |

## Authentication and security

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| B1 | Local sign-in uses a **username** (for example `student.new`), not an email or university ID. | Demo accounts have no email addresses. SSO will replace this anyway. | Registrar / IT | Open |
| B2 | A sign-in lasts **8 hours** (`Jwt:LifetimeMinutes` = 480), with no refresh token. | Covers a working day of registration. Configurable. | University IT | Open |
| B3 | The browser keeps the access token in `localStorage`. | Simple, and works with the bearer interceptor. With SSO (M8), this should move to the identity provider's flow or to secure cookies. | University IT (security) | Open |
| B4 | Sign-in is limited to **10 attempts per minute per IP** (200 in Development, for repeated manual testing). There is no per-account lockout yet. | Slows down password guessing without locking out shared campus IPs. | University IT (security) | Open |
| B5 | Passwords use PBKDF2-HMAC-SHA256 with **600,000 iterations** (the OWASP 2023 minimum). | Configurable. Each hash records its own iteration count, so the number can be raised later. | — | Decided |
| B6 | Swagger is **off** outside Development. When it is off, `/api/swagger` gets the same "sign-in required" response as any unknown API path. | Don't advertise the API surface in production. | — | Decided |
| B7 | Demo accounts exist only when `DemoSeed:Enabled` is true. It defaults to false and is on only in `appsettings.Development.json`; `.env.example` carries a warning comment. | Required by the brief. | — | Decided |
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
| D1 | Brand colours are the brief's **indigo `#3d3d5f`** (top bar, headings, links) and **gold `#f7bf0a`** (the one primary button per screen, active states, the rule under the top bar), with `#26263d` text on gold. A charcoal/sage palette used briefly on 2026-11-09 was reverted on 2026-11-10 at the project owner's request. | Brief §9. | University marketing (brand guide) | Decided |
| D2 | Logos are the brief's official files, downloaded unmodified from lu.ac.ae on 2026-11-10: the **white wordmark** (only on indigo: top bar and sign-in band) and the **indigo shield** (light backgrounds, favicon, touch icon). Source URLs and checksums are in `web/public/assets/brand/BRAND.md`. The wordmark has no Arabic, so the sign-in band adds جامعة ليوا as text. | Brief §9. | University marketing (vector originals) | Decided |

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
| S5 | **No screenshot routine and no Playwright** (removed 2026-11-10, brief §13). The M1/M2 screenshots stay in `docs/screenshots/` as history. Contrast and RTL are checked by scripts and unit tests instead. | Brief §13; Playwright needs a browser download. | — | Decided |

## Usability and data (audit of M1–M5, 2026-11-10)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| S6 | The four first-run free-time cards are: **Full-time daytime** (Mon–Fri 8 am–5 pm, Friday until noon), **Evenings only** (Mon–Thu 5–8 pm), **Weekends + evenings** (Mon–Thu 5–8 pm, Friday morning, Saturday 8 am–5 pm; Saturday classes only appear in terms that teach on Saturday) and **Custom** (hour-by-hour grid). "Skip" shows every class. | Brief §9. In the UAE the weekend is Saturday–Sunday; there is no Sunday teaching. | Student Affairs | Open |
| S7 | Unsent changes in the planner **autosave as a draft** (not a plan version, not sent to anyone, not audited). "Save for later" still creates a saved version. | Brief §9 ("drafts autosave") without filling the audit log with every click. | — | Decided |
| S8 | **Critical path ("gold") courses** are those on, or one step short of, the longest prerequisite chain left in the student's own plan (chain length ≥ 2). | Brief §8/§9: delaying them delays graduation. | Academic advisors | Open |
| S9 | Section capacity: full sections are never recommended; a section with **90% or more** of its seats taken gets a `NearlyFull` warning. | Brief §4. | Registrar | Decided |
| S10 | `FallOnlyMissed` is raised for **any** single-season course (Fall-only or Spring-only) that is offered and eligible now but isn't in the plan, because skipping it waits a full year. | Brief §8 warning list; the same logic applies to Spring-only courses. | Academic advisors | Open |
| S11 | The graduation projection assumes department permission (capstone, internship) is granted when the time comes, and says so with a `PermissionAssumed` note listing those courses. | Brief §4. **Question for the Registrar:** is that a safe assumption? | Registrar | Open |
| S12 | The planner puts its input in a **canonical order** (catalog and sections by code, each requirement group's courses by code) before planning, so the order rows come back from the database or a file never changes the result. | Brief §5: identical output from CSV and database (tested). | — | Decided |
| S13 | Program titles are the brief's: "Bachelor of Information Technology (BIT) — General Track", "BBA in Management", "Bachelor of Mass Communication Concentration in Public Relations". The Arabic titles are drafts until checked against lu.ac.ae/ar ([translations/REVIEW.md](../translations/REVIEW.md)). | Brief §3. | University marketing / Registrar | Open |
| S14 | 15 demo students cover every story in brief §5, each with a sign-in. "Major change" is a student who took Mass Communication courses before moving to Business; those courses count only as free electives. | Brief §5. | — | Decided |
| S15 | Digits are Western (0–9) in both languages. `UI_ARABIC_DIGITS=true` switches the Arabic UI to Arabic-Indic digits. Dates are Gregorian with Arabic month names. | Brief §4. | Student Affairs | Open |
| S16 | Demo credentials are listed on the sign-in page **only** when the API runs in Development with the demo seed on (`GET /api/auth/demo-accounts` is 404 otherwise). | Brief §4. | — | Decided |

## AI explainer (M7)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| X1 | The explainer is **off** until an administrator enters a provider and key (Settings → AI assistant). Without it the panel says "Not available". | Brief §12: fully optional. | University IT | Decided |
| X2 | Only course codes, titles, credits, the planner's reason codes, limits and graduation terms are sent. No name, ID, email, GPA, grades, probation or hold wording. | Brief §12/§13: no PII to external services. | University IT (security) / Data protection | Open |
| X3 | Default limits: 20 questions per person per hour, 20-second timeout, one retry, 30-minute cache. Cost estimate uses 3 / 15 USD per million input / output tokens (configurable). | Reasonable starting values. | University IT | Open |
| X4 | "Contradiction" checks are deliberately simple: a not-eligible course described as takeable in the same sentence (without "if", "once", "after", "not"), or a graduation term the projection doesn't contain. When in doubt the reply is discarded, not shown. | Safer to fall back than to mislead. | — | Decided |
| X5 | Advisor draft notes are written in the language the advisor's screen is in; the advisor edits them before sending. | Brief §12. | Academic advisors | Open |

## Advisor portal (M5)

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| R1 | A **hold** doesn't stop a student planning or sending a plan. It adds an `AccountHold` warning ("registration waits until it's cleared") that the student confirms, and the advisor sees a hold marker. | The advisor can still help; PeopleSoft enforces the hold at registration. | Registrar | Open |
| R2 | **Needs review** is triggered only when a re-check of an approved plan finds a prerequisite, minimum grade, standing, credits or corequisite problem. Full sections, clashes and credit-limit changes never flip an approved plan. | Brief §4. | Registrar | Decided |
| R3 | Grades arrive either one at a time (`POST /api/admin/grades`) or in a transcripts file; both re-check approved plans for the current term. | Until PeopleSoft pushes grade changes. | Registrar / IT | Open |
| R4 | Every status change notifies **both** people: the other person gets the news, the person who acted gets a short confirmation. | Brief §4 ("in-app for both sides on every status change"). | — | Decided |
| R5 | **Email stubs** are written to the application log, in the recipient's preferred language, with the user id but never the email address. A real mail sender replaces the stub later. | No mail server yet; keep PII out of logs. | University IT | Open |
| R6 | "Close to graduating" (urgency marker) means **two semesters or fewer** left in the recommendation's projection with summer. | Brief §10 ("within two terms of graduation"). | Academic advisors | Open |
