# Dashboard

**Route:** `/dashboard`  
**File:** `Dashboard.tsx`

## Purpose

Main copy-generation flow: choose copy type, goal, optional context, length → generate copy → navigate to GeneratedCopy. Shows copy and CRM stats, pipeline by stage/assignee, recent copy activity, and quick links.

## Behavior

- **Hero:** Welcome message and north-star metric (total pipeline value when backend is used, or copy generated this week in demo).
- **Stats (copy):** "Copy generated this week", "Saved templates", "Sent to contacts & deals", "~2 min avg. time saved per piece"; from `getCopyHistoryStats()`, `getTemplates().length`.
- **Stats (CRM):** When `getDashboardStats()` returns: "Active leads", "Open deals", "Deals won", "Deals lost". Pipeline value by stage and by assignee when data is available.
- **Generate copy:** Copy types (5): Sales Email, Follow-up, CRM Note, Deal Message, Workflow. User selects one → "Message goal" dropdown, "Optional context" textarea, "Length" (Short / Medium / Long). "Generate copy" → `getUserSettings()` then `generateCopy(...)` → toast → navigate to GeneratedCopy.
- **Template pre-fill:** `location.state.templateId` (from Templates) pre-fills copy type and goal via `getTemplateById(templateId)`.
- **Regenerate context:** `location.state.regenerateContext` (from GeneratedCopy or History) pre-fills the context textarea.
- **Recent copy activity:** Last 5 items from `getCopyHistory()`; "View all copy history" → `/history`. Empty state CTA: "Browse templates".
- **Quick links:** Leads, Contacts, Deals, Tasks, Companies, Activities; plus Templates and Settings.
- AppHeader; `MAIN_CONTENT_ID` for skip link (accessibility).

## API / Data

- `getTemplateById`, `getUserSettings`, `generateCopy`, `getCopyHistoryStats`, `getCopyHistory`, `getTemplates`, `getDashboardStats`, `getPipelineValueByStage`, `getPipelineValueByAssignee`
