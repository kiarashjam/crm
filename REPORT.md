# ACI CRM — Project Report (Repo Root)

## What this project is
**ACI** is a **CRM companion** that helps sales teams generate and save CRM-ready copy (sales emails, follow-ups, CRM notes, deal messages).

The repo contains:
- **Frontend SPA** (`src/`) — React + TypeScript UI.
- **Backend API** (`backend/`) — ASP.NET Core Web API with EF Core + SQL Server.
- **Marketing site** (`website/`) — separate Vite app for public pages.

## Current key capabilities
- **Authentication**: JWT-based auth with **optional 2FA (TOTP)**.
- **Copy generation**: template-driven (can be swapped for LLM later).
- **CRM-like objects**: contacts, deals, templates, copy history, settings, connection status.
- **UI flow**: login/register → dashboard generate → send → history; settings includes 2FA.

## Security notes (important)
- **JWT** is required for most backend endpoints.
- **2FA** is optional; when enabled:
  - Login becomes **two-step**: password → TOTP code.
  - TOTP secrets are **protected** before being stored.

## Folder “origin” reports
Open these first when you enter a folder:
- `backend/README.md`
- `src/app/README.md`
- `src/README.md`
- `website/README.md`
- `scripts/README.md`
- `public/README.md`
- `guidelines/README.md`

