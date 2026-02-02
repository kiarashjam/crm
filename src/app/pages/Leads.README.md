# Leads (`/leads`)

List, search, create, edit, delete, and convert leads. Used for CRM lead management.

- **API:** `getLeads`, `getCompanies`, `createLead`, `updateLead`, `deleteLead`, `convertLead` (to contact/deal).
- **State:** leads, companies, loading, search, dialog (create/edit), delete confirm, convert dialog.
- **Conventions:** Toasts use `messages` (e.g. `messages.success.leadCreated`, `messages.errors.generic`). Search shows count and no-results hint. Data load uses `cancelled` cleanup on unmount.
- **Convert:** Convert lead to contact and/or deal via dialog; uses backend `/api/leads/{id}/convert` when `VITE_API_URL` is set.
