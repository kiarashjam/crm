# Running the CRM From Scratch (No External Connection)

This report explains how to run the CRM **from scratch** on your machine—without connecting to any external service (no Azure, no remote API, no cloud database). It also describes what would need to change if you want “from scratch” to be the default or only option.

---

## This system is standalone

**The ACI CRM is a standalone application.** It does **not** require any external service to run:

| Requirement | Standalone behavior |
|-------------|---------------------|
| **Backend API** | Optional. If `VITE_API_URL` is not set, the frontend uses **demo mode**: mock data and localStorage; no backend or database needed. |
| **Database** | Optional. Only needed when using the real backend (Option B). Default is **LocalDB** or **Docker** on your machine—no cloud DB required. |
| **Auth** | Self-contained. **Demo mode:** frontend-only (demo user in localStorage). **Full stack:** backend issues its own JWT; no Google/OAuth or external identity provider required. |
| **Copy generation** | Template-based in-process; no external LLM or API call required. |
| **External CRM** | Not required. “Connect your CRM” on the Connection page is a **local setting** (connection status stored per user); no third-party CRM is required to use the app. |

You can run the entire CRM **on one machine** with no internet dependency for core features (except npm/dotnet install and optional deployment).

---

## 1. What “From Scratch” vs “Connecting From Somewhere” Means Here

| Concept | Meaning in this project |
|--------|--------------------------|
| **From scratch** | Run the CRM entirely on your computer: database (Docker or LocalDB), backend API, and frontend. No deployment, no cloud, no remote URLs. |
| **Connecting from somewhere** | Using a **remote** backend or database: e.g. setting `VITE_API_URL` to an Azure Web App URL, or pointing the backend at Azure SQL. That is optional and only needed for deployment (see `DEPLOY.md`). |

You do **not** need to connect the CRM “from somewhere.” You can ignore `DEPLOY.md`, Azure scripts, and GitHub Actions unless you later decide to publish the app.

---

## 2. Two Ways to Run the CRM From Scratch

### Option A: Demo mode only (frontend + mock data)

- **No backend, no database.**  
- Good for: trying the UI, flows, and copy generation without setting up anything else.

**Steps:**

1. From the repo root: `npm install` then `npm run dev`.
2. Open the app (e.g. http://localhost:5173).
3. On the login page, click **“Try demo (no backend)”**.  
   You get a demo user and the app uses **mock data** (contacts, deals, templates, etc. from `src/app/api/mockData.ts`). No `VITE_API_URL` is required.

**Behavior:**

- Leave `.env` empty or omit `VITE_API_URL`. The frontend will **not** call a real API.
- All CRM data is in-memory/localStorage and mock data; it is not persisted in a real database.

---

### Option B: Full stack from scratch (database + backend + frontend)

- **Everything runs locally:** database (Docker or LocalDB), backend API, frontend.
- Good for: real sign-in, persisted contacts/deals/templates, and the full CRM behavior.

**Steps:**

1. **Database**
   - **Docker (recommended):** From repo root run `docker compose up -d`. Wait ~15 seconds. Database is on `localhost:1433`.
   - **Or** use **SQL Server LocalDB** (no Docker). The backend’s `appsettings.json` / `appsettings.Development.json` already point to `(localdb)\mssqllocaldb` by default.

2. **Backend**
   - If using **Docker** for the DB, set the connection string (see `LOCAL_DEV.md`), for example in PowerShell:
     ```powershell
     $env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=ACI;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"
     ```
   - From repo root:
     ```bash
     cd backend
     dotnet run --project src/ACI.WebApi/ACI.WebApi.csproj
     ```
   - API will be at **http://localhost:5160** (or the port printed in the console). On first run, migrations and seed data are applied.

3. **Frontend**
   - In a **second terminal**, from repo root, create a `.env` file with:
     ```
     VITE_API_URL=http://localhost:5160
     ```
   - Then:
     ```bash
     npm install
     npm run dev
     ```
   - Open the app (e.g. http://localhost:5173), then **sign in or register** with email/password. The app will use the real API; no “demo (no backend)” needed for real data.

**Summary (full stack from scratch):**

| Component  | How to run                          | URL / port          |
|-----------|--------------------------------------|---------------------|
| Database  | `docker compose up -d` or LocalDB    | localhost:1433 or LocalDB |
| Backend   | `cd backend && dotnet run --project src/ACI.WebApi` | http://localhost:5160 |
| Frontend  | `npm run dev` with `VITE_API_URL=http://localhost:5160` | http://localhost:5173 |

---

## 3. What Would Need to Change to Emphasize “From Scratch”

If you want the project to **assume** “run from scratch” (no external connection) and make that obvious:

### 3.1 Make local API the default in development

- **Current:** `.env.example` has `VITE_API_URL=` (empty). So by default the app runs in demo/mock mode.
- **Change:** In `.env.example`, set:
  ```env
  VITE_API_URL=http://localhost:5160
  ```
  And in the README / LOCAL_DEV, state that “from scratch” means: start DB + backend, then frontend with this `.env`.  
  Effect: New clones see that the intended dev setup is “local backend,” not “connect to somewhere.”

### 3.2 One-command start (optional)

- Add a script (e.g. `scripts/start-from-scratch.ps1` or `package.json` script) that:
  1. Starts Docker DB (if Docker is used).
  2. Waits for DB to be ready.
  3. Starts the backend (e.g. `dotnet run`).
  4. Starts the frontend (`npm run dev`) with `VITE_API_URL=http://localhost:5160`.
- This doesn’t require any code change in the app; it only makes “run from scratch” a single command.

### 3.3 Docs: “From scratch” first, deploy later

- In **README.md**: Put “Run from scratch (locally)” as the main way to run the app. Move “Publish to GitHub and Azure” (or “Connecting from somewhere”) to a short “Deploy later” section that links to `DEPLOY.md`.
- In **LOCAL_DEV.md**: Add a short line at the top: “This document describes how to run the CRM from scratch on your machine (no external connection).”

### 3.4 No code change required for “from scratch”

- The app **already** supports running from scratch:
  - Backend uses `ConnectionStrings:DefaultConnection` (LocalDB or Docker).
  - Frontend uses `VITE_API_URL`; if set to `http://localhost:5160`, it talks to your local backend and does not “connect from somewhere.”
- So “from scratch” is a **configuration and documentation** choice, not a mandatory code change.

---

## 4. Summary

| Goal | What to do |
|------|------------|
| **Run CRM standalone (no external connection)** | Use **Option A** (demo + mock; no backend/DB) or **Option B** (DB + backend + frontend all local). Do not set `VITE_API_URL` to any remote URL; do not point the backend at Azure SQL unless you are deploying. |
| **Make “from scratch” the default** | Set `VITE_API_URL=http://localhost:5160` in `.env.example`, reorder README so local run is first, and optionally add a single start script. |
| **Ignore “connecting from somewhere”** | Skip `DEPLOY.md` and Azure scripts until you want to publish. Running standalone never requires them. |

The CRM is **standalone**: you can run it from scratch in this program (locally) without connecting it to any external service. No external API, cloud database, or identity provider is required.
