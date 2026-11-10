# Connecting Smart Registration to PeopleSoft Campus Solutions

**For:** Liwa University IT and the PeopleSoft team (`https://sis.lu.ac.ae/psp/sisprd/`).
**Today:** no API access. Data arrives as **CSV files** (Admin → Imports & exports) in the format in
[import-contract.md](import-contract.md). That path is complete and tested, and is the recommended first step.

All student, catalog and timetable data is read through three interfaces (`src/Liwa.Application/Sis/SisProviders.cs`):

| Interface | Methods | Used for |
|---|---|---|
| `IStudentRecordProvider` | `GetByUserAsync(user)` | program, catalog year, transcript (completed / in progress / withdrawn), transfer credit, academic status (probation), advisor, hold |
| `ICourseCatalogProvider` | `CoursesAsync()`, `ProgramAsync(id)`, `DegreePlanAsync(program, catalogYear)` | courses with credits, prerequisite rules and usual seasons; programs; degree plans with requirement groups |
| `ITermScheduleProvider` | `CurrentTermAsync()`, `SectionsAsync(term)` | the term (teaching days, Friday rule) and its classes with meeting times and seats |

The setting `Sis:Provider` chooses the backend: `Database` (the import-fed database, in use today) or `PeopleSoft`
(`PeopleSoftSisProvider`, a documented stub that throws "not implemented"). **Nothing outside these providers knows
which backend is in use** — the planner, the screens and the explainer only see the interfaces.

## Three ways to connect

| Path | How | Effort | Recommended for |
|---|---|---|---|
| **A. PS Query → CSV export** (scheduled) | Public PS Queries run nightly (Process Scheduler), output CSV in the import format, dropped to a folder and loaded on the Imports page (or by a scheduled call to `POST /api/admin/imports/{kind}`) | Low: queries only | **Start here.** Works with today's database provider, no code change |
| **B. Integration Broker REST** | PeopleSoft exposes REST services (Integration Broker, service operations returning JSON) that `PeopleSoftSisProvider` calls live | Medium: build service operations + implement the provider | Live data, no nightly lag |
| **C. Component Interface via middleware** | A middleware (e.g. a small Java/.NET service with the PeopleSoft CI SDK) reads Component Interfaces and exposes REST to the provider | High | Only if Integration Broker isn't available |

