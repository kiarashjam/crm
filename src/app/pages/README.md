# Pages

Route-level components for the Cadence frontend. Each page has a short README below (where present) and is summarized in [../reports/FRONTEND_PAGES_REPORT.md](../reports/FRONTEND_PAGES_REPORT.md).

## Route index

| Route | Page | README | Purpose |
|-------|------|--------|---------|
| `/` | Homepage | [Homepage.README.md](./Homepage.README.md) | Landing: hero, features, CTA to sign in |
| `/login` | Login | [Login.README.md](./Login.README.md) | Sign in / register / 2FA; demo when no backend |
| `/connect` | Connection | [Connection.README.md](./Connection.README.md) | Connect CRM account; skip or continue → settings |
| `/dashboard` | Dashboard | [Dashboard.README.md](./Dashboard.README.md) | Generate copy: type, goal, context, length → displays inline |
| `/send` | SendToCrm | [SendToCrm.README.md](./SendToCrm.README.md) | Attach copy to contact or deal; no-results hint under search |
| `/templates` | Templates | [Templates.README.md](./Templates.README.md) | Browse templates → use on dashboard |
| `/history` | History | [History.README.md](./History.README.md) | List, search, copy, regenerate, send again; no-results hint |
| `/leads` | Leads | [Leads.README.md](./Leads.README.md) | List, search, add, edit, delete, convert leads; count; no-results hint |
| `/deals` | Pipeline | [Pipeline.README.md](./Pipeline.README.md) | Kanban deals by stage; move, create, edit, delete; try/catch on move |
| `/tasks` | Tasks | [Tasks.README.md](./Tasks.README.md) | List tasks (All/Pending/Overdue); add, edit, complete; link to lead/deal |
| `/activities` | Activities | [Activities.README.md](./Activities.README.md) | List activities; filter by contact/deal; log call/meeting/email/note |
| `/contacts` | Contacts | [Contacts.README.md](./Contacts.README.md) | List, search, add, edit contacts; no-results hint |
| `/companies` | Companies | [Companies.README.md](./Companies.README.md) | List, search, add, edit companies; no-results hint |
| `/settings` | Settings | [Settings.README.md](./Settings.README.md) | Brand, CRM connection, 2FA, logout, delete account |
| `/help` | Help | [Help.README.md](./Help.README.md) | How it works, FAQ |
| `/privacy` | Privacy | [Privacy.README.md](./Privacy.README.md) | Privacy policy |
| `/terms` | Terms | [Terms.README.md](./Terms.README.md) | Terms of service |

## Conventions (all pages)

- **Accessibility:** All pages use `MAIN_CONTENT_ID` from `SkipLink` for skip-to-main-content; `<main id={MAIN_CONTENT_ID}>` and `tabIndex={-1}`.
- **Toasts:** Success and error messages use centralized `messages` from `@/app/api` (e.g. `messages.success.leadCreated`, `messages.errors.generic`, `messages.validation.nameRequired`).
- **Search / list:** Pages with search (Leads, Contacts, Companies, History, SendToCrm) show a no-results hint under the search box when the query returns no results.
- **Async safety:** Data-loading `useEffect`s use a `cancelled` flag and cleanup so `setState` is not called after unmount; copy timeouts (History) are cleared on unmount.
