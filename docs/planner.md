# The planner

**For:** developers, and advisors or registrar staff who want to know how recommendations are made.

The planner builds each student's recommended plan for a term. It is **deterministic code, not AI**: it decides what is valid, and the AI explainer (M7) only puts its output into words. It is **pure**: no database and no clock. Everything arrives in its input, including today's date and the term, so the same input always gives the same plan, and every decision can be tested and explained.

- Contract (input and output types): [`src/Liwa.Domain/Planning/PlannerContract.cs`](../src/Liwa.Domain/Planning/PlannerContract.cs)
- Implementation: `src/Liwa.Domain/Planning/Planner.cs`

## Input

| Part | Contents |
|---|---|
| `Today`, `Term` | The date and the term being planned (code, season, year) |
| `Student` | Facts already reduced from the transcript: passed courses with their counting grade (after the repeat-grade rule; transfer credit counts as grade `TR`), failed courses, courses in progress, credits earned, GPA, probation, credits the program needs, permissions granted |
| `Catalog` | Every course: code, credits, parsed prerequisite rule, seasons it is normally offered |
| `DegreePlan` | The student's plan for their catalog year: requirement groups (`AllOf`, `ChooseCredits`, `AnyCourse`) and suggested semesters |
| `Sections` | This term's sections: meeting times, delivery mode, seats left |
| `Policy` | The credit policy: limit rules, repeat-grade rule, in-progress switch, graduating exemption |
| `Availability` | When the student is free, as time windows per day. Empty means "show everything" |
| `Preferences` | Optional own credit cap (can only lower the policy maximum) and delivery preference (any / on campus / online) |

## Output

| Part | Contents |
|---|---|
| `Limits`, `MaxCredits` | Which credit rule applied (minimum, maximum, rule name) and the effective maximum after the student's own cap |
| `Plan` | Recommended sections: course, section, credits, priority rank, **reasons** it was chosen, and the **alternative sections that lost, with reasons** (why section A beat section B) |
| `Courses` | Every course still needed, in priority order: status (`Planned`, `Eligible`, `EligibleWithCondition`, `NotEligible`), how many courses it unlocks, its chain length, and reasons |
| `Warnings` | Plan-level notes: below the minimum, no credit rule matched, courses offered this season but with no sections, planned courses that depend on a grade still in progress, graduation not reachable |
| `Projection` | The graduation term **with and without summer terms**, term by term |

Reasons are codes with course and value parameters (`ReasonCode`). The interface turns them into plain sentences for students ("You need to pass ITC304 or ITC301 first", "Take this soon: other courses depend on it", "Waiting for a grade: you can take this if you pass CSC102") and detailed ones for advisors. Ranking details (chain length, seat comparisons) are shown to advisors only.

## Algorithm

1. **What's left.** Each degree-plan course counts toward the first group it fits. `AllOf` courses not passed or in progress are needed. For `ChooseCredits` groups, credits already covered by passed or in-progress courses are subtracted, and the remaining credits are chosen from the group's other courses by priority. `AnyCourse` (free-elective) credits count in the projection as flexible credits. The recommendation doesn't pick free electives, because they aren't in the degree plan (brief §2).
2. **Priority (critical path).** A prerequisite graph is built over the needed courses (prerequisite edges, plus corequisite links that tie courses to the same term). For each course the planner computes:
   - **Chain length:** the longest chain of needed courses that depends on it. Corequisites add no length; each prerequisite step adds one term.
   - **Unlocks:** how many needed courses depend on it, directly or indirectly.

   Priority order: longer chain first, then unlocks more, then required before elective, then a course offered in fewer seasons (fewer chances to take it), then earlier suggested semester, then course code (a deterministic tie-break). Ranks are recorded in the output.
