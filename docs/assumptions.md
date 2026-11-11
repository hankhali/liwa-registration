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
| [ ] | **Which courses run in Fall, Spring and Summer?** (not in the 2025–2026 catalog) | Every course every term (K1) | |
| [ ] | Is there a **Summer 2**? Which courses, can a student take both summers, and its `STRM`? | Not modelled; one summer a year, named "Summer 1" (K3) | |
| [ ] | How should "Passing 80 hours, including 12 hours of specialization" (PRM410) and "Graduation Semester" (PRM422) be checked? | `CREDITS(80) AND PRM303`; `PRM410 AND CREDITS(105)` (K6) | |
| [ ] | The catalog's own disagreements (descriptions vs curriculum tables, duplicate ENG104, CIT244/CIT246, CIT474/CIT479, GEN301/GEN304) | Curriculum table wins (K7, K8) | |
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
| E7 | **The catalog data is real; the rest of the demo data is illustrative.** Courses, credits, prerequisites, descriptions, the three programs and their plans (123 credits each) come from the official University Catalog 2025–2026 (see "Real catalog data" below). The Spring 2027 timetable, offering patterns, Arabic course titles and all students are invented (one transcript is a real, anonymised record). Everything can be regenerated with `scripts/generate-demo-data.py`. | Real catalog since 2026-11-11. | Registrar | Open |
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
| S13 | Program titles are the catalog's exact names: "Bachelor of Business Administration in Business Information Technology" (replacing the brief's "BBA in Management", at the project owner's request), "Bachelor of Information Technology (BIT) — General Track", "Bachelor of Mass Communication Concentration in Public Relations". The Arabic titles are drafts until checked against lu.ac.ae/ar ([translations/REVIEW.md](../translations/REVIEW.md)). | Brief §3. | University marketing / Registrar | Open |
| S14 | 16 demo students cover every story in brief §5, each with a sign-in, plus `student.hanieh` (a real BBA-BIT record, anonymised). "Major change" is a student who took Mass Communication courses before moving to Business; those courses count only as free electives. | Brief §5. | — | Decided |
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

## Real catalog data (2025-2026)

