# Frontend App (Cadence)

React + TypeScript SPA for **Cadence** — CRM with Intelligent Sales Writer (sales emails, follow-ups, CRM notes, deal messages, workflow messages).

## Structure

| Folder / File | Purpose |
|---------------|--------|
| **`api/`** | API client: connection, contacts, Intelligent Sales Writer, copy history, CRM, deals, messages, settings, templates, types |
| **`components/`** | Shared UI: `AppHeader`, `EmptyState`, `LoadingSpinner`, `SkipLink`, `ui/` (shadcn-style primitives) |
| **`lib/`** | Utilities (e.g. `auth.ts` for demo user) |
| **`pages/`** | Route-level page components; see [pages/README.md](./pages/README.md) and [reports/FRONTEND_PAGES_REPORT.md](./reports/FRONTEND_PAGES_REPORT.md) |

## Routing

Defined in `App.tsx` with React Router:

- `/` — Homepage (landing)
- `/login` — Sign in (demo / Google)
- `/connect` — CRM connection
- `/dashboard` — Intelligent Sales Writer (generates and displays copy inline)
- `/send` — Send copy to CRM (contact/deal)
- `/templates` — Template library
- `/history` — Copy history
- `/settings` — User settings & account
- `/help` — How it works
- `/privacy` — Privacy policy
- `/terms` — Terms of service

## Reading and writing data

- Pages read through the hooks in `hooks/queries` (TanStack Query). Every key starts with the workspace and the signed-in user (`queryKeys.ts`), so one workspace's rows never show under another's. Each read gets the query's `signal`, so a read nobody needs any more (a search typed over, a page left) is cancelled, and gives up after 30 seconds.
- A failed read is shown with `components/LoadFailed` and a retry, never as an empty list.
- Every write goes through `authFetch`, which announces which kinds of record it may have changed (`api/dataChanged.ts`); `hooks/queries/invalidation.ts` then re-reads only those, on every page and in the assistant. When the server starts changing other records on a write, add them to `CHANGES` in `dataChanged.ts`.
- A page that shows its own change at once writes it with `useCacheWriter` (`writeOwnChange`), never `setQueryData` directly: the writer drops a read of that list already on its way, which could not hold the change, and leaves a list that was never read alone.
- The workspace belongs to the browser tab (`lib/auth.ts`), and a page starts afresh when its tab switches workspace (`components/WorkspaceOutlet.tsx`).
- `apiWithFallback` keeps records in the browser for demo data only. Against the real API, a route the server lacks is an error, never a local save.

## Tech

- **React 18**, **TypeScript**, **Vite**
- **TanStack Query** for reads (see above)
- **React Router** (BrowserRouter)
- **Tailwind CSS**, **shadcn/ui**-style components
- **Sonner** for toasts
- **Lucide** icons

## Docs & Reports

- **[pages/README.md](./pages/README.md)** — Index and per-page READMEs
- **[reports/README.md](./reports/README.md)** — Index of all frontend and full-stack reports
- **[reports/FRONTEND_PAGES_REPORT.md](./reports/FRONTEND_PAGES_REPORT.md)** — Frontend pages report (routes, purpose, APIs)
- **[reports/CRM_SALES_CHECKLIST.md](./reports/CRM_SALES_CHECKLIST.md)** — CRM sales checklist (contacts, deals, copy, connection, gaps)
- **[PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md)** (repo root) — Every aspect of the project in one place