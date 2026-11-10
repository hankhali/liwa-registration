# 10-minute faculty demo

**Before you start (2 minutes, off-screen):** on the demo machine run `.\scripts\reset-db.ps1 -Force`, start
`.\scripts\run-dev.ps1`, wait until http://localhost:4200 shows the sign-in page, then run
`python scripts/demo_plan_states.py` so there are plans in every state. Every password is **`Liwa#2027`** (the sign-in
page lists the practice accounts in Development: click one to fill the form). Have two browser windows ready (one normal,
one private) so a student and an advisor can be signed in at the same time.

## 1. The problem (30 seconds)

"Every semester students pick sections by guesswork, and advisors spend the first weeks fixing them: missing
prerequisites, clashes, courses not in the degree plan, an order that delays graduation. Smart Registration builds a
valid plan first and sends it to the advisor."

## 2. A first-year student gets a plan in one click (3 minutes) — wow #1

1. Sign in as **`student.new`** (Mariam, IT, first semester). Point at the three-step walkthrough on Home.
2. Open **Availability**: four big cards. Choose **Full-time daytime** → the week grid appears filled in. "Tap any hour
   to change it — and see, Friday afternoon is never offered." Save.
3. **Planner**: "Here is the plan we recommend for Spring 2027." Point at the timetable (no clashes), the gold
   **Take on time** badges ("other courses depend on these"), and the plain reasons under each course.
4. Open **Not eligible this semester** (grouped by reason) and click **Why?** next to a course: the assistant explains in
   one or two sentences (if the AI assistant is switched on; otherwise it says "Not available").
5. Switch to Arabic (top bar): the whole page flips to right-to-left, course codes stay readable.
6. Press **Send to my advisor**: each note needs "I understand" — that is all the student has to do.

## 3. The graduation path and the delay banner (2 minutes) — wow #2

1. In **Planner → Change courses**, remove one gold course (the credit counter and the timetable update live).
   Changes are saved automatically as a draft.
2. Open **Graduation Path**: semesters as columns, gold courses on the critical path, and the banner
   **"Your selection delays graduation by one semester"** with both dates side by side. Toggle **Include summer**.
3. Ask the assistant "What if I drop …?": the planner re-runs and its own figures appear under the answer.
4. Back in the planner press **Go back to the recommended plan**.

## 4. The advisor reviews and proposes a change (3 minutes) — wow #3

1. In the other window sign in as **`advisor.business`** (Dr. Fatima). Home shows the counts and **Needs attention**.
2. Open **Plans to review** → filter **Waiting for you** → open a plan. One page: the student summary and free time on
   the left, the timetable with every class's reasons and why other times lost, today's checks, and graduation for this
   plan against the recommendation ("delays graduation by …" or "keeps the same date").
3. Click **Suggest changes**, drop one class, write a short note (or click **Draft a note to the student** and edit it),
   send.
4. Sign in as **`student.transfer`** (Maryam): **My Plan** shows "Dr. Fatima suggested these changes" with the
   difference (+ added, − removed) and **Accept**. Accept → Approved, and the advisor gets a message.

## 5. When grades change, and the registrar's side (1 minute)

1. As **`advisor.it`**, open the plan marked **Needs another look** (`student.honors`): a failing grade was posted after
   approval, so the plan came back automatically and both sides were told.
2. Sign in as **`admin`**: **Catalog** (every edit goes through the same checks as an uploaded file), **Imports &
   exports** (check a file, see errors by line, nothing half-loads; download everything back), **Settings → Credit rules**
   ("Test the rules" lists every student whose limits would change before saving).

## Closing line (30 seconds)

"The planner is deterministic and tested on the real degree plans; the AI only explains it and can't invent courses;
everything is in English and Arabic; and the same data can come from PeopleSoft exports today and a live connection
later."
