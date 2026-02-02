# Dashboard

**Route:** `/dashboard`  
**File:** `Dashboard.tsx`

## Purpose

Main copy-generation flow: choose copy type, goal, optional context, length → generate AI copy → navigate to GeneratedCopy. Also shows connection status, copy and CRM stats, and recent activity.

## Behavior

- Connection status badge: shows CRM connected or not, with link to `/connect` (Manage / Connect). Uses `getConnectionStatus()`.
- Stats cards (copy): "Generated this week", "Templates", "Sent to CRM", "~2 min" avg time saved; from `getCopyHistoryStats()`, `getTemplates().length`.
- Stats cards (CRM): When `getDashboardStats()` returns, a second row shows: "Active leads", "Active deals", "Pipeline value", "Won vs lost". Uses `getDashboardStats()`.
- Copy types (5): Sales Email, Follow-up, CRM Note, Deal Message, Workflow. User selects one.
- If type selected: "Message goal" dropdown (6 options, default first goal; e.g. Schedule a meeting, Follow up after demo), "Optional context" textarea, "Length" (Short / Medium / Long). "Generate copy" calls `getUserSettings()` then `generateCopy({ copyTypeId, goal, context, length, companyName, brandTone })` → toast → `navigate('/generated', { state: { copy, copyTypeLabel } })`.
- Template pre-fill: when `location.state.templateId` is set (from Templates), `getTemplateById(templateId)` pre-fills copy type and goal.
- Regenerate context: when `location.state.regenerateContext` is set (from GeneratedCopy or History), it pre-fills the context textarea.
- Recent activity: last 5 items from `getCopyHistory()`; "View full history" → `/history`. Empty state uses EmptyState with CTA (e.g. Try a template). Quick links: Templates, History, Settings, Connection.
- AppHeader; uses `MAIN_CONTENT_ID` for skip link (accessibility).

## API / Data

- `getTemplateById`, `getUserSettings`, `generateCopy`, `getCopyHistoryStats`, `getCopyHistory`, `getTemplates`, `getConnectionStatus`, `getDashboardStats`
