# Settings

**Route:** `/settings`  
**File:** `Settings.tsx`

## Purpose

User settings (company name, brand tone), CRM connection status, logout, and delete-account action.

## Behavior

- On load: `getUserSettings()` (fallback `{ companyName: 'Acme Corporation', brandTone: 'professional' }`), `getConnectionStatus()` → show form and connection status. While `settings === null`, shows full-page LoadingSpinner.
- “Brand Settings”: company name input, brand tone radios (Professional, Friendly, Persuasive), “Save Changes” → `saveUserSettings(settings)` → toast.
- “CRM Connection”: Connected / Not connected with status dot; “Reconnect” or “Connect” link to `/connect`.
- “Account Actions”: “Logout” link to `/login`; “Delete Account” — first click shows “Click again to confirm deletion”, second click `navigate('/')` (no API call).
- AppHeader; uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **GET:** `getUserSettings()`, `getConnectionStatus()`
- **POST:** `saveUserSettings(settings)`
