# Cadence CRM — Every Aspect (Full Overview)

This document lists **every major aspect** of the Cadence CRM project: what it is, how to run it, what it has, and where to read more. Use it as a single entry point for understanding the whole system.

---

## 1. Standalone system

**The Cadence CRM is standalone.** It does not require any external service to run.

| Aspect | Standalone behavior |
|--------|---------------------|
| **Backend** | Optional. Without `VITE_API_URL`, frontend runs in **demo mode** (mock data, no backend/DB). With `VITE_API_URL=http://localhost:5160`, backend runs locally (LocalDB or Docker). |
| **Database** | Local only: SQL Server LocalDB or Docker. No Azure SQL or cloud DB required. |
| **Auth** | Self-contained: demo user (frontend) or JWT from the local backend. No Google/OAuth or external identity provider. |
| **Copy generation** | Template-based, in-process. No external LLM or API. |
| **External CRM** | Not required. “Connect your CRM” is a local connection status; no third-party CRM needed to use the app. |

See [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md) for how to run standalone (demo only or full stack locally).

---

## 2. What the project is

| Aspect | Description |
|--------|-------------|
| **Product** | **Cadence** — a CRM companion for sales teams: generate CRM-ready copy (sales emails, follow-ups, notes, deal messages) and send it to contacts/deals. |
| **Repo contents** | **Frontend** (`src/`) — React + TypeScript SPA. **Backend** (`backend/`) — ASP.NET Core Web API, EF Core, SQL Server. **Marketing site** (`website/`) — separate Vite app for public pages. **Scripts** (`scripts/`) — Azure deployment. |
| **Original design** | Figma: Website Structure Overview (see README). |

---

## 3. Running the CRM (from scratch vs connecting from somewhere)

| Aspect | Where it’s documented | Summary |
|--------|----------------------|---------|
| **Run from scratch (no external connection)** | [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md) | **Option A:** Demo only — `npm run dev`, no backend, “Try demo (no backend)” on login; mock data. **Option B:** Full stack — Docker or LocalDB + backend (`dotnet run` in `backend`) + frontend with `VITE_API_URL=http://localhost:5160`. |
| **Local dev (DB + backend + frontend)** | [LOCAL_DEV.md](./LOCAL_DEV.md) | Database in Docker (`docker compose up -d`), backend connection string, run backend then frontend with `VITE_API_URL`. |
| **Connecting from somewhere (deploy)** | [DEPLOY.md](./DEPLOY.md) | GitHub + Azure: Static Web App (frontend), Web App + Azure SQL (backend). Optional; not needed to run from scratch. |

---

## 4. Authentication and security

| Aspect | What exists |
|--------|-------------|
| **Auth** | JWT-based; login, register; optional **2FA (TOTP)**. When 2FA is enabled, login is two-step: password then 6-digit code. |
| **Demo mode** | If `VITE_API_URL` is not set, login page shows “Try demo (no backend)” — no backend or DB required; `setDemoUser` in frontend. |
| **Token** | Stored in `localStorage` (aci_token); sent as `Authorization: Bearer` on API calls. 401 clears session. |
| **Data isolation** | Backend filters contacts, deals, leads, tasks, activities, copy history, companies by `UserId`. No multi-tenant “organization” or team roles. |

---

## 5. Frontend (routes, pages, UI)

| Aspect | Where it’s documented | Summary |
|--------|----------------------|---------|
| **All routes and pages** | [src/app/reports/FRONTEND_PAGES_REPORT.md](./src/app/reports/FRONTEND_PAGES_REPORT.md) | 19 routes: `/` Homepage, `/login`, `/connect`, `/onboarding`, `/dashboard`, `/generated`, `/send`, `/leads`, `/deals` (Pipeline), `/tasks`, `/activities`, `/contacts`, `/companies`, `/templates`, `/history`, `/settings`, `/help`, `/privacy`, `/terms`. |
| **Per-page purpose, state, API** | Same report | Each page: file, purpose, state, handlers, API used, layout, accessibility. |
| **UI design rationale** | [src/app/reports/UI_DESIGN_RATIONALE.md](./src/app/reports/UI_DESIGN_RATIONALE.md) | Colors (orange primary, slate neutrals), layout (public vs app header), components (SkipLink, LoadingSpinner, EmptyState, AppHeader), patterns (forms, toasts, route state), animations. |
| **Improvements (meaning + checklist)** | [src/app/reports/PAGES_AND_COMPONENTS_IMPROVEMENTS.md](./src/app/reports/PAGES_AND_COMPONENTS_IMPROVEMENTS.md) | What each component/page does; P0/P1/P2 improvements; implementation status. |

