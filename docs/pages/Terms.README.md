# Terms

**Route:** `/terms`  
**File:** `Terms.tsx`

## Purpose

Terms of service: agreement, use license, service description, acceptable use, content ownership, CRM integration, payment, disclaimers, limitation of liability, indemnification, termination, changes, governing law, contact.

## Behavior

- Custom header: “Back to Home” (→ `/`), Cadence logo (→ `/`). No AppHeader.
- Main content: Agreement to Terms; Use License; Service Description; Acceptable Use; Content Ownership; CRM Integration; Payment Terms; Disclaimers; Limitation of Liability; Indemnification; Termination; Changes to Terms; Governing Law; Contact Information.
- Uses `MAIN_CONTENT_ID` for skip link.

- Governing law: the law of the country where the operator has its seat, unless the operator's
  agreement with the club says otherwise. (It used to name California; the operator's counsel
  should confirm the terms as a whole.)
- Contact: the operator named by the server's settings, not a placeholder address.

## API / Data

`GET /api/public/privacy` for the operator's name, address and email (the same facts the privacy
notice prints, from `Privacy:OperatorName`, `OperatorAddress` and `OperatorEmail`). Otherwise
static legal content.
