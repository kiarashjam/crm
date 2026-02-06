# Templates

**Route:** `/templates`  
**File:** `Templates.tsx`

## Purpose

Browse quick-start templates by category. “Use template” opens dashboard with that template pre-selected.

## Behavior

- On load: `getTemplates()` → list templates; loading spinner until resolved. Category icons: Sales (Mail), Follow-up (MessageSquare), Meetings (Calendar), Re-engagement (RotateCcw).
- Each card: category badge, title, description, “Used X times”, “Use template →” → `navigate('/dashboard', { state: { templateId: template.id } })`.
- Bottom section: “How Templates Work” (pre-fill generator; customize before generating), link “Learn more about templates →” to `/help`.
- AppHeader; uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **GET:** `getTemplates()` — returns `Template[]`

## Conventions

- Uses `MAIN_CONTENT_ID` for skip link; AppHeader. Data load uses `cancelled` cleanup on unmount.
