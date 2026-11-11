# UX findings: the student screens through a first-year's eyes

**Method.** On 2026-11-10 I captured every student screen with the installed Chrome (Playwright, `channel: 'chrome'`)
for `student.new` (plan being reviewed), `student.senior` (approved), `student.transfer` (advisor suggested changes),
`student.mid1` (sent back), and `student.evening` (no plan yet). Each was captured in English and Arabic, at
desktop 1440 px and phone 390 px wide: 120 captures in `docs/screenshots/ux-pass/`, with a contact sheet at
`docs/screenshots/ux-pass/index.html`. I looked at each one asking "would a 17-year-old on their first day know what to do here,
and would they hesitate?" The captures after the changes are in `docs/screenshots/ux-pass-after/`, side by side with
these on the same contact sheet.

Each finding has an ID. The **What changed** column records the fix. "Not fixed" items are listed again at the end.

## Everywhere

| ID | What a student would not understand or would hesitate on | What changed |
|---|---|---|
| X1 | **The week grid is wrong.** Only 75-minute classes sit at their time. Every 50-minute class (ENG102 10:00–10:50, GEN110 8:00–8:50), the 12:30 seminar and both 2:00–4:50 labs are drawn *below 8 pm*, in a pile at the bottom of the grid. A student would think they have classes at night, or that the app is broken. The same bug affects the advisor's review page. | The grid now uses 5-minute rows, so every class lands at its real time. A unit test covers 10:00–10:50 and 12:30–1:20. |
| X2 | The grid and the phone day list show bare codes ("MGT220"). A first-year doesn't know codes. | Each block shows the code **and** the title. The phone list shows "MGT220 Human Resource Management". |
| X3 | Reason sentences show bare codes: "You need to pass ITC201 first", "Taken together with CSC101L". | Every course in a reason is "code + title": "You need to pass ITC201 Database Systems first". |
| X4 | Phone bottom bar: Home / Availability / Planner / "Graduation Path" (wraps onto two lines) / More. Messages, which is where advisor replies arrive, is hidden behind More. | Bottom bar is **Home / Plan / Graduation / Messages**. Free time and My Plan are reached from those pages and the drawer. |
| X5 | The primary (gold) button is never top right. On the Planner it sits about 2,300 px down, after eight course cards and the week grid. | Every student screen has one gold primary action in the page header, top right (top of the page on phones). Everything else is an outlined or text button. |
| X6 | The top-bar title on Home reads "My plan", which is also the name of a different page ("My Plan"). | The Home title is "Home". |
| X7 | Arabic: the term name comes from the server in English ("Spring 2027") in the top bar, on the plan heading and inside messages ("أرسلت خطتك لفصل Spring 2027"). | The app builds the semester name from the term code in the reader's language (الربيع 2027) in the top bar and on student pages. Messages with a `term` parameter are translated the same way. |
| X8 | Status names ("Waiting for review", "Being reviewed", "Sent back", "Changes suggested") don't say what the student should do. | Students see the names Draft, Sent to advisor, Advisor is reviewing, Approved, Sent back with comments, Advisor suggested changes, and Needs another look, each with a one-line "what this means for you". Advisors keep their own wording. |
| X9 | Jargon in student text: "Not eligible this semester", "section 02" and "Priority #3: … a chain of 4 terms" under Details, "version 1", "Plan history" entries with section numbers ("BUS220 (E1)"). | Reworded: "Classes you can't take yet", "another class time", "Take this soon: 4 other classes depend on it", and no version numbers or section codes in student text. A test fails if a banned word appears in any student-facing English string. |
| X10 | "Ask about your plan — Not available": an empty box on every planner when the AI assistant is switched off. | The box is hidden when the assistant is off. |

## Home

| ID | Finding | What changed |
|---|---|---|
| H1 | No single answer. Three cards compete: a three-step walkthrough, "Your studies", and "Your plan". The plan status, the only thing that changes, is in the bottom-right card. | Home is one answer: **"Spring 2027: your plan is Sent back with comments."** plus the one-line meaning and the one primary button for the next step. Under it are three big numbers: credits done, GPA, graduation date. Nothing else is above the fold. |
| H2 | The walkthrough still shows for a senior whose plan is already approved. | The walkthrough only shows while the student has no plan, below the answer. |
| H3 | The primary button reads "My Plan", a page name rather than an action. | The button names the next step for each status: "See my plan" (no plan), "Send my plan" (draft), "See what my advisor said" (sent back), "Review the changes" (changes suggested), "Check my plan again" (needs another look), "See my plan" (with the advisor / approved). |
| H4 | The graduation date isn't on Home, and credits and GPA are small text. | Credits done, GPA and graduation date are the three big numbers. |

