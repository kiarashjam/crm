# Frontend Reports

Reports and documentation for the ACI frontend and full-stack CRM.

## Report index (every aspect)

| Report | Description |
|--------|-------------|
| [FRONTEND_PAGES_REPORT.md](./FRONTEND_PAGES_REPORT.md) | All routes, pages, purpose, API usage, and shared conventions |
| [UI_DESIGN_RATIONALE.md](./UI_DESIGN_RATIONALE.md) | Why the UI looks like this: colors, layout, components, accessibility, and design patterns |
| [PAGES_AND_COMPONENTS_IMPROVEMENTS.md](./PAGES_AND_COMPONENTS_IMPROVEMENTS.md) | Meaning of each page and component; what to add/change to make the app friendlier and better |
| [CRM_SALES_CHECKLIST.md](./CRM_SALES_CHECKLIST.md) | CRM sales POV: contacts, deals, copy types, connection, send-to-CRM, gaps and recommendations |
| [SALES_CRM_CORE_GAP_REPORT.md](./SALES_CRM_CORE_GAP_REPORT.md) | Sales CRM core must-haves: what the system has vs what it doesn’t (leads, contacts, deals, pipeline, activity, tasks, reporting, roles) |
| [USER_FLOWS_REPORT.md](./USER_FLOWS_REPORT.md) | User flows from sign-up through full CRM: new user journey, “their company,” inviting people (not implemented), all CRM flows |
| [FLOWS_BACKEND_DATABASE_VERIFICATION.md](./FLOWS_BACKEND_DATABASE_VERIFICATION.md) | Backend and database alignment: schema, entities, API contracts, frontend↔backend mapping, migrations |

## Repo-root docs (standalone, run from scratch, every aspect)

| Doc | Description |
|-----|-------------|
| [RUN_FROM_SCRATCH.md](../../RUN_FROM_SCRATCH.md) | **Standalone:** run the CRM from scratch (no external connection): demo mode or full stack locally |
| [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md) | **Every aspect** of the project in one place: **standalone system**, what it is, run from scratch, auth, frontend, backend, CRM/sales, user flows, config, deploy |

To add a new report, add a new Markdown file here and link it from [../README.md](../README.md) and [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md) if needed.
