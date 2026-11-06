# Liwa University Smart Registration Assistant (rebuild)

You are building a production-quality web application for Liwa University (Abu Dhabi, UAE). This is a **rebuild**: an earlier version reached milestone M5 before the machine it lived on failed. This prompt already contains every decision and correction from that build, so do not reopen settled questions. Read all of it before doing anything.

## 0. Set up the machine and project first

The machine is **Windows**. Run natively on Windows (PowerShell). Do not use WSL for this project.

1. **Find the real Desktop path** with `[Environment]::GetFolderPath('Desktop')`. If it is inside OneDrive (e.g. `C:\Users\<me>\OneDrive\Desktop`), stop and tell me before creating anything. OneDrive syncing `node_modules`, `bin` and `obj` causes locked-file errors, so I will choose whether to turn off Desktop backup or use another location.
2. Create the project folder **`Liwa-Registration` on the Desktop**, and work only inside it.
3. **Check the tools** and report versions: Git, the .NET SDK (latest LTS), Node.js LTS + npm, the Angular CLI, and Docker Desktop (running, Linux containers). For anything missing, give me the exact `winget` command to install it and wait for me. Do not silently install system software. If Docker isn't reachable, tell me to start Docker Desktop instead of working around it.
4. Run `git init` and set `core.autocrlf` to false for this repo. Add a `.gitattributes` that forces LF line endings and a `.gitignore` for .NET, Angular, `.env` and OS files.
5. Save this entire prompt into the repo as `docs/BRIEF.md` and make it the first commit.
6. **Remote:** ask me for the private GitHub or Azure DevOps repo URL, add it as `origin`, and push the first commit. Push again at the end of every milestone, so a machine failure can never lose the work again.
7. Then restate the plan, list only the questions that are genuinely still open, and wait for my answers before implementing M1.

## 1. The problem

Every semester, students rush to register, guess at sections, and then get told by their academic advisor that their choices are wrong: missing prerequisites, courses outside their degree plan, time clashes, or an order that delays graduation. This wastes the first weeks of the semester for students and advisors.

## 2. What we're building

1. A **student** logs in. The system knows their major, degree plan, completed courses, grades, standing and credits earned.
2. The student says **when they are free** (presets like "Evenings only, Mon–Thu 5–8 pm", or a custom grid) and optional preferences (max credits, online vs on-campus).
3. The system **builds a recommended plan automatically**: only sections they can actually take (in their degree plan, prerequisites met, offered this term, inside their availability, no clashes, within credit policy), chosen to **minimise the number of semesters to graduation**.
4. The student can adjust the plan, then **submits it to their advisor**.
5. The **advisor** reviews, then approves, rejects with a comment, or proposes changes. The student accepts or resubmits.
6. An **admin** manages terms, catalog, sections, degree plans, credit policy and data imports.

The university may adopt this for real use. Build it to that standard.

## 3. Context about Liwa University

- SIS is **Oracle PeopleSoft Campus Solutions** (sis.lu.ac.ae, path `psp/sisprd`). LMS is **Blackboard Learn Ultra** (lms.lu.ac.ae). No data or API access yet; the registrar will provide data later.
- Colleges: Business; Engineering and Computing; Media and Public Relations; Medical and Health Sciences.
- **Seed with three real program names** (illustrative degree plans until the registrar supplies real ones):
  - Bachelor of Information Technology (Engineering and Computing), prefixes ITC/CSC
  - BBA in Management (Business), prefixes BUS/MGT/ACC/FIN
  - Bachelor of Mass Communication (Media and PR), prefix MCM
  - 120 credits each. Most courses are 3 credits, but include some 1, 2 and 4-credit courses (labs, capstone, internship) so credit edge cases get exercised.
- Branding: indigo and gold, Open Sans, visually close to Blackboard Ultra. Use a placeholder logo slot (`assets/brand/logo.svg`) that I will replace with the official logo.

## 4. Calendar rules (UAE)