## When are you free (Availability)

| ID | Finding | What changed |
|---|---|---|
| A1 | Picking "Evenings only" just reveals an hour grid and a second button, "Save and see my plan", at the bottom. The plan doesn't change until the student finds that button: four clicks and a scroll for "show me only classes I can attend". | Picking Full-time daytime, Evenings only, or Weekends + evenings saves at once and opens the plan, rebuilt for those times. Custom opens the grid as before. The Planner has the same choices (plus "Any time" and "Choose my own hours") as one-tap buttons at the top, so the plan updates in place. |
| A2 | "Go to my plan" link under the cards duplicates the cards' purpose. | Removed. "Skip this and show me every class" stays. |

## Planner

| ID | Finding | What changed |
|---|---|---|
| P1 | When the plan is with the advisor there is no first sentence telling the student what to do. The card title is "Your plan for Spring 2027" and there is no page heading. | Page header with a first sentence for every state ("This is your plan. Happy with it? Send it to your advisor." / "Your advisor has your plan. You don't need to do anything now."). |
| P2 | "Send to my advisor" sits at the bottom. When a plan comes back, the advisor's note is at the top but the button is two screens away. | "Send to my advisor" is the gold button in the header, top right. |
| P3 | An approved plan says "You'll get a notification when they reply". | Approved says "You're ready to register for these classes." |
| P4 | Every course card repeats "Needed for your degree" plus up to three bullets and a "Details" button: a lot to read for 5–8 classes. | One plain line per class (the most useful reason). "Needed for your degree" only shows when there is no better reason. The critical-path badge reads "Take this soon". |
| P5 | "Why can't I take ITC301?" needs two clicks into a collapsed panel, then a collapsed group, and the answer uses a bare code. "Not eligible" is jargon. | A visible section, **"Classes you can't take yet"**, lists every such class with code, title and a one-sentence reason ("You need to pass ITC201 Database Systems first"). Nothing to expand. |
| P6 | Once a plan is sent, the "can't take yet" list disappears, so the question can't be answered. | The list stays visible in every state. |
| P7 | Free time can only be changed on another page. | The three free-time choices are buttons at the top of the planner (when the plan can still be changed). |
| P8 | The credit total is small text in the middle of the page. | Big number in the header area: "15 credits this semester (you can take 12–18)". |
| P9 | Phone: the day list repeats the day on every line ("Mon · MGT220 …", "Mon · BUS205 …"). | Per-day headings ("Monday") with that day's classes under each, with code and title. No hour grid on phones (the "Show week grid" toggle is gone). |

## Graduation

| ID | Finding | What changed |
|---|---|---|
| G1 | The graduation date is an ordinary sentence. There is no "semesters left". | A headline: **Spring 2030**, with "6 semesters left" as a second big number. |
| G2 | Two toggles (Summer, and My selection / Recommended). | One toggle: "Include summer classes". When the student's own plan differs from ours, one line says so ("Our recommended plan: Fall 2029"). |
| G3 | "1 credits of free electives". | "1 credit of free electives". |
| G4 | "Why some courses wait" is a wall of 30–40 bullets. | Kept (it answers "why so long?"), but it starts collapsed under a one-line summary, and every line has code + title. |
| G5 | Phone: semester columns are cut off at the right edge and there is no hint to scroll. | On phones the semesters stack vertically. |

## My Plan

| ID | Finding | What changed |
|---|---|---|
| M1 | Sent back: the advisor's note is the third thing on the page, and nothing on the page does the next step. | The advisor's note is the first thing on the page, in a coloured box, with one gold button for the next step: "Fix my plan" (sent back), "Accept the changes" (changes suggested), "Check my plan again" (needs another look). |
| M2 | My Plan doesn't list the classes in the plan. | "Your classes": code, title, days and times. |
| M3 | Suggested changes show "— BUS220 (E1)" with no title. History shows section codes and "version 1". | Changes and history show code + title with no section codes. Versions are only shown to advisors. |
| M4 | The status tracker's labels ("Advisor reviewing", "Advisor's decision") are fine, but the chip has no explanation. | The student status name with its one-line meaning sits above the tracker. |

