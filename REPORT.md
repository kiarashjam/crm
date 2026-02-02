# ACI CRM — Project Report (Repo Root)

## What this project is
**ACI** is a **CRM companion** that helps sales teams generate and save CRM-ready copy (sales emails, follow-ups, CRM notes, deal messages). **The system is standalone:** it does not require any external service to run (no required cloud API, no required external database; backend and database are optional or local—see [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md)).

The repo contains:
- **Frontend SPA** (`src/`) — React + TypeScript UI.
- **Backend API** (`backend/`) — ASP.NET Core Web API with EF Core + SQL Server.
- **Marketing site** (`website/`) — separate Vite app for public pages.

## Current key capabilities
- **Authentication**: JWT-based auth with **optional 2FA (TOTP)**.
- **Copy generation**: template-driven (can be swapped for LLM later).
- **CRM-like objects**: contacts, deals, leads, tasks, activities, companies, templates, copy history, settings, connection status.
- **UI flow**: login/register → dashboard generate → send → history; settings includes 2FA. Full CRM: Leads, Pipeline (deals), Tasks, Activities, Contacts, Companies.

## Security notes (important)
- **JWT** is required for most backend endpoints.
- **2FA** is optional; when enabled:
  - Login becomes **two-step**: password → TOTP code.
  - TOTP secrets are **protected** before being stored.

## Every aspect at a glance
For a **single document that lists every aspect** of the project (**standalone system**, running from scratch, auth, frontend, backend, CRM/sales, user flows, deployment), see **[PROJECT_ASPECTS.md](./PROJECT_ASPECTS.md)**.

Key docs:
- **Run from scratch (no external connection):** [RUN_FROM_SCRATCH.md](./RUN_FROM_SCRATCH.md)
- **Local dev (DB + backend + frontend):** [LOCAL_DEV.md](./LOCAL_DEV.md)
- **Deploy to Azure (optional):** [DEPLOY.md](./DEPLOY.md)

## Folder “origin” reports
Open these first when you enter a folder:
- `backend/README.md`
- `src/app/README.md`
- `src/README.md`
- `website/README.md`
- `scripts/README.md`
- `public/README.md`
- `guidelines/README.md`

Frontend reports (routes, pages, UI, CRM checklist, sales gap, user flows, backend/DB verification): `src/app/reports/README.md`.

