# Security and privacy, in plain language

**For:** anyone reviewing Smart Registration for the university (data protection, IT security, the Registrar), no
technical background needed.

## Who can see and do what

| Role | Can | Can't |
|---|---|---|
| **Student** | See their own studies, plan, messages; send their plan to their advisor; ask the AI assistant about their own plan | See any other student; see staff screens |
| **Advisor** | See and review the plans and records of **their own** students; approve, send back or suggest changes; see the list of data changes | See students assigned to other advisors; change catalog data, users or settings |
| **Administrator** | Load and edit catalog and student data, manage staff accounts, change settings and credit rules | — (every change is recorded) |

Every request is checked **on the server**, not just hidden on the screen: asking for another advisor's student gives
"not allowed", and students get "not allowed" on every staff address. Every page requires sign-in except the sign-in
page itself.

## Signing in

- **Today:** a username and password. Passwords are never stored, only a one-way scrambled form (PBKDF2 with 600,000
  rounds). After 10 sign-in attempts in a minute from the same place, further attempts are refused until the minute is up.
- **When the university switches it on:** "Sign in with your Liwa account" through Microsoft Entra ID, the same account
  as Blackboard. The app never sees the password and keeps none of Microsoft's tokens ([sso.md](sso.md)).
- A sign-in lasts 8 hours. Switching university sign-in on can't lock anyone out: username/password sign-in can only be
  switched off deliberately, after university sign-in has been tested.
- Practice (demo) accounts with a shared password exist only on development machines and are shown on the sign-in page
  only there. On a real server they are off.

## Secrets

The settings that unlock other systems — the university sign-in secret and the AI provider's key — are **encrypted**
before they are stored, and the app only ever shows their **last four characters**. The keys that protect sessions and
these secrets are set by IT on the server and are never in the source code.

## What the AI assistant sees — and never sees

The AI assistant only **explains** what the planner already decided; it can't change anything. What is sent to the AI
provider is built in code from the plan itself:

| Sent | Never sent |
|---|---|
| Course codes, course titles, credits | Any name (student or advisor) |
| The planner's reasons ("needs CSC102 first", "only offered in Fall") | University ID, email address, username |
| Credit limits and the graduation estimate | GPA, grades, transcript |
| The student's question, after names, IDs and emails are removed | Probation or hold details, anything about other students |

Before anything is sent, the app checks it once more and **refuses** if it finds a name, ID or email. Every answer is
checked when it comes back: an answer that mentions a course that isn't in the plan, says something the planner didn't
say, or is in the wrong language is **thrown away** and replaced by "I can't explain that part — ask your advisor".
The assistant is off until an administrator switches it on, and each person can ask a limited number of questions an
hour. The usage log keeps counts and reasons only — never questions or answers. Details: [explainer.md](explainer.md).

## Records of who did what (audit log)

Every change of a plan's status is recorded with the time, the person, and the plan's version **before and after**
(which classes were added or removed). Every data upload, edit and removal gets a numbered entry in the data change
history (who, when, how many rows), visible to administrators and advisors. Staff account changes, password resets,
advisor reassignments, settings changes and grade postings are recorded too. The records are only ever added to,
never edited.

## Messages and email

Students and advisors get an in-app message whenever a plan changes. Until a mail server is connected, the emails that
would be sent are written to the server log in the person's language, identified by an internal number, never by the
email address.

## Keeping data

- The data lives in one PostgreSQL database on the university's own server; nothing is stored with third parties.
- Students missing from an upload are **never deleted** automatically. Degree plans can only be removed when no student
  follows them.
- **Retention (to agree with the university):** plans and their history are kept for the student's time at the
  university; the AI usage counts for 30 days on screen (the rows can be cleared after that). These are proposals in
  [assumptions.md](assumptions.md).
- Backups follow the university's usual database backup schedule ([INSTALL.md](INSTALL.md)).

## Web security basics

The site runs over HTTPS behind the university's web server, with protections against being embedded in other sites,
content-type tricks and loading scripts from elsewhere. Errors shown to users never include technical details; IT can
find them in the log by the reference number shown.
