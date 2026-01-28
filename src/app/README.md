# Frontend App (ACI)

React + TypeScript SPA for **ACI** — AI-powered copy generation for CRM (sales emails, follow-ups, CRM notes, deal messages, workflow messages).

## Structure

| Folder / File | Purpose |
|---------------|--------|
| **`api/`** | API client: connection, contacts, copy generator, copy history, CRM, deals, messages, settings, templates, types |
| **`components/`** | Shared UI: `AppHeader`, `EmptyState`, `LoadingSpinner`, `SkipLink`, `ui/` (shadcn-style primitives) |
| **`lib/`** | Utilities (e.g. `auth.ts` for demo user) |
| **`pages/`** | Route-level page components; see [pages/README.md](./pages/README.md) and [reports/FRONTEND_PAGES_REPORT.md](./reports/FRONTEND_PAGES_REPORT.md) |

## Routing

Defined in `App.tsx` with React Router:

- `/` — Homepage (landing)
- `/login` — Sign in (demo / Google)
- `/connect` — CRM connection
- `/onboarding` — Brand tone setup
- `/dashboard` — Main copy generation
- `/generated` — View/copy generated text
- `/send` — Send copy to CRM (contact/deal)
- `/templates` — Template library
- `/history` — Copy history
- `/settings` — User settings & account
- `/help` — How it works
- `/privacy` — Privacy policy
- `/terms` — Terms of service

## Tech

- **React 18**, **TypeScript**, **Vite**
- **React Router** (BrowserRouter)
- **Tailwind CSS**, **shadcn/ui**-style components
- **Sonner** for toasts
- **Lucide** icons

## Docs & Reports

- **[pages/README.md](./pages/README.md)** — Index and per-page READMEs
- **[reports/FRONTEND_PAGES_REPORT.md](./reports/FRONTEND_PAGES_REPORT.md)** — Frontend pages report (routes, purpose, APIs)
- **[reports/CRM_SALES_CHECKLIST.md](./reports/CRM_SALES_CHECKLIST.md)** — CRM sales checklist (contacts, deals, copy, connection, gaps)