Source: **Liwa University Catalog 2025–2026**, https://lu.ac.ae/downloadPdfs/University_Catalog_2025_2026.pdf, saved as
`docs/reference/University_Catalog_2025_2026.pdf`. The program curricula and study plans were transcribed by hand into
`scripts/generate-demo-data.py` (PDF pages 130–132, 194–197 and 284–287). The Course Descriptions section (PDF pages
307–400) was extracted by `scripts/extract_catalog.py` into `docs/reference/catalog-2025-2026-courses.json` and used
for descriptions and to cross-check every prerequisite. Page numbers in the `source` columns are PDF pages.

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| K1 | **Offering pattern: every course is offered every term** (Fall, Spring and Summer). The catalog doesn't say which courses run in which semester. Until it is known, "only offered in Fall, so it waits a year" never happens with the real data. | Not in the catalog. | **Registrar: which courses run in Fall / Spring / Summer?** | Open |
| K2 | **Term names follow Liwa's academic-year style**: "Fall 2026-2027", "Spring 2026-2027", "Summer 1 2025-2026" (seen on a real transcript). Term codes stay calendar-year (`2026FA`, `2027SP`, `2026SU`). The current term is Spring 2026-2027 (`2027SP`). | Real transcript. | Registrar | Decided |
| K3 | **A Summer 2 exists** (the transcript says "Summer 1"). It is not modelled yet: the projection plans one summer term a year (shown as "Summer 1"). The app recognises "Summer 2 …" in names from the server; a term code such as `2026S2` would be the next step. | Assumed from "Summer 1". | **Registrar: is there a Summer 2; which courses run; can a student take both summers; what is its PeopleSoft `STRM`?** | Open |
| K4 | **CIT100 or CIT101; GEN100 or GEN101.** The catalog lists them as alternatives (CIT101 replaces CIT100 for students joining from Fall 2025; GEN101 is for non-Arabic speakers). Each pair is a "choose 3 credits" group, and a prerequisite naming one accepts the other (`CIT100 OR CIT101`, `GEN100 OR GEN101`). | Catalog p.130. | Registrar | Decided |
| K5 | **Prerequisites written as credit thresholds**: BIT415 Graduation Project = `CREDITS(99) AND BIT407`; BIT419 Internship = `CREDITS(99)`; CIT483 Internship = `CREDITS(75)`; CIT484 = `CREDITS(90)`; CIT473 = `CREDITS(48)`; CIT481 = `CREDITS(81)`; CIT474/CIT487 = `CIT231 AND CREDITS(81)`; CIT470/CIT477 = `CIT473 AND CREDITS(81)`. | Catalog curriculum tables. | Registrar | Decided |
| K6 | **Prerequisites that can't be written exactly** (approximated): PRM410 Field Training "Passing 80 hours, including 12 hours of specialization courses" → `CREDITS(80) AND PRM303` (the "12 hours of specialization" part is not checked; the description says 78 hours + PRM303). PRM422 Graduation Project "Graduation Semester" / "Graduation Class + PRM410" → `PRM410 AND CREDITS(105)` (at most 18 credits left: one final semester). ENG100 "English proficiency test (EmSAT, IELTS, TOEFL…)" is an admission condition, not a course, so ENG100 has no prerequisite here. | No rule for "specialization hours" or "final semester". | Registrar / College of Media | Open |
| K7 | **Where the curriculum table and the course description disagree, the program's curriculum table wins**: GEN304 Ethics (table ENG100, description "ENG200", which doesn't exist); HRM201 (table none, description BUS100); BUS407 Project Management (BIT table BIT203, description BUS306); CIT473 (table 48 credits, description DSAI310 + DSAI212 from another program); CIT477 (table CIT473 + 81 credits, description DSAI215, CIT473); CIT481 (table 81 credits, description CIT122); MCM201 (table GEN105, description none); MCM204 (table none, description "CIT101A"); PRM302 (table MCM122, description "MCM218", not in the program); PRM303 (table MCM203, description PRM302). | The program table is the one students follow. | Registrar / departments | Open |
| K8 | **Inconsistencies inside the study plans**: BIT Year 2 lists "CIT246 Database Systems" but the core table says CIT244 (CIT244 used). BIT electives list Internet of Things as CIT474 in one table and CIT479 in another (CIT474 used, as in the description). Mass Communication (PR) plan prints "GEN 301 Professional Ethics" where the curriculum has GEN304 Ethics (GEN304 used), lists ENG104 twice (Year 1 and Year 3) and GEN100 twice (one is really "Elective course 1"). **Year 3 total: the plan says "YEAR 3 (30 CH)" but, without the duplicate ENG104, Year 3 has 27 credits; the four years add up to 126 as printed and 123 once the duplicate is removed.** MCM122 (Year 1, Semester 2) needs GEN100, which the plan puts in Year 2. | Catalog p.196, 286–287. | Registrar / departments | Open |
| K9 | **Electives found in the catalog**: BBA-BIT major elective (choose one): BUS421, BAF403, MKT301, BUS411. BIT General Track electives (choose two): CIT474 Internet of Things, CIT487 Cloud and Edge Computing, CIT477 Natural Language Processing, CIT470 Data Mining (CIT352 and CIT481 are electives only for the concentrations; for the General Track they are required track courses). Mass Communication (PR): program elective (choose one of MCM210–MCM213), supporting elective (choose one of MCM125, MCM326–MCM329), and two free courses (any course). | Catalog p.131, 195, 285. | — | Decided |
| K10 | **Mass Communication descriptions are in Arabic** and the PDF's Arabic text extracts with scrambled letters, so MCM/PRM courses have no description in the seed (titles come from the English curriculum table). | Extraction limit. | — | Open |
| K11 | **Arabic course titles are machine-drafted** (flagged in `translations/REVIEW.md`). The catalog has official Arabic titles for the Mass Communication courses, but they extract scrambled too. | Extraction limit. | University marketing | Open |
| K12 | **The real catalog has no lecture/lab pairs, no minimum grades and no department-permission courses**, so the demo stories that used them (lab corequisite pair, "MCM301 needs MCM201 with at least a C", "internship needs permission") now use the real equivalents (MCM202 needs MCM123; internship needs 75 credits). The behaviours are still tested with synthetic data in the planner unit tests. | Real catalog. | — | Decided |
| K13 | **Normal load: 15 credits.** The recommendation aims for the normal load (`CreditPolicy.NormalLoad`, default 15; a credit rule can set its own, e.g. per standing; a term can override it with `normal_load` in the terms file) and never goes above it unless the student chooses more in "Credits to aim for this semester". The rule's maximum (18, or 21 with GPA ≥ 3.50) stays the hard cap. The projection plans the normal load each future semester, **except that a semester that can finish the degree may go up to the maximum** (the catalogs' own final semesters run to 18 credits). Without this, 8 × 15 = 120 < 123 and no program could finish in 8 semesters. With it, all three plans graduate a fresh student in exactly 8 (7 × 15 + 18). | Project owner, 2026-11-11. | **Registrar: normal load per standing / term; may a final semester go up to 18?** | Open |
| K15 | **Limited free time continues in the projection.** Future semesters assume the student keeps their current free time: at most as many classes as it has non-overlapping weekly class times in this semester's timetable (online classes not counted). At Liwa, evenings only means **4 slots**: Mon/Wed and Tue/Thu × two blocks (5:00–6:15 and 6:30–7:45 pm), so at most 4 classes (12 credits) a semester. Graduation then shows both dates: "If you keep evenings only: X. With a full-time schedule: Y." This holds until real future timetables exist. | Project owner, 2026-11-11. | Registrar (evening offering per semester) | Open |
| K16 | **"Take this first"** marks the two highest-ranked planned courses that start a chain of at least two remaining courses, with the chain in one line ("MTH105 → MTH203 → BUS306 → BUS307 & BUS309"). The critical-path ranking is unchanged. | Project owner, 2026-11-11. | — | Decided |
| K17 | PRM422's "Graduation Semester" is now `PRM410 AND CREDITS(105)` (at most 18 credits left), so the final semester can include it (was 108). | Needed for the 8-semester plan at 15 credits. | College of Media | Open |
| K14 | **`student.hanieh`** is a real BBA-BIT transcript supplied by the project owner (catalog 2025–2026): name, ID and email replaced ("Demo Student H", `202600116`, no email). It took the place of the planned `student.aziz` placeholder. Terms mapped: Spring 2025-2026 = `2026SP`, Summer 1 2025-2026 = `2026SU`, Fall 2026-2027 = `2026FA` (in progress). Free time: weekday evenings (Mon–Thu 5–8 pm). | Project owner. | — | Decided |