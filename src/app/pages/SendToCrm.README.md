# Send to CRM

**Route:** `/send`  
**File:** `SendToCrm.tsx`

## Purpose

Attach generated copy to a contact or deal in the CRM. User picks object type (contact/deal/workflow/email), selects a record when applicable, and sends.

## Behavior

- Receives `location.state`: `{ copy?, copyTypeLabel? }`. If no copy and not yet sent, shows EmptyState (“No copy to send”, “Go to Dashboard” → `/dashboard`) without `MAIN_CONTENT_ID` on main.
- Object type: Contact, Deal, Workflow, Email draft. Only Contact and Deal show record list + search; Workflow and Email show amber notice: “In the demo, select Contact or Deal to send your copy…”
- Loads `getContacts()` and `getDeals()`; search filters by name/email or name/value. User selects one record; “Confirm & Send” calls `sendCopyToCrm({ objectType, recordId, recordName, copy, copyTypeLabel })` → toast → success view.
- Success view: “Successfully Sent!”, sent-to record card, “Create Another” (→ `/dashboard`), “View History” (→ `/history`).
- AppHeader; uses `MAIN_CONTENT_ID` for skip link when not in empty state.

## API / Data

- **GET:** `getContacts()`, `getDeals()`
- **POST:** `sendCopyToCrm({ objectType, recordId, recordName, copy, copyTypeLabel })`
