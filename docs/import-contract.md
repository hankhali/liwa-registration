# Data import contract (CSV)

**For:** the Registrar's data team and university IT.
**Purpose:** the exact format Smart Registration accepts for terms, the course catalog, programs and degree plans, the timetable, and student records. A scheduled **PeopleSoft query export** saved as CSV is the most likely first source (see the field hints in each table).

Working examples of every file are in [`src/Liwa.Infrastructure/Seeding/Demo/`](../src/Liwa.Infrastructure/Seeding/Demo/). The demo data is loaded through this same import, so those files are guaranteed to be valid.

## How importing works

- **One file per kind.** Upload as an administrator (`POST /api/admin/imports/{kind}`; an upload screen arrives in M6).
- **Check first.** Every upload is a **dry run** unless you choose to save (`dryRun=false`). A dry run lists every problem and what *would* change (created / updated / unchanged) without saving anything.
- **All or nothing.** If a file has *any* error, nothing from it is saved. Warnings don't block saving, but they should be read.
- **Safe to repeat.** Importing the same file again changes nothing ("unchanged"). Rows are matched on their key columns (marked 🔑); a matching row updates the existing record.
- **Nothing is deleted** by an import. A course or section missing from a later file stays as it was.
- **Order matters**, because later files refer to earlier ones: terms → courses → programs → degree plans → sections → students → transcripts.
- Every saved import is written to the audit log (who, when, counts).

## File format

- CSV (comma-separated), **UTF-8** so Arabic works. In Excel, use *Save As → CSV UTF-8*.
- The **first line holds the column names** (any order, any letter case). Unknown columns are ignored with a warning.
- Values may be quoted with `"…"`, which allows commas, quotes (`""`) and line breaks inside a value.
- Blank lines are skipped. A blank value means "not given".
- **Problems are reported by line number** (the header is line 1) and column, in plain language.

Common value formats:

| Type | Format | Examples |
|---|---|---|
| Term code | Year + `SP` / `SU` / `FA` | `2027SP` (Spring 2027), `2026FA` |
| Date | `YYYY-MM-DD` | `2027-01-11` |
| Time | 24-hour `HH:MM` (shown as 12-hour in the app) | `17:00` (5:00 pm) |
| Days | Names separated by `;` (or space), or compact letters | `Mon;Wed`, `Mon Wed Fri`, `MW`, `TR` (R = Thursday) |
| Seasons | Separated by `;` | `Fall;Spring`, `Summer`, `All` |
| Yes/no | `yes`/`no` (also `true`/`false`, `1`/`0`) | `yes` |
| Course code | 2–4 letters + 3 digits, optional letter (`L` for labs) | `ITC201`, `ITC110L` |

---

