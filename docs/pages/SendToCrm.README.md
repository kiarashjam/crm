# Send to CRM

**Route:** `/send`  
**File:** `SendToCrm.tsx`

## Purpose

Attach generated copy to a contact or deal in the CRM. User picks object type (contact/deal/workflow/email), selects a record when applicable, and sends.

## Behavior

- Receives `location.state`: `{ copy?, copyTypeLabel? }`. If no copy and not yet sent, shows EmptyState (“No copy to send”, “Go to Dashboard” → `/dashboard`) with `main id={MAIN_CONTENT_ID}` and sr-only h1 for accessibility.
- Object type: Contact, Deal, Workflow, Email draft. Only Contact and Deal show record list + search; Workflow and Email show amber notice: “In the demo, select Contact or Deal to send your copy…”
- Loads `getContacts()` and `getDeals()` with `cancelled` cleanup; search filters by name/email or name/value. No-results hint under search when query returns no records. User selects one record; “Confirm & Send” calls `sendCopyToCrm(...)` → toast (`messages`) → success view.
- Success view: “Successfully Sent!”, sent-to record card, “Create Another” (→ `/dashboard`), “View History” (→ `/history`).
- AppHeader; uses `MAIN_CONTENT_ID` for skip link when not in empty state.

## API / Data

- **GET:** `getContacts()`, `getDeals()`
- **POST:** `sendCopyToCrm({ objectType, recordId, recordName, copy, copyTypeLabel })`
