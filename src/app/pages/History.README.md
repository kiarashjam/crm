# History

**Route:** `/history`  
**File:** `History.tsx`

## Purpose

List past generated/sent copy with search. User can copy to clipboard, “Send again” (→ /send with copy), or regenerate from context.

## Behavior

- On load: `getCopyHistory()` with `cancelled` cleanup so no setState after unmount. Search filters by copy text, type, or recipient name. Shows “X items” and no-results hint under search when query returns no results.
- Each item: type, “To: {recipientName}”, relative date, copy snippet, “Copy” (clipboard + toast; 2s timeout cleared on unmount), “Send again” (navigate /send with copy), “Regenerate” (navigate /dashboard with regenerateContext).
- Empty state: “No copy history yet” with “Create your first copy” → `/dashboard`, or “No results found” when search has no matches. Uses EmptyState component.
- Toasts use `messages` (e.g. `messages.copy.copied`, `messages.errors.generic`). AppHeader; `MAIN_CONTENT_ID` for skip link.

## API / Data

- **GET:** `getCopyHistory()` — returns `CopyHistoryItem[]`