- The UAE work week is **Monday–Friday**. Sunday is a weekend day. **Never** generate Sun/Tue/Thu patterns.
- Patterns: Mon/Wed, Tue/Thu, Mon/Wed/Fri (mornings only), and Mon–Thu evening blocks (e.g. 5–8 pm) for working students.
- **Friday after 12:00 is never a teaching slot** (Jumu'ah) unless term config overrides it.
- Teaching days are configurable per term (`TeachingDays`). Saturday is supported as an optional teaching day (to be confirmed with the university).
- Terms: Fall, Spring, Summer. Current demo term: Spring 2027.
- Show times in **12-hour format** (5:00 pm), never 24-hour, in the UI.

## 5. Tech stack

- **Backend:** .NET (use the latest LTS SDK installed on this machine; tell me which one), clean architecture with four projects: `Liwa.Domain`, `Liwa.Application`, `Liwa.Infrastructure` (EF Core + PostgreSQL), `Liwa.Api` (controllers, JWT, role policies, problem-details errors, health endpoint, Swagger).
- **Frontend:** latest Angular + Angular Material, standalone components, signals. Auth state in one service, bearer interceptor that signs out on 401, route guards by role.
- **Database:** PostgreSQL.
- **Docker:** `docker-compose.yml` with db, api and web; nginx in the web image proxies `/api` so the app is same-origin. Settings from `.env` (git-ignored) with a committed `.env.example`.
- Check which ports are free before choosing them, and record them in the README.
- **Windows specifics:** Docker Desktop (WSL2 backend) runs Postgres and the integration tests' throwaway containers. Run the API and Angular dev servers as native Windows processes. Any helper scripts go in `scripts/` as PowerShell (`.ps1`), e.g. `scripts/dev.ps1` to start db + API + web in one command and `scripts/reset-db.ps1`. The README's run instructions are written for Windows/PowerShell.
- **Arabic and RTL from the start** (i18n for all UI strings, RTL layout verified).
- Accessibility: WCAG AA contrast, keyboard navigation, no horizontal scrolling on phones.

## 6. Authentication

- Local accounts now (PBKDF2 hashing, JWT), behind interfaces so SSO can be added without changing callers.
- Future login is SSO via the university identity provider (likely Microsoft Entra ID), with PeopleSoft supplying the student ID. Write `docs/sso.md` describing what university IT would need to register.
- **Never** collect or replay students' SIS passwords, and never scrape the SIS.
- Demo seeder runs only when `DemoSeed:Enabled` is true (on in Development, off elsewhere). Demo accounts: students, advisors, an admin, all with password `Liwa#2027`. Seed varied students: a new first-year, a student on probation, a high-GPA student, a graduating senior with 6 credits left, and a student with a failed prerequisite.

## 7. Data and integration

- Domain: Course, Section (meeting times, capacity, `Waitlist` flag unused for now), Term, Program, DegreePlan (requirements, electives, catalog year), StudentRecord (transcript, in-progress courses, transfer credits), CreditPolicy, Plan (versioned), AuditLog, Notification.
- **CSV import pipeline** with a documented contract for catalog, sections, degree plans and student records. A PeopleSoft query export is the most likely first data source.
- SIS access goes through a provider interface: a mock provider now, a PeopleSoft adapter later. Write `docs/peoplesoft-integration.md` covering the realistic routes (Integration Broker web services, Component Interfaces, scheduled query exports).

## 8. The planner (most important part)

- **Deterministic code, not an LLM.** The planner decides what is valid; AI only explains.
- **Pure:** no database, no clock. The current term and today's date are inputs.
- Write the input/output DTOs and `docs/planner.md` **before** implementing.
- Algorithm: topological sort on the prerequisite graph, critical-path priority (courses that unlock the most go first), greedy per-term packing under credit policy and offering patterns, clash check on meeting times. Record the priority order and rejected alternatives in the output so an advisor can see why section A beat section B.
- **Prerequisite expressions:** AND, OR, MinGrade(course, grade), Corequisite(course), Standing(level), CreditsEarned(n), DepartmentPermission. A text format with a parser, e.g. `CSC101 AND (MTH102 OR MTH110:C)`, so prerequisites can be seeded from CSV.
- **CreditPolicy is fully configurable** (per term type, standing, GPA band, probation). Seed sensible defaults from config, never hardcode numbers. I will confirm the real values with the registrar.
- Repeat-grade rule configurable: "latest counts" (default) or "highest counts".
- **In-progress prerequisites:** "eligible with condition" by default, controlled by `AllowInProgressPrerequisites`. Carry the condition into the plan as a warning. When grades post, re-run validation; a failed prerequisite moves an approved plan to `NeedsReview` instead of leaving it standing.
- Projections use offering patterns. If a course's pattern says it's offered this term but it has zero sections in the timetable, add a soft warning.
- Students cannot take two sections of the same course in one term.
- Graduation projection both with and without summer terms.
- **Required tests** (in addition to normal coverage):
  - A Fall-only course correctly adds a year when missed.
  - The probation credit cap is applied.
  - A student exactly at the GPA 3.5 boundary gets the 21-credit cap.
  - A corequisite pair where only one section fits availability is rejected as a pair with a clear issue.
  - A graduating senior with 6 credits left is **not** forced to the 12-credit minimum.

## 9. Student experience (write for a non-technical first-year)

These are lessons from the first build. Apply them from the start:

- On first visit, **build and show the recommended plan automatically**: "Here's the plan we recommend. Happy with it? Send it to your advisor." The full course list sits behind a "Change courses" action. Submit becomes the primary button only once a plan exists.
- **No jargon.** Every planner reason code maps to a plain sentence, e.g. "You need to pass ITC304 or ITC301 first", "Take this soon — other courses depend on it", "Waiting for a grade — you can take this if you pass CSC102". Ranking details (critical-path depth, seat comparisons) are advisor-only.
- "Not eligible" courses are grouped by reason ("Need to pass another course first (14)", "Not offered this term (5)") in a collapsed panel.
- Availability: lead with big preset cards ("I work during the day — evenings only"), and show the grid only for Custom. Empty-state copy: "Pick when you're free, or skip this and we'll show everything."
- On phones, the timetable is a simple list ("Tue — ITC201, 5:00–6:15 pm"), not an hour grid.
- Show warnings **while** the student builds the plan, not only at submit. At submit, each remaining warning needs an "I understand" confirmation.

## 10. Advisor and admin

- **Advisor:** review queue with status/program filters, sort orders, counts and urgency markers (cards on phones). One-page review with student summary, availability, timetable, each course with the planner's reason (details open by default), confirmed warnings, live checks, and graduation for this plan vs the recommendation. Decisions: approve (optional note, disabled while live checks show errors), reject (required comment), propose changes (opens the same planner on the student's record; note required). Opening a newly sent plan marks it "Advisor reviewing". Advisee list and read-only student record with transcript. Advisor home with a "Needs attention" list.
- **Audit and history:** every status change logged with plan versions before and after; plan history shows added and removed classes, visible to both sides. Every status change notifies the other side.
- **Admin:** terms, teaching days, credit policy, catalog, sections, degree plans, CSV import with validation report and dry-run, export.

