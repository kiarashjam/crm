# Companies (`/companies`)

List, search, create, and edit companies. Companies are used when creating leads and linking deals.

- **API:** `getCompanies`, `createCompany`, `updateCompany`.
- **State:** companies, loading, search, dialog (create/edit).
- **Conventions:** Toasts use `messages`. Search shows count and no-results hint. Data load uses `cancelled` cleanup on unmount.
