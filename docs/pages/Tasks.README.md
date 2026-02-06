# Tasks (`/tasks`)

List tasks with filters (All, Pending, Overdue). Create, edit, and toggle complete; optionally link to lead or deal.

- **API:** `getTasks` (with `overdueOnly` when filter is overdue), `createTask`, `updateTask`, `getLeads`, `getDeals` (for dialog).
- **State:** tasks, loading, filter, dialog (create/edit), lead/deal lists for dropdowns.
- **Conventions:** Toasts use `messages` (e.g. `messages.success.taskCreated`, `messages.task.completed`). Data load uses `cancelled` cleanup on unmount.
