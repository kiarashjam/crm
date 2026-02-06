# Generated Copy

**Route:** `/generated`  
**File:** `GeneratedCopy.tsx`

## Purpose

View and edit generated copy, copy to clipboard, adjust tone (shorter / friendlier / persuasive), regenerate, or send to CRM.

## Behavior

- Receives `location.state`: `{ copy?, copyTypeLabel? }`. If no copy, uses `DEFAULT_COPY` (placeholder sales email). Editable textarea.
- Actions: “Copy” (clipboard + toast); “Adjust Copy” panel: “Make shorter”, “Make friendlier”, “Make more persuasive” (replace text inline with preset variants), “Regenerate” (navigate to `/dashboard` with `state: { regenerateContext: generatedText.slice(0, 200) }`); “Send to CRM” (navigate to `/send` with current `generatedText` and `copyTypeLabel`).
- Tip box: “You can edit the text directly or use the adjustment buttons to refine your copy.”
- Uses `MAIN_CONTENT_ID` for skip link; AppHeader.

## API / Data

- No direct API calls. Uses `navigator.clipboard.writeText` for copy; navigation only for regenerate/send.
