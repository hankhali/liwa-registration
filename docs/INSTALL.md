# Installing Smart Registration on a server

**For:** Liwa University IT (server administrators). A plain install on **Windows Server 2022+** or **Ubuntu 24.04 LTS**:
PostgreSQL, the .NET 10 runtime, the API as a service, and the built web app behind **IIS** or **nginx**. A Docker
packaging can be added later; nothing below depends on it.

```
Browser ──HTTPS──> IIS / nginx ──┬── /            → static files (the Angular app, dist/web/browser)
                                 └── /api/*       → http://127.0.0.1:5080 (the API service)
                                                     └── PostgreSQL (localhost:5432, database "liwa")
```

The web app and the API are served from **one address** (same origin), so no CORS setup is needed.

## 1. What to install

| Component | Version | Windows | Ubuntu |
|---|---|---|---|
| PostgreSQL | 16 or later (tested on 18) | EDB installer | `apt install postgresql` |
| ASP.NET Core runtime | 10.0 | "ASP.NET Core Runtime 10 — Hosting Bundle" (includes the IIS module) | `apt install aspnetcore-runtime-10.0` |
| Web server | IIS 10 with URL Rewrite + Application Request Routing, or nginx | IIS | `apt install nginx` |
| Python 3 | 3.10+ | for `scripts/verify_install.py` only | same |

## 2. Build the release (on a build machine)

```powershell
dotnet publish src/Liwa.Api -c Release -o dist/api
cd web; npm ci; npx ng build --configuration production --output-path ../dist/web
```

Copy `dist/api` to the server (e.g. `C:\Liwa\api` or `/opt/liwa/api`) and `dist/web/browser` to the web root
(e.g. `C:\Liwa\web` or `/var/www/liwa`).

## 3. Database

As the PostgreSQL superuser, create the role and database (change the password):

```sql
CREATE ROLE liwa LOGIN PASSWORD '<strong password>';
CREATE DATABASE liwa OWNER liwa ENCODING 'UTF8' TEMPLATE template0;
```

