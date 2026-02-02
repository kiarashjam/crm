# Pipeline (`/deals`)

Kanban view of deals by stage. Move deals between stages, create, edit, and delete deals.

- **Route:** `/deals` (component name Pipeline).
- **API:** `getDeals`, `getCompanies`, `getContacts`, `updateDeal`, `createDeal`, `deleteDeal`.
- **State:** deals, contacts, companies, loading, create dialog, edit dialog, delete confirm.
- **Stages:** Qualification, Proposal, Negotiation, Closed Won, Closed Lost. Moving to Closed Won/Lost sets `isWon`.
- **Conventions:** Toasts use `messages`. `handleMoveStage` wrapped in try/catch. Data load uses `cancelled` cleanup on unmount.
