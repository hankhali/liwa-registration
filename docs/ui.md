# UI guide

How the web app is put together, and the shared pieces every screen uses. The users are first-year students who
don't know registration vocabulary, so every string has to pass that test (brief §9).

## Shell

`web/src/app/core/layout/shell.component.*`

| Part | What it does |
|---|---|
| Top bar | Indigo (`--liwa-header`) with the gold rule. White wordmark, the current page's title, the term selector (only the current semester is open for planning), notifications bell with unread count, EN/AR switch, user menu (theme: device / light / dark, sign out). |
| Left rail | One menu per role (`core/layout/nav.ts`). Collapsible to icons only (remembered per browser). Student: Home, Availability, Planner, Graduation Path, My Plan, Messages. Advisor: Home, Plans to review (badge = waiting + needs review), My students, Messages. Admin: Home and the admin areas. |
| Phones (≤ 760 px) | The rail becomes a bottom bar with the first four items and **More**, which opens a drawer with everything (focus trapped, Escape closes). |
| Focus | After every navigation, focus moves to `<main>` (WCAG 2.4.3). A skip link jumps there too. |

## Theme

`web/src/styles.scss`. All colours are CSS custom properties written as `light-dark(<light>, <dark>)`; the Material 3
theme uses `theme-type: color-scheme`, so one attribute (`<html data-theme>`, set by `ThemeService`) switches
everything. Status chips, the gold critical-path badge, the timetable and notices all have their own tokens.
`scripts/check_contrast.py` checks every text/background pair against WCAG AA in both themes.

## Shared components (`web/src/app/shared/`)

| Component | Use |
|---|---|
| `app-page-header` | Page title (h1), one-line subtitle (on student screens: what to do here), and the actions slot on the end side, where the screen's **one gold primary action** goes (top right; full width on phones). |
| `app-stat-card` | A big number with a label, optionally a link (with query params). `attention` adds a warning edge when the number is above zero. |
| `app-status-chip` | A plan status with a **fixed colour and icon per status**, the same on every screen for both roles: Draft, Waiting for review, Being reviewed, Approved, Sent back, Changes suggested, Needs another look. With `[student]="true"` it uses the student names (Sent to advisor, Advisor is reviewing, Sent back with comments, Advisor suggested changes, …); `studentStatus.meaning.*` holds the one-line "what this means for you". |
| `app-empty-state` | "Nothing here yet" with an icon, a sentence and an optional action. |
| `ConfirmService` / `app-confirm-dialog` | A yes/no Material dialog (focus trapped and restored). `danger` makes the confirm button red. |
| `app-timetable-grid` | The weekly grid (see below). |
| `app-plan-editor` | The two-panel planner ("Change courses"): classes the student can take, grouped by requirement, with title, "Take this soon", one reason and a class-time picker, next to the week and a credit counter. Shared by the student planner and the advisor's **Suggest changes** (brief §10). It only edits a selection; the page checks and saves. |
| `app-plan-history` | A plan's history, newest first, with "Version 1 → 2" and classes added/removed. Used by the student (My Plan), the advisor review page and the student record. `[student]="true"` drops version numbers and class codes and shows course titles. |
| `bidi.ts` | `isolate(code)` wraps a course code in Unicode isolates (LRI … PDI). |

### Timetable grid

`<app-timetable-grid [blocks] [online] [days] [free] [editable] [fridayCutoff] [startHour] [endHour]>` (default 8 am–10 pm: Liwa teaches until 10 pm)