---

## 6. Backend and database

| Aspect | Where it’s documented | Summary |
|--------|----------------------|---------|
| **Backend structure** | [backend/README.md](./backend/README.md) | Clean Architecture: Domain, Application, Infrastructure, WebApi. SQL Server + EF Core 8. |
| **API overview** | backend/README.md, [FLOWS_BACKEND_DATABASE_VERIFICATION.md](./src/app/reports/FLOWS_BACKEND_DATABASE_VERIFICATION.md) | Auth, Settings, Connection, Templates, Copy (generate/send), Copy history, Contacts, Companies, Deals, Leads, Tasks, Activities, Reporting (dashboard stats). |
| **Database schema and migrations** | FLOWS_BACKEND_DATABASE_VERIFICATION.md | Tables: Users, UserSettings, CrmConnections, Companies, Contacts, Deals, Leads, TaskItems, Activities, Templates, CopyHistoryItems. Migration `SalesCrmCore` adds Leads, TaskItems, Activities and columns (Contact.Phone, Deal.ExpectedCloseDateUtc, Deal.IsWon). |
| **Frontend ↔ backend alignment** | FLOWS_BACKEND_DATABASE_VERIFICATION.md | Which frontend module calls which controller; request/response shapes. |

---

## 7. CRM and sales (what the system has vs gaps)

| Aspect | Where it’s documented | Summary |
|--------|----------------------|---------|
| **CRM sales checklist** | [src/app/reports/CRM_SALES_CHECKLIST.md](./src/app/reports/CRM_SALES_CHECKLIST.md) | Contacts, deals, companies (data + UI), copy types, goals, templates, connection, send to Contact/Deal, history, dashboard, settings. Gaps: optional Pipeline in nav, companies as first-class list (now have Companies page with create/edit). |
| **Sales CRM core gap report** | [src/app/reports/SALES_CRM_CORE_GAP_REPORT.md](./src/app/reports/SALES_CRM_CORE_GAP_REPORT.md) | Must-haves: leads, contacts, companies, deals, pipeline, activities, tasks, reporting. Implemented: Leads (CRUD, delete), Pipeline (Kanban, move stage, new deal, delete), Tasks, Activities, Contacts (list, search), Companies (list, create, edit), Dashboard stats. Not implemented: user roles (rep/manager/admin), last-activity on contact/deal, contact–deal link, convert lead. |

---

## 8. User flows (from sign-up to full CRM)

| Aspect | Where it’s documented | Summary |
|--------|----------------------|---------|
| **New user journey** | [src/app/reports/USER_FLOWS_REPORT.md](./src/app/reports/USER_FLOWS_REPORT.md) | Homepage → Login (or demo) → optional Connect CRM → Onboarding (company name, brand tone) → Dashboard. |
| **“Their company”** | Same report | User’s company = name + brand tone (settings). Companies in CRM = accounts they track (customer companies). No multi-user organization or invite flow. |
| **All CRM flows** | Same report | Copy (generate, templates, adjust, send to CRM, history); Leads (list, search, add, edit, delete); Pipeline/Deals (Kanban, move stage, new deal, delete); Tasks (list, filter, add, edit, complete); Activities (list, log, filter by contact/deal); Companies (list, add, edit); Contacts (list, search); Dashboard stats; Settings (brand, connection, 2FA, logout, delete); Help, Privacy, Terms. |

---