## 11. AI explainer

- Explains the planner's output in plain language and answers "why can't I take X?". It never decides eligibility.
- Anthropic by default; provider and base URL configurable (to allow a regional or self-hosted endpoint for data residency later).
- **Never send personal data** (name, university ID, email) to the model. Send only the planner's structured output, referring to "the student".
- Explanations available in English and Arabic.

## 12. How to work

1. Do section 0 first, then restate the plan and list open questions. Wait for my answers.
2. Build in milestones, stopping at the end of each one with a handover: what was built, how to run it, test results, decisions I may want to change, known gaps.
3. Commit in small, meaningful steps, and **push to `origin` at the end of every milestone**.
4. Keep `README.md`, `CHANGELOG.md` and `docs/assumptions.md` (every assumption, so I can confirm it with the university) up to date.
5. At the end of each UI milestone, capture screenshots of every changed screen in English and Arabic, at desktop and phone widths, into `docs/screenshots/<milestone>/` with an `index.html` contact sheet, and run an automated accessibility check.

**Milestones:**
- **M1:** scaffold, Docker, auth, role-based landing pages (no engineering/status pages for students)
- **M2:** domain model, migrations, seed data, CSV import contract
- **M3:** planner contract, `docs/planner.md`, implementation and full tests
- **M4:** student UI (section 9)
- **M5:** advisor portal, audit, notifications
- **M6:** admin panel and import/export
- **M7:** AI explainer and full Arabic polish (including advisor-facing planner text)
- **M8:** PeopleSoft adapter notes, SSO notes, deployment docs, and a one-page summary I can give the university's IT and registrar
