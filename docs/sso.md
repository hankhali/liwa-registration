# Sign in with your Liwa account (Microsoft Entra ID)

**For:** Liwa University IT (identity / Microsoft 365 administrators).
**What we need:** one app registration in Liwa's Microsoft Entra ID tenant, and three values back.

Smart Registration signs people in with the same university Microsoft account they already use for Blackboard ("LU-AZURE" on lms.lu.ac.ae) and university email. It uses standard OpenID Connect (authorization code flow with PKCE). It **never** sees or stores anyone's password, and it never keeps Microsoft's tokens. It only reads who the person is, then issues its own short-lived session.

## 1. Register the app (about 10 minutes)

In the [Microsoft Entra admin center](https://entra.microsoft.com): **Identity → Applications → App registrations → New registration**.

| Setting | Value |
|---|---|
| Name | `Liwa Smart Registration` |
| Supported account types | **Accounts in this organizational directory only** (Liwa University only, single tenant) |
| Redirect URI, platform | **Web** |
| Redirect URI | `https://<smart-registration-address>/api/auth/sso/callback` |

Add one redirect URI per environment, for example:

- Production: `https://registration.lu.ac.ae/api/auth/sso/callback` (or whatever address is chosen)
- Test (optional): `https://registration-test.lu.ac.ae/api/auth/sso/callback`
- Developer machine (optional): `http://localhost:4200/api/auth/sso/callback`

Then:

1. **Authentication:** leave "Access tokens" and "ID tokens" (implicit grant) **unticked**. The app uses the code flow, not the implicit flow.
2. **Certificates & secrets → New client secret.** Choose an expiry that fits your policy (for example 12 or 24 months) and note the renewal date. Copy the secret **value** straight away; it's shown only once.
3. **API permissions:** the default **Microsoft Graph → User.Read (delegated)** is enough. The app requests only the `openid`, `profile` and `email` scopes. Click **Grant admin consent for Liwa University** so people aren't asked to consent one by one.
4. **Token configuration → Add optional claim → ID token → `email`** (recommended). This helps match people to their records.

### Optional: limit who can sign in

Under **Enterprise applications → Liwa Smart Registration → Properties**, set **Assignment required = Yes**, then assign the student and staff groups under **Users and groups**. Without this, any Liwa account can reach the app's sign-in. Even then, the app only lets in people who already have a Smart Registration account (see section 3).

## 2. Send us three values

Send these through a secure channel, never in plain email:

| Value | Where to find it |
|---|---|
| **Directory (tenant) ID** | App registration → Overview |
| **Application (client) ID** | App registration → Overview |
| **Client secret value** | The value copied in step 2 |

There are two ways to put them in, and neither stores the secret in source code:

- **In the app** (recommended): an administrator opens **Settings → University sign-in**, enters the three values, saves, and clicks **Test connection** (it reads the tenant's public sign-in metadata; no user data is sent). The secret is encrypted at rest and only ever shown as its last four characters. Saved values apply when the service next starts, so nobody is signed out mid-session.
- **As server configuration**: `Sso__Enabled=true`, `Sso__TenantId`, `Sso__ClientId`, `Sso__ClientSecret` (environment variables or `appsettings.Production.json`; see [INSTALL.md](INSTALL.md)). Values saved in the app take precedence at start-up.

**Required claims:** `oid` (object ID, used for the permanent link) and `email` or `upn` / `preferred_username` (used once, for the first match). The app reads nothing else.

## 3. Who gets in

Signing in with Microsoft proves *who* someone is. Smart Registration then decides *whether* they get in:

- Only people who **already have a Smart Registration account** get in. Accounts come from registrar data (PeopleSoft): students, advisors and staff, each with their role.
- On a person's **first** sign-in, the app matches them to their account by **university email address**. It uses the `email` claim, or `preferred_username` if no email is present.
- After that, the account is **permanently linked** to that person's Entra object ID. Later sign-ins use the link, so a changed display name or email alias doesn't matter.
- If an email address is later given to a **different** person, the app refuses to sign them into the old account and logs a warning. It never re-links silently.
- Anyone else sees: *"We couldn't find your Liwa account in Smart Registration. Please contact the Registrar's Office."*

**Question for IT and the Registrar:** which email do accounts use: student-ID style (`202700101@lu.ac.ae`), name style, or both? The registrar's import must carry the same address Entra reports.

## 4. What happens during sign-in

```
Browser            Smart Registration                 Microsoft Entra ID (Liwa tenant)
   | "Sign in with your Liwa account"
   |---------------> /api/auth/sso/start
   |<--------------- redirect (state, nonce, PKCE challenge)
   |------------------------------------------------------> Microsoft sign-in page (MFA as configured)
   |<------------------------------------------------------ redirect with one-time code
   |---------------> /api/auth/sso/callback
   |                 swaps code for ID token (server-to-server, with PKCE verifier + client secret)
   |                 validates signature, issuer (Liwa tenant), audience, nonce, expiry
   |                 /api/auth/sso/complete: finds the person's account, issues our session
   |<--------------- redirect to /login?sso_code=<single-use, 60-second code>
   | app trades the code for its session at /api/auth/sso/exchange
```

- **Data read from Microsoft:** object ID (`oid`), tenant ID (`tid`), name, email / `preferred_username`. Nothing else, and no Graph calls.
- **Data kept:** the link between the account and `entra:<tenant>:<object id>`, and the last sign-in time.
- **Multi-factor authentication and Conditional Access** apply as usual, because they happen on Microsoft's side.
- The app asks Microsoft to **always show the account picker** (`prompt=select_account`), which helps on shared campus computers.

## 5. Running the site behind a proxy or load balancer

- The site must be served over **HTTPS** in production, and the redirect URI registered in Entra must match the public address exactly.
- If TLS ends at a load balancer in front of the app's nginx, the load balancer must pass `X-Forwarded-Proto: https` and the original `Host` header. Otherwise the app builds an `http://` or wrong-host redirect URI and Microsoft rejects it (`AADSTS50011`).
- The single-use sign-in codes are held in the API's memory. With **more than one API instance**, either use sticky sessions or replace the in-memory store (`InMemorySignInHandoff`) with a shared one, such as Redis or a database table.

## 6. Username and password sign-in

Username/password accounts stay available next to the Microsoft button, for demo accounts and as emergency admin access if Entra is unavailable. **A settings change can never lock people out:** in Settings, username/password sign-in can only be switched off when university sign-in is on, its connection test has passed, and the administrator ticks "I understand". As server configuration the switch is `Sso__LocalSignInEnabled=false`.

## SAML alternative

If the university prefers SAML 2.0 to OpenID Connect, the same Entra app registration can be created as an *Enterprise application (non-gallery) → SAML*, with the Entity ID `https://<address>/api/auth/saml` and the Reply URL `https://<address>/api/auth/saml/acs`, releasing `user.objectid` and `user.mail` as claims. The application keeps the same matching rules (email on first sign-in, then the permanent object-ID link); only the protocol handler changes (an ASP.NET Core SAML2 handler in place of the OpenID Connect one, feeding the same external sign-in code in `src/Liwa.Application/Auth/ExternalSignIn.cs`). OpenID Connect is recommended because it needs no certificate exchange and is what is already built and tested.

## 7. Checklist

- [ ] App registration created (single tenant, Web platform)
- [ ] Redirect URI(s) added for each environment
- [ ] Client secret created and expiry date recorded
- [ ] Admin consent granted for User.Read
- [ ] Optional `email` claim added to the ID token
- [ ] (Optional) Assignment required + groups assigned
- [ ] Tenant ID, client ID and secret sent securely
- [ ] Email format used by accounts agreed with the Registrar