- Monday–Friday by default (Saturday when a term or the student's free time includes it; Sunday is never shown).
- Classes are placed by time in **5-minute rows**. Class times such as 10:50 and 4:50 pm are multiples of 5; with 15-minute rows they got fractional row numbers and fell below the grid. Each block shows the code and the title. Overlapping classes on the same day are marked as a **clash** (red outline and icon, plus a sentence above the grid). The clash rule is end-exclusive: 10:00–10:50 and 10:50–11:40 don't clash; a one-minute overlap does.
- Friday after 12:00 is shaded "No classes Friday afternoon" (Jumu'ah) unless the term allows it.
- `free` windows are shaded green. With `editable`, every hour is a toggle button (`aria-pressed`) and day headers select the whole day: this is the free-time editor on the Availability page.
- Online classes with no meetings are listed under the grid.
- On phones there is no hour grid: a per-day list with a heading per day and lines like "5:00–6:15 pm · ITC201 Database Systems".
- The visual grid is hidden from screen readers; they get the same classes as a list.

## Reason codes → sentences

The server never sends prose for planner decisions, only codes and parameters. `ReasonTextPipe`
(`core/planning/reason-text.pipe.ts`) turns them into sentences from `planner.reasons.*`:

- `PrerequisiteNotMet [ITC304, ITC301]` → "You need to pass ITC304 or ITC301 first"
- `WaitingForGrade [CSC201, CSC102]` → "Waiting for a grade: you can take CSC201 if you pass CSC102"
- `UnlocksOtherCourses 5` → "Take this soon: 5 other classes depend on it"
- Every course in a sentence is shown as code **and** title ("ITC201 Database Systems"), from `CourseTitles`
  (`core/planning/course-titles.ts`), which every planning view fills.
- `TakeWithCorequisite`, `NotOfferedThisTerm`, `SectionFull`, `NoSectionFits`, … all have plain versions.

`| reasonText: 'action' : { total }` gives the **actionable** version used in the Checks box, from `planner.actions.*`,
e.g. "Add 9 more credits to reach the 12-credit minimum, or ask your advisor to approve a lighter load."
Every course code inside a sentence is isolated (`isolate()`), and only the code: never code + title.

Technical ranking (priority, chain length, seats compared) sits behind **Details** for students; advisors see it
open on the review page.

## Student screens

| Page | Route | Notes |
|---|---|---|
| Home | `/student` | Studies at a glance, the plan's status and next step, first-login walkthrough (dismissed per user on the server: `POST /api/me/walkthrough/dismiss`). |
| Availability | `/student/availability` | Four preset cards (Full-time daytime, Evenings only, Weekends + evenings, Custom) and "skip this to see every class". The grid editor appears after a choice. Saved on the server (`PUT /api/students/me/preferences`). |
| Planner | `/student/planner` | "Here is the plan we recommend for Spring 2027", the timetable and one primary button **Send to my advisor**. **Change courses** opens two panels: courses you can take grouped by requirement group (code, title, credits, gold "Take on time" badge, **Why?**) and the timetable with clashes and a credit counter. Unsent changes autosave as a draft (`PUT /api/students/me/draft`). "Not eligible this semester" is its own folded panel grouped by reason with counts. Sending asks for "I understand" on each warning; errors block. |
| Graduation Path | `/student/graduation` | Semesters as columns, courses as cards, gold = critical path with the legend as a sentence, a Summer toggle, "My selection / Recommended" with both dates when they differ, a delay banner, and "Why some courses wait". |
| My Plan | `/student/plan` | Status tracker (who and when), advisor notes, proposed changes as a diff with **Accept**, full history with versions. |
| Messages | `/student/messages` | Every notification. Same page for advisors and admins. |

## Vocabulary rules

- "semester", never "term", in anything a student reads.
- "3 credits", never "3 cr". One credit is "1 credit".
- Standing with the year: "Sophomore (2nd year)".
- 12-hour times with am/pm in English (ص/م in Arabic); Western digits in both languages.
- Course codes on Arabic lines are isolated (`<bdi dir="ltr">` in templates, `isolate()` in sentences).

## Tests

`web/src/app/shared/shared-components.spec.ts` covers the shared components, the clash rule, the free-time editor,
RTL rendering (codes isolated on Arabic lines, and only the code) and the reason sentences.
`features/student/plan/student-plan.component.spec.ts` covers the planner view (recommended plan first, one primary
action, gold badge, actionable checks, grouped "not eligible" panel, Arabic).
