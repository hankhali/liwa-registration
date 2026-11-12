# Sharing the demo with a link

Use this to let someone (a reviewer, a professor) try the platform from their own browser. Nothing is installed: VS Code's built-in port forwarding gives a public `https://…devtunnels.ms` address for the app running on this machine.

## 1. Start the app (demo data on)

```powershell
# API (Development = practice accounts + the guided tour)
. .\scripts\common.ps1; Initialize-Tools
dotnet publish src/Liwa.Api -c Release -o dist/api
cd dist/api; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_URLS='http://localhost:5080'; dotnet Liwa.Api.dll

# Web (second terminal), proxies /api to the API
cd web; npx ng serve --port 4200

# Optional, once: the plan states the tour shows (advisor queue, suggested changes)
python scripts/demo_plan_states.py
```

## 2. Forward port 4200

1. In VS Code, open the **Ports** panel (View → Open View… → Ports, or the PORTS tab next to the Terminal).
2. Click **Forward a Port** and type `4200`. Sign in with GitHub or Microsoft if VS Code asks.
3. Right-click the row → **Port Visibility** → **Public**.
4. Right-click → **Copy Local Address**. That `https://…-4200.….devtunnels.ms` link is the one to send.

Only port 4200 is needed: the web server forwards `/api` to the API. `web/angular.json` allows `*.devtunnels.ms` hosts.

## 3. What the visitor does

1. Open the link. The first time, Microsoft shows a "you are about to connect to a developer tunnel" page: click **Continue**.
2. On the sign-in page, click **Start the guided tour** (or sign in as `admin` / `admin`).
3. The walkthrough goes through every step in order: the student, the advisor, the advisor's feedback, the registrar. A spotlight shows what each step is about and what to click. **Next** (or the → key) moves on.

## Keep in mind

- The link works only while this computer is on, awake and running both servers. Stop forwarding (right-click → Stop Forwarding Port) when the review is over.
- Anyone with the link can sign in with the practice accounts, including `admin` / `admin`. All data is practice data, but changes they make are saved. Reset with `scripts\reset-db.ps1 -Force` then `python scripts/demo_plan_states.py`.
- This is a demo setup (Development mode). A real deployment has no practice accounts and no tour.
