# Attach copy

**Route:** `/send`  
**File:** `SendToCrm.tsx`

## Purpose

Save a piece of writing on a lead, contact, or deal inside Cadence. The writing is stored in copy history. Nothing is emailed and nothing is sent to another system.

## Behavior

- The page is usable on its own. Paste or write the text here, or arrive with `location.state.copy` and `copyTypeLabel` from History.
- Contacts can open this page with `contactId` and `contactName`. That contact is selected, and the writing box stays empty until you add text.
- Object types are Lead, Contact, and Deal. Search matches name, email, company, status, value, or stage. Missing fields do not crash the filter.
- Lists load with `Promise.allSettled`. A failed list shows a retry line and does not wipe the lists that succeeded.
- “Save on {name}” calls `sendCopyToCrm`. Success offers Open the record, Save on someone else, and View history.

## API / Data

- **GET:** `getContacts()`, `getDeals()`, `getLeads()`
- **POST:** `sendCopyToCrm({ objectType, recordId, recordName, copy, copyTypeLabel })`