## 1. `terms` — academic terms

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `term_code` 🔑 | yes | e.g. `2027SP`. Must match `season` and `year`. | `STRM` (mapped to this format) |
| `name` | yes | e.g. `Spring 2027` | `DESCR` |
| `season` | yes | `Fall`, `Spring` or `Summer` | |
| `year` | yes | e.g. `2027` | |
| `start_date`, `end_date` | yes | First and last day | `TERM_BEGIN_DT`, `TERM_END_DT` |
| `teaching_days` | no | Default `Mon;Tue;Wed;Thu;Fri`. **Sunday is never allowed.** Saturday is allowed (with a warning to confirm). | |
| `friday_afternoon` | no | `yes` lets classes run after 12:00 on Friday. Default `no` (Jumu'ah). | |
| `is_current` | no | `yes` for the term students are planning now. Only one term can be current. | |

## 2. `courses` — the catalog

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `course_code` 🔑 | yes | e.g. `ITC201` | `SUBJECT` + `CATALOG_NBR` |
| `title` | yes | English title | `COURSE_TITLE_LONG` |
| `title_ar` | no | Arabic title | |
| `credits` | yes | Whole number 0–6 | `UNITS_MAXIMUM` |
| `department` | no | e.g. `Information Technology` | `ACAD_ORG` |
| `prerequisites` | no | Prerequisite rule (see below) | from requisite groups (`RQRMNT_GROUP`) |
| `offered` | no | Seasons it is normally offered. Default `Fall;Spring`. Used to project future terms (a course offered only in Fall adds a year if missed). | `CRSE_OFFER` / typical-offer |
| `description` | no | | `DESCRLONG` |
| `active` | no | Default `yes` | `EFF_STATUS` |

### Writing prerequisites

| Write | Means |
|---|---|
| `CSC101` | Pass CSC101 (any passing grade) |
| `MTH110:C` | Pass MTH110 with at least a C |
| `COREQ(ITC110L)` | Take ITC110L in the same term (or before) |
| `STANDING(Junior)` | At least Junior standing: `Freshman`, `Sophomore`, `Junior` or `Senior` |
| `CREDITS(90)` | At least 90 credits earned |
| `PERMISSION` | Needs department permission |
| `A AND B`, `A OR B`, `( … )` | Combine. **AND is checked before OR**, so use parentheses when mixing them. |

Examples: `CSC101 AND (MTH102 OR MTH110:C)` · `ITC301 AND STANDING(Senior)` · `CREDITS(90) AND PERMISSION`.

Every course a rule mentions must exist (in the same file or already imported); a course can't require itself. Rules are stored in a tidy standard form, so `itc201 and standing(junior)` becomes `ITC201 AND STANDING(Junior)`.

## 3. `programs` — degree programs

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `program_code` 🔑 | yes | e.g. `BIT` | `ACAD_PLAN` |
| `name` | yes | e.g. `Bachelor of Information Technology` | `DESCR` |
| `name_ar` | no | Arabic name | |
| `college` | no | e.g. `College of Engineering and Computing` | `ACAD_GROUP` |
| `total_credits` | yes | e.g. `120` | |

## 4. `degree-plans` — what each program requires

One row per course in a requirement group. A file **replaces the whole plan** for each program + catalog year it contains.

| Column | Required | Meaning |
|---|---|---|
| `program_code` 🔑 | yes | An existing program |
| `catalog_year` 🔑 | yes | The plan year students follow (the year they entered), e.g. `2024` |
| `group_key` | yes | Short key, e.g. `CORE`, `MAJ-ELEC` |
| `group_name` | no | Shown to people, e.g. `Major electives (choose 15 credits)` |
| `group_type` | yes | `AllOf` (take every course listed) · `ChooseCredits` (choose courses from the list until `group_credits` is reached) · `AnyCourse` (free electives: any course counts; `course_code` may be blank) |
| `group_credits` | for `ChooseCredits`/`AnyCourse` | Credits required from the group |
| `course_code` | yes, except `AnyCourse` | An existing course |
| `suggested_semester` | no | 1–8: when the plan suggests taking it (guidance only) |

Checks: rows of the same group must agree on name, type and credits; a `ChooseCredits` group must list enough courses to reach its credits. A plan that doesn't add up to the program's total is a **warning**.

## 5. `sections` — the timetable

One row per meeting pattern. Several rows with the same key add more meeting times to one section (for example a lecture pattern plus a lab slot).

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `term_code` 🔑 | yes | An existing term | `STRM` |
| `course_code` 🔑 | yes | An existing course | `SUBJECT`+`CATALOG_NBR` |
| `section` 🔑 | yes | e.g. `01`, `E1`, `L1` (max 10 characters) | `CLASS_SECTION` |
| `capacity` | yes | Seats | `ENRL_CAP` |
| `enrolled` | no | Already enrolled (default 0) | `ENRL_TOT` |
| `delivery` | no | `OnCampus` (default), `Online` or `Hybrid` | `INSTRUCTION_MODE` |
| `instructor` | no | | `INSTRUCTOR` |
| `room` | no | | `FACILITY_ID` |
| `days`, `start_time`, `end_time` | together | e.g. `Mon;Wed`, `17:00`, `18:15`. Leave all three blank for online sections with no fixed time. | `MON`…`SUN`, `MEETING_TIME_START/END` |

**UAE calendar checks (errors):** no Sunday classes; no Sunday/Tuesday/Thursday pattern; only the term's teaching days; nothing after 12:00 on Friday unless the term allows it; a section's meetings can't overlap each other; campus and hybrid sections need at least one meeting.
**Warnings:** more enrolled than seats; a section in a season the course isn't normally offered in; a section for an inactive course.

## 6. `students` — student records

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `student_id` 🔑 | yes | University ID | `EMPLID` |
| `display_name` | yes | | `NAME_DISPLAY` |
| `email` | no, but **needed for "Sign in with your Liwa account"** | University email. This is how a student is matched to their Microsoft account on first sign-in (see [sso.md](sso.md)). | `EMAIL_ADDR` |
| `username` | no | Sign-in name for the username form (defaults to the student ID) | |
| `program_code` | yes | An existing program | `ACAD_PLAN` |
| `catalog_year` | yes | Plan year the student follows | `REQ_TERM` (year) |
| `admit_term` | yes | e.g. `2025FA` | `ADMIT_TERM` |
| `status` | no | `Good` (default) or `Probation`, as set by the Registrar | academic standing |
| `advisor_username` | no | Username of an existing advisor | `ADVISOR_ID` (mapped) |

A new student gets an account **without a password**: they sign in with their university account. Matching is by `student_id` first; a username that belongs to a staff account or another student is an error.

## 7. `transcripts` — completed, in-progress and transfer courses

A file **replaces the full history** of every student it contains, so always send everything for a student.

| Column | Required | Meaning | PeopleSoft hint |
|---|---|---|---|
| `student_id` | yes | An existing student | `EMPLID` |
| `course_code` | yes | An existing course | |
| `term_code` | yes, except transfers | e.g. `2026FA` | `STRM` |
| `grade` | for completed courses | `A`, `A-`, `B+`, `B`, `B-`, `C+`, `C`, `C-`, `D+`, `D`, `F`, `P` (pass), `W` (withdrawn) | `CRSE_GRADE_OFF` |
| `status` | no | `Completed`, `InProgress`, `Withdrawn` or `Transfer`. If blank: no grade means in progress; `W` means withdrawn; otherwise completed. | `STDNT_ENRL_STATUS` |
| `credits` | no | Defaults to the course's credits | `UNT_TAKEN` |
| `source` | no | For transfers, e.g. `Advanced Placement (AP English)` | `TRNSFR_SRC_ID` |

Checks: courses in progress have no grade; a course can appear only once per term. Repeated courses (different terms) are kept; which attempt counts follows the credit policy (latest by default).

---

## Questions for the Registrar

1. Can PeopleSoft run a scheduled query export in these formats (or close to them; we can map other column names)?
2. Which term-code format does Liwa use in `STRM`, and how does it map to Fall/Spring/Summer?
3. Is the grade scale above correct, including whether `P` satisfies a minimum-grade prerequisite?
4. Which email address do student accounts use, matching what Microsoft Entra reports?
