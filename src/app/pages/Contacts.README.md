# Contacts (`/contacts`)

List, search, create, and edit contacts. Used when sending copy to CRM and in Activities filter.

- **API:** `getContacts`, `createContact`, `updateContact`, `getCompanies` (for edit dialog).
- **State:** contacts, companies, loading, search, dialog (create/edit).
- **Conventions:** Toasts use `messages`. Search shows count and no-results hint. Data load uses `cancelled` cleanup on unmount. Rows clickable → navigate to Send to CRM with contact context.