(`scripts/create-db.sql` does the same for a development machine, with the dev password and `CREATEDB` for tests;
don't give `CREATEDB` in production.) The API creates and upgrades the tables itself when
`Database__MigrateOnStartup=true`.

## 4. Configuration

Set these as environment variables of the service (or in `appsettings.Production.json` next to `Liwa.Api.dll`).
Environment variables use `__` between levels.

| Setting | Required | Value |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |
| `ASPNETCORE_URLS` | yes | `http://127.0.0.1:5080` (the API listens only locally; the web server is in front) |
| `ConnectionStrings__Liwa` | yes | `Host=localhost;Port=5432;Database=liwa;Username=liwa;Password=<password>` |
| `Jwt__SigningKey` | yes | 48+ random bytes, base64 (`[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))` or `openssl rand -base64 48`). The API refuses to start without it |
| `Jwt__LifetimeMinutes` | no | `480` (8 hours) |
| `Secrets__Key` | yes | 32 random bytes, base64 (`openssl rand -base64 32`). Encrypts stored secrets (SSO client secret, AI key). Keep a copy: without it those secrets must be entered again |
| `Database__MigrateOnStartup` | yes | `true` |
| `DemoSeed__Enabled` | yes | `false` (never `true` on a real server: it creates practice accounts with a shared password) |
| `RateLimiting__SignInPermitsPerMinute` | no | `10` sign-in attempts per IP per minute |
| `Swagger__Enabled` | no | `false` |
| `Sis__Provider` | no | `Database` (the only one implemented; see [peoplesoft-integration.md](peoplesoft-integration.md)) |
| `Sso__Enabled`, `Sso__TenantId`, `Sso__ClientId`, `Sso__ClientSecret`, `Sso__LocalSignInEnabled` | no | University sign-in; usually set in the app instead ([sso.md](sso.md)) |
| `Ai__Enabled`, `Ai__Provider`, `Ai__Model`, `Ai__BaseUrl`, `Ai__ApiKey` | no | The AI explainer; usually set in the app instead ([explainer.md](explainer.md)). `mock` is refused outside Development |
| `UI_ARABIC_DIGITS` | no | `true` for Arabic-Indic digits in the Arabic UI (default Western digits) |
| `CreditPolicy__*` | no | Starting credit rules (only used to create the first policy; admins edit them in Settings) |

**The first administrator.** With demo data off there is no account yet. Create one with the same configuration as the
service (it migrates the database first, then exits):

```powershell
$env:LIWA_ADMIN_PASSWORD = '<at least 12 characters>'
dotnet C:\Liwa\api\Liwa.Api.dll create-admin registrar.admin "Registrar Administrator"
Remove-Item Env:LIWA_ADMIN_PASSWORD
```

Then sign in, add the advisors on the **Users** page, and load the data on **Imports & exports** (in this order:
semesters, courses, programs, degree plans, class times, students, transcripts; format in
[import-contract.md](import-contract.md)). Students sign in with their university account once SSO is on.

## 5. The API as a service

**Windows (service):**

```powershell
sc.exe create LiwaApi binPath= "\"C:\Program Files\dotnet\dotnet.exe\" C:\Liwa\api\Liwa.Api.dll" start= auto
# set the environment variables for the service (registry key HKLM\SYSTEM\CurrentControlSet\Services\LiwaApi, value Environment, REG_MULTI_SZ)
sc.exe start LiwaApi
```

(Alternatively host the API in IIS itself with the ASP.NET Core Module: an application pointing to `C:\Liwa\api`
with "No managed code".)

**Ubuntu (systemd)** — `/etc/systemd/system/liwa-api.service`:

```ini
[Unit]
Description=Liwa Smart Registration API
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/liwa/api
ExecStart=/usr/bin/dotnet /opt/liwa/api/Liwa.Api.dll
Restart=always
User=liwa
EnvironmentFile=/etc/liwa/api.env
SyslogIdentifier=liwa-api

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload && sudo systemctl enable --now liwa-api
```

## 6. The web app and TLS

**nginx** (`/etc/nginx/sites-available/liwa`):

```nginx
server {
    listen 443 ssl http2;
    server_name registration.lu.ac.ae;
    ssl_certificate     /etc/ssl/liwa/fullchain.pem;
    ssl_certificate_key /etc/ssl/liwa/privkey.pem;

    root /var/www/liwa;
    index index.html;

    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;
    add_header Referrer-Policy strict-origin-when-cross-origin always;
    add_header Content-Security-Policy "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; font-src 'self'; frame-ancestors 'none'" always;
    add_header Strict-Transport-Security "max-age=31536000" always;

    location /api/ {
        proxy_pass http://127.0.0.1:5080;
        proxy_set_header Host $http_host;               # keep the port, for sign-in redirects
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        client_max_body_size 10m;                        # import files
    }

    location / {
        try_files $uri $uri/ /index.html;                # Angular routes
    }
}
server { listen 80; server_name registration.lu.ac.ae; return 301 https://$host$request_uri; }
```

**IIS:** create a site with the web root `C:\Liwa\web` and the HTTPS binding (university certificate). With URL Rewrite
+ ARR (proxy enabled at server level): rule 1 `^api/(.*)` → `http://127.0.0.1:5080/api/{R:1}` (preserve host header),
rule 2 for everything that isn't a file or folder → `/index.html`. Add the same security headers as above.

## 7. Check the install

```bash
python3 scripts/verify_install.py --api https://registration.lu.ac.ae --web https://registration.lu.ac.ae
```

On a real server (no demo data) the demo-account checks fail by design; run the full check on a staging copy with
`DemoSeed__Enabled=true` first. It reads only: migrations in order, seed loaded, sign-ins, the planner for each demo
student, advisor and admin pages, and the web app with its `/api` proxy.

## 8. Backups

The database is the only state (plus `Secrets__Key` and `Jwt__SigningKey`).

```bash
# nightly at 01:30, keep 30 days
30 1 * * * pg_dump -h localhost -U liwa -Fc liwa > /backup/liwa-$(date +\%F).dump && find /backup -name 'liwa-*.dump' -mtime +30 -delete
```

Windows: a scheduled task running `"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -h localhost -U liwa -Fc -f D:\backup\liwa-%date%.dump liwa`
(use a `.pgpass` / `pgpass.conf` file for the password).

**Restore test (do it once a term):**

```bash
createdb -h localhost -U postgres liwa_restore_test
pg_restore -h localhost -U postgres -d liwa_restore_test /backup/liwa-<date>.dump
psql -h localhost -U postgres -d liwa_restore_test -c "select count(*) from student_records"
dropdb -h localhost -U postgres liwa_restore_test
```

## 9. Upgrades

1. Back up the database (section 8).
2. Build the new release (section 2) and stop the service.
3. Replace `api/` and the web root with the new files (keep `appsettings.Production.json` / the environment file).
4. Start the service: with `Database__MigrateOnStartup=true` the database is upgraded on start (migrations are applied in order; they never drop data).
5. Run `scripts/verify_install.py` (on staging with demo data; on production at least the health, sign-in and admin checks).

## 10. Logs

The API writes to the console: the Windows Event Log / service output, or `journalctl -u liwa-api` on Ubuntu.
Errors carry a `traceId` that also appears in the error response the user saw. Email notifications are written to the
log as stubs ("Email stub … to user …") until a mail server is configured; they carry the user id, never the address.
Explainer requests are counted in the database (`ai_requests`) without any content.
