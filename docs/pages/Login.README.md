# Login

**Route:** `/login`  
**File:** `Login.tsx`

## Purpose

Sign-in page: demo login, Google (placeholder), and Email (placeholder). Demo redirects to dashboard on success.

## Behavior

- “Back to home” link to `/`.
- Buttons: “Login with Google” (placeholder), “Try demo (instant access)” (calls `setDemoUser(DEMO_USER)`, then `navigate('/dashboard', { replace: true })`), “Login with Email” (placeholder).
- Footer: “Connect your CRM in one click after signing in”; links to Privacy Policy and Terms of Service; “Don’t have an account? Sign up free” → `/`.
- Uses `MAIN_CONTENT_ID` for skip link.

## API / Data

- **Auth:** `setDemoUser` from `@/app/lib/auth` (in-memory demo user; `DEMO_USER = { name: 'Demo User', email: 'demo@example.com' }`).