## 9. Copy generation and send-to-CRM

| Aspect | What exists |
|--------|-------------|
| **Copy types** | Sales Email, Follow-up, CRM Note, Deal Message, Workflow Message. |
| **Goals** | Schedule a meeting, Follow up after demo, Request feedback, Share resources, Check in on progress, Close the deal. |
| **Generate** | Dashboard: type, goal, optional context, length (short/medium/long) → generate → Generated Copy page. Uses company name and brand tone from settings. Currently template-based (can be swapped for LLM). |
| **Send to CRM** | Pick Contact or Deal, confirm → copy stored in copy history (recipient, type, timestamp). Workflow/Email draft show demo notice. |
| **History** | List, search by copy/type/recipient, copy to clipboard, regenerate (→ Dashboard with context), “Send again” (→ Send to CRM with copy). |

---

## 10. Configuration and environment

| Aspect | Where / what |
|--------|--------------|
| **Frontend API URL** | `.env`: `VITE_API_URL=http://localhost:5160` (or empty for demo/mock). See [.env.example](./.env.example). |
| **Backend connection string** | `appsettings.json` or `appsettings.Development.json`: `ConnectionStrings:DefaultConnection`. Or env: `ConnectionStrings__DefaultConnection`. LocalDB default: `(localdb)\mssqllocaldb`. Docker: see LOCAL_DEV.md. |
| **JWT** | `appsettings`: Jwt:SecretKey, Issuer, Audience, ExpiryMinutes. |

---

## 11. Deployment (optional)

| Aspect | Where it’s documented |
|--------|----------------------|
| **GitHub + Azure** | [DEPLOY.md](./DEPLOY.md) — push to GitHub, workflows deploy frontend (Static Web App) and backend (Web App + Azure SQL). |
| **Scripts** | [scripts/README.md](./scripts/README.md) — Azure resource creation (West Europe, East US 2, webapp-only). |
| **Secrets** | [SECRETS_SETUP.md](./SECRETS_SETUP.md) — GitHub and Azure configuration. |

---

## 12. Folder “origin” reports (where to look first)

| Folder | Report / README |
|--------|------------------|
| Repo root | [REPORT.md](./REPORT.md), [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md), [PROJECT_ASPECTS.md](./PROJECT_ASPECTS.md) (this file) |
| backend/ | [backend/README.md](./backend/README.md) |
| src/ | [src/README.md](./src/README.md) |
| src/app/ | [src/app/README.md](./src/app/README.md) |
| src/app/reports/ | [src/app/reports/README.md](./src/app/reports/README.md) — index of all frontend reports |
| website/ | [website/README.md](./website/README.md) |
| scripts/ | [scripts/README.md](./scripts/README.md) |
| public/ | [public/README.md](./public/README.md) |
| guidelines/ | [guidelines/README.md](./guidelines/README.md) |

---

## 13. Quick reference: report by topic

| Topic | Report |
|-------|--------|
| **Standalone system** (no required external service) | RUN_FROM_SCRATCH.md, this doc §1 |
| Run from scratch (no external connection) | RUN_FROM_SCRATCH.md |
| Local dev (DB + backend + frontend) | LOCAL_DEV.md |
| All routes and pages | src/app/reports/FRONTEND_PAGES_REPORT.md |
| UI design rationale | src/app/reports/UI_DESIGN_RATIONALE.md |
| Page/component meaning and improvements | src/app/reports/PAGES_AND_COMPONENTS_IMPROVEMENTS.md |
| CRM sales checklist | src/app/reports/CRM_SALES_CHECKLIST.md |
| Sales CRM core (have vs gap) | src/app/reports/SALES_CRM_CORE_GAP_REPORT.md |
| User flows (sign-up → full CRM) | src/app/reports/USER_FLOWS_REPORT.md |
| Backend, DB, API, alignment | src/app/reports/FLOWS_BACKEND_DATABASE_VERIFICATION.md |
| Deploy to Azure | DEPLOY.md |

---

*This document is the single “every aspect” overview. Last updated: February 2026.*
