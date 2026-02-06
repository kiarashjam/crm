# Help

**Route:** `/help`  
**File:** `Help.tsx`

## Purpose

Static “How it works”: what Cadence does, how to generate copy, connect CRM, data & privacy, and support link.

## Behavior

- Sections: “What This Tool Does”; “How to Generate Copy” (4 steps: choose type, set goal, generate and refine, send to CRM); “How CRM Connection Works” (OAuth, permissions, disconnect); “Data & Privacy” (bullets + link to Privacy Policy); “Need More Help?” (Contact Support mailto: support@example.com).
- AppHeader; uses `MAIN_CONTENT_ID` for skip link.

## API / Data

None. Static content only.

## Conventions

- Uses `MAIN_CONTENT_ID` for skip link; AppHeader.
