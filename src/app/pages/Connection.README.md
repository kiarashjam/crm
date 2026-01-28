# Connection

**Route:** `/connect`  
**File:** `Connection.tsx`

## Purpose

Connect or confirm CRM account. Shows connection status, “Connect” action, and when connected offers “Continue” to onboarding.

## Behavior

- On load: `getConnectionStatus()` → show connected vs not; loading spinner until resolved.
- When not connected: bullet list (read contacts/deals, create emails/notes, update workflow messages), “Connect” button (`setConnectionStatus({ connected: true, accountEmail: user?.email ?? 'company@example.com' })`, toast), “Skip for now” link to `/dashboard`.
- When connected: “Successfully Connected!”, connected account email, “Continue” button → `navigate('/onboarding')`.
- Uses `getDemoUser()` for account email when connecting.
- Uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **GET:** `getConnectionStatus()` — `{ connected, accountEmail? }`
- **POST:** `setConnectionStatus({ connected, accountEmail })`
