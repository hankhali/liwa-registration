# Portal patterns: what we take from PeopleSoft Fluid and Blackboard Ultra, and what we don't

Students and staff already use **Oracle PeopleSoft Campus Solutions (Fluid UI)** for registration and **Anthology
Blackboard Learn Ultra** for courses. Smart Registration should feel familiar next to them without cloning either.
Notes from the vendors' public documentation and the live Liwa sign-in pages.

## Taken

| Pattern | From | In Smart Registration |
|---|---|---|
| Left navigation rail with icons and labels, collapsible | Blackboard Ultra base navigation | The role rail (`core/layout/nav.ts`), collapsible to icons; phone bottom bar + drawer |
| Header / account bar: logo, page title, notifications, user menu | PeopleSoft Fluid banner | The indigo top bar with the gold rule, white wordmark, page title, term selector, bell, EN/AR, user menu |
| Tiles with a count that lead to a list | Fluid homepage tiles | Advisor home stat cards ("Plans waiting for review") |
| Activity stream / notifications with unread count | Ultra activity stream | The bell and the Messages page |
| One task per page, primary action bottom-right/end | Fluid activity guides | Planner: one primary "Send to my advisor"; review: decision panel |
| Open Sans | Ultra's UI font | Open Sans + Noto Sans Arabic, self-hosted |

## Deliberately not copied

- **PeopleSoft's registration vocabulary and shopping-cart flow** ("enrollment cart", "class search", "validate"):
  first-year students don't know these words. We show a ready plan and plain sentences instead.
- **Dense grids of class search results:** the planner offers only classes the student can actually take.
- **Vendor branding, icons and colours** (Oracle Redwood, Anthology purple): the app uses Liwa's colours and Material
  icons, so it reads as a Liwa service, not a vendor screen.
- **Modal-heavy flows:** checks appear inline as sentences; the only confirmations are "I understand" per warning and
  destructive admin actions.
- **Hidden server-side rules:** every decision carries a reason code shown in plain words, with details for advisors.
