# Frontend Pages Report

Report of all frontend routes, pages, purpose, and API usage. Generated for the ACI app.

---

## Summary

| # | Route | Component | Auth | API Usage |
|---|-------|-----------|------|-----------|
| 1 | `/` | Homepage | No | None |
| 2 | `/login` | Login | No | Auth (demo) |
| 3 | `/connect` | Connection | Yes | getConnectionStatus, setConnectionStatus |
| 4 | `/onboarding` | Onboarding | Yes | saveUserSettings |
| 5 | `/dashboard` | Dashboard | Yes | getTemplateById, getUserSettings, generateCopy, getCopyHistoryStats, getCopyHistory, getTemplates, getConnectionStatus |
| 6 | `/generated` | GeneratedCopy | Yes | None (clipboard, nav) |
| 7 | `/send` | SendToCrm | Yes | getContacts, getDeals, sendCopyToCrm |
| 8 | `/templates` | Templates | Yes | getTemplates |
| 9 | `/history` | History | Yes | getCopyHistory |
| 10 | `/settings` | Settings | Yes | getUserSettings, getConnectionStatus, saveUserSettings |
| 11 | `/help` | Help | Yes | None |
| 12 | `/privacy` | Privacy | No | None |
| 13 | `/terms` | Terms | No | None |

---

## Per-Page Detail

### 1. Homepage (`/`)

- **File:** `pages/Homepage.tsx`
- **Purpose:** Public landing: hero, stats, How It Works (3 steps), What You Can Do (10 features), Why Choose ACI, CTA strip, footer (Product/Legal/social).
- **Components:** Sticky header (ACI logo, Sign In), hero (trust badge, headline, Get started / See how it works), stats (5 copy types, Templates, 1-click), step cards, feature grid, CTA section, footer.
- **API:** None.
- **README:** [pages/Homepage.README.md](../pages/Homepage.README.md)

### 2. Login (`/login`)

- **File:** `pages/Login.tsx`
- **Purpose:** Sign in: Google (placeholder), Demo (instant), Email (placeholder). Demo → dashboard.
- **Components:** Back to home, card with Google / Try demo / Login with Email, Privacy & Terms links, “Sign up free” → `/`.
- **API:** `setDemoUser` (lib/auth).
- **README:** [pages/Login.README.md](../pages/Login.README.md)

### 3. Connection (`/connect`)

- **File:** `pages/Connection.tsx`
- **Purpose:** Connect CRM; when connected, “Continue” → onboarding.
- **Components:** When not connected: permission bullets, Connect button, “Skip for now” → `/dashboard`. When connected: success message, account email, “Continue” → `/onboarding`. Loading spinner on initial load.
- **API:** `getConnectionStatus`, `setConnectionStatus`.
- **README:** [pages/Connection.README.md](../pages/Connection.README.md)

### 4. Onboarding (`/onboarding`)

- **File:** `pages/Onboarding.tsx`
- **Purpose:** Set company name and brand tone; save → dashboard.
- **Components:** Form (company name, brand tone: Professional / Friendly / Persuasive), “Save & Continue”, “You can change these settings anytime”.
- **API:** `saveUserSettings`.
- **README:** [pages/Onboarding.README.md](../pages/Onboarding.README.md)

### 5. Dashboard (`/dashboard`)

- **File:** `pages/Dashboard.tsx`
- **Purpose:** Main copy generation: type, goal, context, length → generate → /generated. Template/regenerate pre-fill from state.
- **Components:** AppHeader, connection badge (Manage/Connect → `/connect`), stats (Generated this week, Templates, Sent to CRM, ~2 min), copy-type grid, goal dropdown, optional context textarea, length (Short/Medium/Long), generate button, recent activity (last 5), quick links (Templates, History, Settings, Connection).
- **API:** `getTemplateById`, `getUserSettings`, `generateCopy`, `getCopyHistoryStats`, `getCopyHistory`, `getTemplates`, `getConnectionStatus`.
- **README:** [pages/Dashboard.README.md](../pages/Dashboard.README.md)

### 6. Generated Copy (`/generated`)

- **File:** `pages/GeneratedCopy.tsx`
- **Purpose:** View/edit copy, copy to clipboard, adjust (shorter/friendlier/persuasive), regenerate, send to CRM.
- **State:** `location.state`: `{ copy?, copyTypeLabel? }`; default placeholder if no copy.
- **Components:** AppHeader, editable textarea, Copy button, “Adjust Copy” (Make shorter, Make friendlier, Make more persuasive, Regenerate), Send to CRM button, tip box.
- **API:** None (clipboard, navigation).
- **README:** [pages/GeneratedCopy.README.md](../pages/GeneratedCopy.README.md)

