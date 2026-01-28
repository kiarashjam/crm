# History

**Route:** `/history`  
**File:** `History.tsx`

## Purpose

List past generated/sent copy with search. User can copy item to clipboard or regenerate from context.

## Behavior

- On load: `getCopyHistory()` → list items; loading spinner until resolved. Search filters by copy text, type, or recipient name.
- Each item: type, “To: {recipientName}”, relative date (formatDate), copy snippet (line-clamp-2), “Copy” (clipboard + toast), “Regenerate” (navigate to `/dashboard` with `state: { regenerateContext: item.copy.slice(0, 300) }`).
- Empty state: “No copy history yet” with “Create your first copy” → `/dashboard`, or “No results found” when search has no matches (no action button). Uses EmptyState component.
- AppHeader; uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **GET:** `getCopyHistory()` — returns `CopyHistoryItem[]`
