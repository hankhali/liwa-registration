# The AI explainer

The explainer turns the planner's results into a few plain sentences, and answers questions about the plan. It
**never decides anything**: the deterministic planner decides, the advisor approves. If no AI provider is configured,
the panel says "Not available" and nothing else in the app changes.

Code: `src/Liwa.Application/Explainer/` (contract and grounding rules) and `src/Liwa.Infrastructure/Explainer/`
(service and providers). Tests: `tests/Liwa.UnitTests/Explainer/ExplainerGuardTests.cs`,
`tests/Liwa.IntegrationTests/ExplainerTests.cs`.

## Where it appears

| Who | Where | What |
|---|---|---|
| Student | Planner page, "Ask about your plan" | **Explain my plan** (4–6 sentences), **Why?** next to every not-eligible course, and a question box limited to the plan |
| Advisor | Review page | The same, run on the submitted plan, plus **Draft a note to the student**, which goes into the advisor's note box to edit. It is never sent automatically |

The answer follows the UI language (English or Arabic).

## Grounding is enforced in code, not in the prompt

1. **The payload** (`ExplanationPayload`) is built only from the planner's output (`PlannerResult`, `Planner.Check`,
   `Planner.ProjectPlan`) and the catalog entries (code, title, credits) of the courses in it: the plan, the
   not-eligible courses with their reason codes, the warnings, credit limits and the graduation projection.
   It contains no name, university ID, email or advisor name.
2. **The payload is refused** at the service boundary (`ExplainerGuard.PayloadIsSafe`) if it contains one of the
   student's or advisor's names (or a distinctive part of one), an email address or an ID. A refusal is logged and
   the fallback is shown. This should never happen; it is a safety net.
3. **Questions are cleaned** (`StripQuestion`): names, IDs and emails become "the student" before anything else
   happens. Questions are limited to 500 characters.
4. **Replies are checked** (`CheckReply`) and **discarded** if they:
   - name a course code that isn't in the payload;
   - contradict the planner: say a not-eligible course can be taken, or give a graduation term the projection
     doesn't have;
   - are in the wrong language (Arabic expected but mostly Latin text, or Arabic text in an English answer).
   A discarded reply is replaced by the fixed fallback, **"I can't explain that part — ask your advisor."**
   («لا أستطيع شرح هذا الجزء — اسأل مرشدك.»), and every discard is logged with its reason.

## What-if questions re-run the planner

"What if I drop STA201?", "What if I skip summer?", "What if I add MGT230?" and "Why can't I take ITC301?" are
recognised by simple rules in English and Arabic (`ParseIntent`). Anything else is classified by **one** model call
into a fixed set (drop / add / summer / why / explain / none), and the code checks the answer
(`ParseClassification`: a known intent, and a course code that exists).

| Intent | What the code does |
|---|---|
| drop X | Removes X's section from the plan and runs `Planner.ProjectPlan` and `Planner.Check` again |
| add X | Adds the free section of X with the most seats that doesn't clash, then re-runs both |
| summer | Compares the projection with and without summer (summer is never assumed) |
| why X | Puts X and its reason codes in the payload as the "focus" |
| none | Declined politely: "I can only answer questions about your plan…" (no provider call) |

The model explains **only the difference**, and the planner's own figures (graduation before and after, the semester
difference) are shown under the answer, so the student never has to trust the wording.

## Reliability, cost and limits

- Provider, model and base URL are configurable (Settings → AI assistant): **Anthropic** (default model
  `claude-sonnet-5-5`), any **OpenAI-compatible** endpoint, or the **mock** provider for demos. The mock is only
  offered when the API runs in Development; any other environment refuses to start with it.
- **Timeout** 20 seconds (configurable), **one retry**, then the fallback.
- **Cache:** identical plan + question pairs are answered from memory for 30 minutes.
- **Hourly limit** per person (default 20 questions), then a friendly "try again later".
- The API key is encrypted at rest and only ever shown as its last four characters.
- **AI usage** (Admin → AI usage): requests per day, tokens in and out, estimated cost
  (`Ai:PricePerMillionInputUsd`, `Ai:PricePerMillionOutputUsd`, default 3 and 15), cache hits, recent fallbacks and
  discards. The log (`ai_requests`) keeps counts and reasons only — never the question, the payload or the reply.

## What goes to the model, and what never does

| Sent | Never sent |
|---|---|
| Course codes, titles and credits of the courses in the plan or blocked | Names (student, advisor) |
| The planner's reason and warning codes with their parameters | University ID, email, username |
| Credit limits and totals | GPA, grades, transcript |
| Graduation terms (with/without summer) and what-if figures | Probation or hold status in words |
| The question, after names, IDs and emails are removed | Anything from other students |