## Messages

| ID | Finding | What changed |
|---|---|---|
| S1 | Works. Each message links to the right page. In Arabic the semester name is English (X7). | Fixed by X7. Empty state already says what will appear; it now has a "See my plan" button. |

## The five student tasks, after the changes

Clicks counted from Home. Checked on the "after" captures and by hand on the running app.

| Task | Path | Clicks |
|---|---|---|
| a. "I'm new. What do I take this semester?" (`student.evening`) | Home: "Spring 2027: your plan is ready to look at" → gold **See my plan** → the recommended plan is already built, with the week → gold **Send to my advisor** (top right) → **Send now** | 3 |
| b. "I work evenings. Only classes I can attend." | Home → **Plan** (rail / bottom bar) → **Evenings only** at the top of the plan → the plan is rebuilt for Mon–Thu 5–8 pm. (Or Free time → the **Evenings only** card, which saves and opens the rebuilt plan.) | 2 |
| c. "Why can't I take ITC301?" | Home → **Plan** → "Classes you can't take yet": **ITC301 Systems Analysis and Design**: "You need to pass ITC201 Database Systems first". Visible without expanding anything | 1 |
| d. "When will I graduate? What if I skip summer?" | Home already shows the graduation date as a big number. → **Graduation**: "You could graduate in **Spring 2030**", "Semesters left: **7**", one toggle "Include summer classes" with the other answer under it | 1–2 |
| e. "My advisor sent it back. What now?" (`student.mid1`) | Home: "your plan is **Sent back with comments**" → gold **See what my advisor said** → My Plan opens with the advisor's note first → gold **Fix my plan** → the planner, with the note at the top and **Send to my advisor** | 2 |

## Advisor review page (brief §10)

| ID | Finding | What changed |
|---|---|---|
| R1 | Two columns (plan + a side panel with decision, checks, student and graduation stacked). The brief asks for left = student, centre = plan, right = graduation, with the decision at the bottom. | Three columns: **left** = student (credits, GPA, year, credit limit, probation, hold, free-time mini-grid, link to the full record); **centre** = the week, every class with the planner's reasons (open), today's checks and the student's acknowledged warnings, history; **right** = graduation for this plan in big type, delay vs the recommendation, and the with/without-summer table. **Bottom**: a bar with the note, Approve, Suggest changes and Send back, which stays at the bottom of the window. Two columns below 1280 px, one below 900 px. |
| R2 | "Suggest changes" opened a checkbox list with section codes and no timetable. | It opens **the same two-panel planner the student uses** (`shared/plan-editor.component.ts`, also used by the student's "Change courses"), run on the student's record: classes grouped by requirement with title, "Take this soon", one reason and a class-time picker, and the week with clashes and a credit counter. Checked live on the server, and plans with errors can't be sent. |

Captures: `docs/screenshots/ux-pass-after/advisor.it__{en,ar}__desktop__{review,suggest-changes}.png`.

## Not fixed (and why)

- **Instructor names and advisor notes stay in English on Arabic screens.** Names are proper nouns from the
  registrar's data, and an advisor's note is what the advisor typed. Translating either would put words in someone's
  mouth.
- **Course titles without an Arabic title** stay in English, as the rules allow. The demo catalog has Arabic titles for
  all 3 programs, but real data may not.
- The term selector in the top bar remains (with one open semester it is a label in practice). It is needed once two
  semesters overlap.
- **The rail still says "Availability" and "Graduation Path"**, as the brief (§9) names them.
  The page headings use the plainer "When are you free?" and "Graduation", and the phone bottom bar says
  "Graduation".
- When an advisor has suggested changes, **"Your classes" on My Plan lists the suggested version** (that is what is
  saved as the current plan). The diff above it shows what changed. A side-by-side "yours / suggested" view would
  be clearer, but needs the previous version's classes from the API.
- **Graduation for students in a summer-only course** (e.g. `student.transfer`'s BUS410 internship): the "without
  summer" path still shows that one summer, because the course is only offered then. The planner is right, but the
  toggle can look as if it does nothing for these students. A one-line note there would help.
- **Advisor-side jargon is unchanged** on purpose: advisors keep "section 02", ranking details and version numbers.
- Full-page screenshots show the advisor's sticky decision bar and the phone bottom bar part-way down the image. In
  a real window they sit at the bottom of the screen.