Whichever path: the student's **university ID = PeopleSoft `EMPLID`**, the **term code** maps from `STRM`
(e.g. `2272` → `2027SP`; the mapping table is the Registrar's), and **grades** use PeopleSoft's official grade
(`CRSE_GRADE_OFF`).

## Fields needed, by record, mapped to our CSV columns

### Terms (`terms.csv`)

| Our column | PeopleSoft | Notes |
|---|---|---|
| `term_code` | `TERM_TBL.STRM` → mapped to `YYYYSP/SU/FA` | Registrar provides the mapping |
| `name` | `TERM_TBL.DESCR` | "Spring 2027" |
| `season`, `year` | derived from `STRM` / `TERM_BEGIN_DT` | |
| `start_date`, `end_date` | `TERM_TBL.TERM_BEGIN_DT`, `TERM_END_DT` | |
| `teaching_days`, `friday_afternoon` | not in PeopleSoft | set by the Registrar (default Mon–Fri, no Friday afternoon) |
| `is_current` | the term open for planning | one row = yes |
| `in_progress_prerequisites` | not in PeopleSoft | optional override of the policy |

### Courses (`courses.csv`)

| Our column | PeopleSoft |
|---|---|
| `course_code` | `CRSE_OFFER.SUBJECT` + `CATALOG_NBR` (e.g. `ITC` + `201`) |
| `title` | `CRSE_CATALOG.COURSE_TITLE_LONG` |
| `title_ar` | translated title (custom field or Registrar's list) |
| `credits` | `CRSE_CATALOG.UNITS_MAXIMUM` |
| `department` | `CRSE_OFFER.ACAD_ORG` |
| `prerequisites` | `RQ_GRP_TBL` / `RQ_LINE_TBL` requisite groups → our text rule (`ITC201 AND (CSC102 OR MTH110:C)`, `COREQ(...)`, `STANDING(Junior)`, `CREDITS(90)`, `PERMISSION`) |
| `offered` | `CRSE_TYPOFF_CD` (typically offered) → `Fall;Spring` / `Summer` |
| `active` | `CRSE_CATALOG.EFF_STATUS` |

### Programs and degree plans (`programs.csv`, `degree-plans.csv`)

| Our column | PeopleSoft |
|---|---|
| `program_code`, `name`, `total_credits` | `ACAD_PROG_TBL.ACAD_PROG`, `DESCR`; `ACAD_PLAN_TBL` units required |
| `catalog_year` | requirement term (`REQ_TERM`) of the plan |
| `group_key`, `group_name`, `group_type`, `group_credits` | Academic Advisement requirements (`RQ_GRP_TBL`, `RQ_TBL`): AllOf = all courses; ChooseCredits = "choose N units"; AnyCourse = free electives |
| `course_code`, `suggested_semester` | requirement lines (`RQ_LINE_TBL`) and the sample plan |

### Class schedule (`sections.csv`)

| Our column | PeopleSoft |
|---|---|
| `term_code`, `course_code`, `section` | `CLASS_TBL.STRM`, `SUBJECT`+`CATALOG_NBR`, `CLASS_SECTION` |
| `delivery` | `CLASS_TBL.INSTRUCTION_MODE` (P = on campus, OL = online, HY = hybrid) |
| `instructor` | `CLASS_INSTR` → name |
| `room` | `CLASS_MTG_PAT.FACILITY_ID` |
| `capacity`, `enrolled` | `CLASS_TBL.ENRL_CAP`, `ENRL_TOT` |
| `days`, `start_time`, `end_time` | `CLASS_MTG_PAT.MON…SUN`, `MEETING_TIME_START`, `MEETING_TIME_END` (one row per meeting pattern) |

### Students (`students.csv`)

| Our column | PeopleSoft |
|---|---|
| `student_id` | `PERSONAL_DATA.EMPLID` |
| `display_name` | `NAMES.NAME_DISPLAY` |
| `email` | `EMAIL_ADDRESSES` (university type) — must match what Entra ID reports ([sso.md](sso.md)) |
| `program_code`, `catalog_year`, `admit_term` | `ACAD_PROG` (current row), `ACAD_PLAN.REQ_TERM`, `ADMIT_TERM` |
| `status` | `ACAD_STDNG_ACTN` (probation → `Probation`) |
| `advisor_username` | `ADVISOR.ADVISOR_ID` → the advisor's Smart Registration username |
| `hold` | `SRVC_IND_DATA.SRVC_IND_CD` (active negative service indicators, e.g. `FINANCE`) |

### Transcripts (`transcripts.csv`)

| Our column | PeopleSoft |
|---|---|
| `student_id`, `term_code`, `course_code` | `STDNT_ENRL.EMPLID`, `STRM`, class → course |
| `credits` | `STDNT_ENRL.UNT_TAKEN` |
| `grade` | `STDNT_ENRL.CRSE_GRADE_OFF` (empty while in progress) |
| `status` | `Completed` / `InProgress` / `Withdrawn` (`STDNT_ENRL_STATUS`, `ENRL_STATUS_REASON`) / `Transfer` (`TRNS_CRSE_DTL`) |
| `source` | transfer institution (`EXT_ORG_ID` → name) |

## What `PeopleSoftSisProvider` must implement (path B or C)

1. `GetByUserAsync`: map the signed-in account to `EMPLID` (the account's university ID), then return the student
   record with the **full** transcript and transfer credit, academic status, advisor and hold — the same shape the
   import builds.
2. `CoursesAsync`: every active course with credits, the prerequisite rule as text (parsed by
   `PrerequisiteParser`, so unknown courses and cycles are caught) and the seasons it is usually offered.
3. `ProgramAsync` / `DegreePlanAsync`: the program and its requirement groups for the student's catalog year.
4. `CurrentTermAsync` / `SectionsAsync`: the term open for planning and its classes with meeting times and seats.

Cache catalog and schedule data (they change rarely); read the student record live. Keep the CSV import working as the
fallback when PeopleSoft is unavailable.

## Sample PS Query export spec (for the PeopleSoft team)

| Query name | Output file | Prompts | Rows |
|---|---|---|---|
| `LU_SR_TERMS` | `terms.csv` | none | terms from the previous year to two years ahead |
| `LU_SR_COURSES` | `courses.csv` | none | active catalog courses with requisite text |
| `LU_SR_PROGRAMS` | `programs.csv` | none | the programs in scope |
| `LU_SR_DEGREE_PLANS` | `degree-plans.csv` | catalog year | requirement groups and lines per program |
| `LU_SR_SECTIONS` | `sections.csv` | `STRM` | classes and meeting patterns for the term open for planning |
| `LU_SR_STUDENTS` | `students.csv` | `STRM` | active students in the programs in scope, with advisor and holds |
| `LU_SR_TRANSCRIPTS` | `transcripts.csv` | none | all enrollments and transfer credit of those students |

Rules for every file: UTF-8, comma-separated, header row with **exactly** our column names, one row per item, empty
cell for "no value". Run them in the order above. A file with any error is rejected as a whole, with line and column
numbers, so a bad export never half-loads. Re-running a file that hasn't changed changes nothing. Students missing from
a file are never deleted.
