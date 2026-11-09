# Assumptions register

Each item is something we assumed or decided without confirmation. The **Confirm with** column says who should check it. Update an item's status when it is confirmed or changed.

Status key: **Open** = needs confirmation · **Decided** = project decision, change on request · **Confirmed** = agreed with the university.

## Platform and tooling

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| A1 | Backend targets **.NET 10** (latest LTS installed: SDK 10.0.302). | The brief asks for the latest LTS. | University IT (hosting) | Decided |
| A2 | Frontend is **Angular 22** with Angular Material 22, on Node 26 LTS. | The brief asks for the latest Angular. | — | Decided |
| A3 | On the development machine, **Docker Engine runs inside WSL Ubuntu** instead of Docker Desktop. The code and the dev servers still run natively on Windows. | Docker Desktop needs admin rights, which this machine doesn't have. Nothing in the project depends on this, so it works unchanged with Docker Desktop. | — | Decided |
| A4 | UI language switches **in the running app** (Transloco) rather than through Angular's compile-time i18n. | One build serves both English and Arabic, and the language can change without reloading the page. | — | Decided |
| A5 | Fonts (Open Sans, Noto Sans Arabic) and icons are **self-hosted**, not loaded from Google's CDN. | Lets a strict Content-Security-Policy work, needs no third-party requests, and works on locked-down campus networks. | University IT | Decided |
| A6 | Ports: Postgres **5433**, API dev **5080**, Angular dev **4200**, full stack **8088**. | All were checked as free on this machine. 5433 avoids clashing with a local Postgres. | — | Decided |
| A7 | Production images: PostgreSQL **17**, nginx serving the built app, and the API on the .NET 10 ASP.NET runtime image. | Current supported versions. | University IT | Open |

## Authentication and security

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| B1 | Local sign-in uses a **username** (for example `student.new`), not an email or university ID. | Demo accounts have no email addresses. SSO will replace this anyway. | Registrar / IT | Open |
| B2 | A sign-in lasts **8 hours** (`Jwt:LifetimeMinutes` = 480), with no refresh token. | Covers a working day of registration. Configurable. | University IT | Open |
| B3 | The browser keeps the access token in `localStorage`. | Simple, and works with the bearer interceptor. With SSO (M8), this should move to the identity provider's flow or to secure cookies. | University IT (security) | Open |
| B4 | Sign-in is limited to **10 attempts per minute per IP** (200 in Development, where automated screenshots sign in repeatedly). There is no per-account lockout yet. | Slows down password guessing without locking out shared campus IPs. | University IT (security) | Open |
| B5 | Passwords use PBKDF2-HMAC-SHA256 with **600,000 iterations** (the OWASP 2023 minimum). | Configurable. Each hash records its own iteration count, so the number can be raised later. | — | Decided |
| B6 | Swagger is **off** outside Development. When it is off, `/api/swagger` gets the same "sign-in required" response as any unknown API path. | Don't advertise the API surface in production. | — | Decided |
| B7 | Demo accounts exist only when `DemoSeed:Enabled` is true. It defaults to false, and `.env.example` sets it to true for local Docker only, with a warning comment. | Required by the brief. | — | Decided |
| B8 | Sign-in uses the **university Microsoft account (Entra ID)**, the same one behind "LU-AZURE" on Liwa's Blackboard sign-in page. It's built and switched off until IT registers the app ([docs/sso.md](sso.md)). The project owner chose this on 2026-11-09 over launching from inside Blackboard (LTI 1.3), which can still be added later. | Students use one account everywhere; we never handle their passwords. | University IT | Open |
| B9 | On first SSO sign-in, people are matched to their Smart Registration account by **university email** (the `email` claim, then `preferred_username`). After that, the account is linked permanently to their Entra object ID and is never re-linked to another person. People without an account are refused with a plain message pointing to the Registrar. | Accounts and roles come from registrar data, not from Entra. | Registrar + IT (which email format accounts carry) | Open |
| B10 | Username/password sign-in stays available next to SSO (`SSO_LOCAL_SIGNIN_ENABLED=true`) for demo accounts and emergency admin access. It can be switched off entirely. | Keeps a way in if Entra is unavailable. | University IT (security) | Open |
| B11 | Single-use SSO handoff codes are kept in the API's memory (60 seconds). That's fine for one API instance; several instances need sticky sessions or a shared store. | Simplest safe option for the current single-server deployment. | University IT (hosting) | Open |

## Users and roles

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| C1 | There are three roles: **Student, Advisor, Admin**. Each user has exactly one role. | Enough for the workflows in the brief. A staff member who is both advisor and admin would need two accounts for now. | Registrar | Open |
| C2 | Students see no engineering or status pages. System status (version, environment, database health) is for admins only. | From the brief (M1). | — | Decided |
| C3 | Demo people's names and university-ID formats (for example `202700101`, staff `E1001`) are invented. | Illustrative only, until real data arrives. | Registrar (ID format) | Open |

## Branding

| # | Assumption / decision | Why | Confirm with | Status |
|---|---|---|---|---|
| D1 | Brand colours are **charcoal `#272727`** and **sage `#b5b59e`**, sampled from the official logo on Liwa's Blackboard sign-in page (lms.lu.ac.ae). Sage is used only on charcoal (7.2:1). On light backgrounds, a deep olive `#6b6a52` is used for accents and focus rings (5.5:1). This **replaces the brief's indigo and gold**, at the project owner's request on 2026-11-09, so the app matches the university's real identity. The main site (www.lu.ac.ae) blocks automated browsers, so it couldn't be checked; exact values should come from the brand guide. | Match Liwa's real look. | University marketing (brand guide) | Open |
| D2 | The app uses the **official Liwa University logo** (shield "LU" mark with the bilingual wordmark, "Est 1993"), taken from the Blackboard sign-in page and converted to transparent PNGs: `logo.png` (full lockup, on the sign-in page), `logo-mark.png` (shield, in the top bar), and `icon-64.png`, `icon-192.png` and `favicon.ico` (shield on charcoal). The source is a 500 px JPEG, so these should be replaced with vector files from marketing. | Approved by the project owner on 2026-11-09. | University marketing (logo files, usage rules) | Open |