3. **Eligibility this term.** Each needed course is checked:
   - **Offered:** offered this season (offering pattern) and has at least one section. Offered by pattern but with no sections is a **soft warning**.
   - **Prerequisite rule:** AND, OR, minimum grade, corequisite, standing, credits earned, department permission.
     - A prerequisite **in progress** satisfies the rule "with a condition" when `AllowInProgressPrerequisites` is on (the default). The condition becomes a `WaitingForGrade` reason and an `InProgressPrerequisite` warning. When the grade posts and the student failed, M5 re-runs validation and moves an approved plan to *Needs review*.
     - A **failed** prerequisite is reported as such (`PrerequisiteFailed`).
     - For OR, the best branch wins; for AND, every part must hold.
     - Transfer credit (`TR`) meets any minimum grade, as does `P`.
   - **Corequisites** are satisfied by a passed or in-progress course, or by planning both in the same term.
4. **Credit limits.** The first matching rule in the policy gives the minimum and maximum (by season, standing, GPA band and probation; GPA bounds include the lower value, so 3.50 gets the 3.50+ rule). The student's own cap can only lower the maximum. If the student needs fewer credits to graduate than the minimum, the minimum drops to what they need (`GraduatingStudentsExemptFromMinimum`).
5. **Greedy packing with clash checks.** The planner walks the eligible courses in priority order and, for each, looks for a section that:
   - fits the availability windows (sections with no meetings always fit),
   - matches the delivery preference,
   - has seats left,
   - doesn't clash with sections already chosen.

   Among the sections that work, the one with the most seats left wins (then section code). Every losing section is recorded with its reasons: outside availability, clashes with a course, full, delivery not preferred, fewer seats.
   - **Corequisite pairs are placed together or not at all.** Every combination of the two courses' sections is tried. If none fits, **both** are rejected with `CorequisiteDoesNotFit`, naming the partner.
   - A course that would exceed the maximum is skipped (`CreditLimitReached`), and later, smaller courses may still fit.
   - Only one section per course, ever.
   - A `ChooseCredits` group stops receiving courses once its credits are covered (`ElectiveQuotaMet`).

   If the total ends below the minimum, a `BelowMinimumCredits` warning is added; the planner never adds invalid courses to reach it.
6. **Graduation projection.** Starting with the recommended plan for this term (in-progress courses are assumed passed), the planner simulates the following terms using **offering patterns** (not sections, which don't exist yet):
   - Each term takes eligible needed courses in priority order up to that season's credit limit (current GPA and probation assumed).
   - Leftover room is filled with free-elective credits until they are used up.
   - Department permission is assumed obtainable in the future.

   The term sequence follows the calendar (Spring → Summer → Fall). It is run twice: **with** summer terms, and **without** regular summer classes. "Without summer" still uses a summer term for courses that are offered *only* in summer (such as internships); otherwise a degree that requires one could never be finished. A course offered only in Fall therefore pushes graduation a year later when its Fall is missed. The projection gives up after 24 terms (`GraduationNotReachable`).

## Rejected alternatives (considered and not used)

- **Optimal search (integer programming) over all section combinations.** It might squeeze in one more course, but its choices are much harder to explain to a student or advisor, and it is slower. Greedy packing over a transparent priority order gives the same result in nearly every real case, and every decision has a reason.
- **An LLM choosing courses.** It isn't deterministic, can't be verified, and can't be trusted with eligibility. The brief forbids it.
- **Treating corequisites as ordinary prerequisites.** That would schedule a lab a term after its lecture. Corequisites are kept as same-term links.

## Required tests (brief §8)

All are in `tests/Liwa.UnitTests/Planning/PlannerTests.cs`:

- [x] A Fall-only course correctly adds a year when missed.
- [x] The probation credit cap is applied.
- [x] A student exactly at the GPA 3.5 boundary gets the 21-credit cap.
- [x] A corequisite pair where only one section fits availability is rejected as a pair, with a clear issue.
- [x] A graduating senior with 6 credits left is **not** forced to the 12-credit minimum.

The tests also cover: clash avoidance, availability windows, one section per course, in-progress conditions, failed and minimum-grade prerequisites, OR rules, standing, credits and permission, the soft warning for offered-but-no-sections, elective quotas, delivery preference, projection with and without summer, and determinism (same input, same output).