### 7. Send to CRM (`/send`)

- **File:** `pages/SendToCrm.tsx`
- **Purpose:** Attach copy to contact or deal (workflow/email show demo notice). Success → Create Another / View History.
- **State:** `location.state`: `{ copy?, copyTypeLabel? }`. Empty state when no copy (Go to Dashboard).
- **Components:** AppHeader; object type (Contact, Deal, Workflow, Email draft); record list + search for contact/deal; Confirm & Send; success view with “Create Another” (→ `/dashboard`), “View History” (→ `/history`). EmptyState when no copy.
- **API:** `getContacts`, `getDeals`, `sendCopyToCrm`.
- **README:** [pages/SendToCrm.README.md](../pages/SendToCrm.README.md)

### 8. Templates (`/templates`)

- **File:** `pages/Templates.tsx`
- **Purpose:** Browse templates by category; “Use template” → dashboard with templateId.
- **Components:** AppHeader, template grid (category, title, description, “Used X times”, “Use template →”), “How Templates Work” section, link to /help.
- **API:** `getTemplates`.
- **README:** [pages/Templates.README.md](../pages/Templates.README.md)

### 9. History (`/history`)

- **File:** `pages/History.tsx`
- **Purpose:** List copy history; search (copy/type/recipient); copy to clipboard; regenerate (→ dashboard with context).
- **Components:** AppHeader, search input, list (type, recipient, date, snippet, Copy, Regenerate), EmptyState when empty or no search results.
- **API:** `getCopyHistory`.
- **README:** [pages/History.README.md](../pages/History.README.md)

### 10. Settings (`/settings`)

- **File:** `pages/Settings.tsx`
- **Purpose:** Brand settings, CRM connection, Logout, Delete account (two-click confirm → navigate `/`).
- **Components:** AppHeader, Brand Settings (company name, tone, Save Changes), CRM Connection (status, Reconnect/Connect → `/connect`), Account Actions (Logout → `/login`, Delete Account). LoadingSpinner while settings null.
- **API:** `getUserSettings`, `getConnectionStatus`, `saveUserSettings`.
- **README:** [pages/Settings.README.md](../pages/Settings.README.md)

### 11. Help (`/help`)

- **File:** `pages/Help.tsx`
- **Purpose:** What ACI does, How to Generate Copy (4 steps), CRM Connection, Data & Privacy, Contact Support.
- **Components:** AppHeader, sections (What This Tool Does, How to Generate Copy, How CRM Connection Works, Data & Privacy, Need More Help? with mailto).
- **API:** None.
- **README:** [pages/Help.README.md](../pages/Help.README.md)

### 12. Privacy (`/privacy`)

- **File:** `pages/Privacy.tsx`
- **Purpose:** Privacy policy (intro, collection, use, security, sharing, rights, CRM, cookies, transfers, children, changes, contact).
- **Components:** Custom header (Back to Home, ACI logo), prose sections.
- **API:** None.
- **README:** [pages/Privacy.README.md](../pages/Privacy.README.md)

### 13. Terms (`/terms`)

- **File:** `pages/Terms.tsx`
- **Purpose:** Terms of service (agreement, license, description, acceptable use, ownership, CRM, payment, disclaimers, liability, indemnification, termination, changes, governing law, contact).
- **Components:** Custom header (Back to Home, ACI logo), prose sections.
- **API:** None.
- **README:** [pages/Terms.README.md](../pages/Terms.README.md)

---

## Shared Conventions

- **Skip link:** All pages set main content `id={MAIN_CONTENT_ID}` (from `SkipLink`) for accessibility. SendToCrm empty state does not set it on main.
- **App header:** Used on Dashboard, GeneratedCopy, SendToCrm, Templates, History, Settings, Help. Not used on Homepage, Login, Onboarding, Connection, Privacy, Terms (custom or minimal headers).
- **Toasts:** Sonner via `toast.success` / `toast.error` for API and copy feedback.
- **Navigation:** React Router `Link` and `useNavigate`; state passed via `navigate(path, { state })` for copy, copyTypeLabel, templateId, regenerateContext.

---

*Last updated: January 2026*
