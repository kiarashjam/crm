# Activities (`/activities`)

List activities (calls, meetings, emails, notes). Filter by all, by contact, or by deal. Log new activity with optional contact/deal link.

- **API:** `getActivities`, `getActivitiesByContact`, `getActivitiesByDeal`, `createActivity`, `deleteActivity`, `getContacts`, `getDeals`.
- **State:** activities, contacts, deals, loading, filter, filterContactId, filterDealId, dialog, delete confirm.
- **Conventions:** Toasts use `messages`. Data-load and contacts/deals fetch use `cancelled` cleanup on unmount.
