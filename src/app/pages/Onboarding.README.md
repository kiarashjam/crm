# Onboarding

**Route:** `/onboarding`  
**File:** `Onboarding.tsx`

## Purpose

First-time setup: company name and brand tone (professional / friendly / persuasive). Saves settings and redirects to dashboard.

## Behavior

- Form: company name (text, required; fallback `'My Company'` if empty), brand tone (radio: Professional, Friendly, Persuasive with short descriptions).
- Submit: `saveUserSettings({ companyName: companyName.trim() || 'My Company', brandTone })` → toast → `navigate('/dashboard')`.
- Footer text: “You can change these settings anytime”.
- Uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **POST:** `saveUserSettings({ companyName, brandTone })`
