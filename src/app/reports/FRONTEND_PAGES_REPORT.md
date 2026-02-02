# Frontend Pages Report

Report of all frontend routes, pages, purpose, state, API usage, and UI structure. Generated from the ACI app source files.

---

## App Structure

- **Router:** `BrowserRouter` (React Router).
- **Global UI:** `SkipLink` (skip to main content, visible on focus); `Toaster` from Sonner (`position="top-center"`, `richColors`, `closeButton`).
- **Catch-all:** `path="*"` → `Navigate to="/" replace`.
- **Route guard:** None; all routes are reachable without auth. Auth is enforced by backend when `VITE_API_URL` is set; demo mode uses `setDemoUser` without token.

---

## Summary

| # | Route | Component | Auth | API / Data |
|---|-------|-----------|------|------------|
| 1 | `/` | Homepage | No | None |
| 2 | `/login` | Login | No | login, loginWithTwoFactor, register, setSession; demo: setDemoUser when no backend |
| 3 | `/connect` | Connection | No | getConnectionStatus, setConnectionStatus |
| 4 | `/onboarding` | Onboarding | No | saveUserSettings |
| 5 | `/dashboard` | Dashboard | No | getTemplateById, getUserSettings, generateCopy, getCopyHistoryStats, getCopyHistory, getTemplates, getConnectionStatus, getDashboardStats |
| 6 | `/generated` | GeneratedCopy | No | None (clipboard, navigate with state) |
| 7 | `/send` | SendToCrm | No | getContacts, getDeals, sendCopyToCrm |
| 8 | `/leads` | Leads | No | getLeads, searchLeads, getCompanies, createLead, updateLead, deleteLead |
| 9 | `/pipeline` | Pipeline | No | getDeals, updateDeal, createDeal, deleteDeal, getCompanies |
| 10 | `/tasks` | Tasks | No | getTasks (overdueOnly when filter=overdue), createTask, updateTask, getLeads, getDeals (for dialog) |
| 11 | `/activities` | Activities | No | getActivities, getActivitiesByContact, getActivitiesByDeal, createActivity, getContacts, getDeals |
| 12 | `/contacts` | Contacts | No | getContacts |
| 13 | `/companies` | Companies | No | getCompanies, createCompany, updateCompany |
| 14 | `/templates` | Templates | No | getTemplates |
| 15 | `/history` | History | No | getCopyHistory |
| 16 | `/settings` | Settings | No | getUserSettings, getConnectionStatus, saveUserSettings, twoFactorSetup, twoFactorEnable, twoFactorDisable |
| 17 | `/help` | Help | No | None |
| 18 | `/privacy` | Privacy | No | None |
| 19 | `/terms` | Terms | No | None |

---

## Per-Page Detail

### 1. Homepage (`/`)

- **File:** `pages/Homepage.tsx`
- **Purpose:** Public landing: hero, stats, How It Works, What You Can Do, Why Choose ACI, CTA, footer.
- **State:** None (stateless).
- **API:** None.
- **Layout:**
  - **Header:** Sticky, `bg-white/80 backdrop-blur-md`; ACI logo (Sparkles) + “Sign In” link → `/login`.
  - **Hero:** Trust badge (“AI copy for your CRM”); headline “Write sales copy in seconds, not hours”; subtext; CTAs “Get started” → `/login`, “See how it works” → `/help`. Animated blob backgrounds.
  - **Stats:** 3 columns — “5” Copy types, “Templates” Ready-to-use, “1-click” Send to CRM.
  - **How It Works:** 3 cards — Connect your CRM, Generate copy, Send to CRM (step number + title + description).
  - **What You Can Do:** 10 feature cards (Mail, MessageSquare, FileText, Briefcase, Workflow, LayoutTemplate, History, Send, Link2, Settings) with title + short description.
  - **Why Choose ACI:** 3 cards — Save time, Simple setup, Your brand voice (Clock, Check, Sparkles; stat badge + title + desc).
  - **CTA strip:** Orange gradient; “Start writing in minutes”; “Get Started Free” → `/login`; “No credit card required • Free forever plan”.
  - **Footer:** 4 columns — ACI + tagline; Product (How it works, Templates, Dashboard, History, Settings, Connect CRM); Legal (Privacy, Terms, Help); copyright + Twitter/LinkedIn/GitHub.
- **Accessibility:** `main id={MAIN_CONTENT_ID}`, `tabIndex={-1}`; section `aria-labelledby` for hero, how-it-works, features, why, cta.
- **README:** [pages/Homepage.README.md](../pages/Homepage.README.md)

---

### 2. Login (`/login`)

- **File:** `pages/Login.tsx`
- **Purpose:** Sign in or create account; optional 2FA step; when no backend, “Try demo (no backend)” → dashboard.
- **State:**
  - `mode`: `'login' | 'register'`.
  - `name`, `email`, `password` (name only when register).
  - `submitting`.
  - `requires2fa`, `twoFactorToken`, `code` (6-digit OTP).
  - `canSubmit`: derived (useMemo) — 2FA: code length 6 and token; else email+password; register requires name.
- **Handlers:**
  - `handleSubmit`: if 2FA → `loginWithTwoFactor` → setSession → navigate `/dashboard`; else login/register → if `requiresTwoFactor` set 2FA state and return; else setSession → navigate `/dashboard`. Demo button: `setDemoUser({ name: 'Demo User', email: 'demo@example.com' })` → navigate `/dashboard`.
- **API:** `login`, `loginWithTwoFactor`, `register` (api/auth); `setSession`, `setDemoUser` (lib/auth). Demo button only when `!getApiBaseUrl()`.
- **Layout:**
  - Header: “Back to home” → `/`.
  - Main: centered card; Sparkles icon; “Welcome back”; subtext (2FA or “Sign in to start…”).
  - When !requires2fa: Sign in / Create account tabs; register shows Name input; Email, Password; primary button (Create account / Sign in); when no backend, divider + “Try demo (no backend)” (Play icon).
  - When requires2fa: “Authentication code” label; InputOTP (6 slots); “Verify & sign in”; “Back” (clears 2FA state).
  - Footer in card: “Connect your CRM in one click…”; Privacy Policy, Terms of Service; below card: “Having trouble? See help” → `/help`.
- **Toasts:** success “Signed in” / “Account created”; message “Two-factor code required”; error on catch.
- **README:** [pages/Login.README.md](../pages/Login.README.md)

---

### 3. Connection (`/connect`)

- **File:** `pages/Connection.tsx`
- **Purpose:** Connect CRM; when connected, “Continue” → onboarding; else “Skip for now” → dashboard.
- **State:** `isConnected`, `loading` (true until first getConnectionStatus).
- **Effects:** On mount, `getConnectionStatus()` → set `isConnected`, set `loading` false.
- **Handlers:** `handleConnect`: `setConnectionStatus({ connected: true, accountEmail: user?.email ?? 'company@example.com' })` → set isConnected, toast success. `handleContinue`: navigate `/onboarding`.
- **API:** `getConnectionStatus`, `setConnectionStatus`; `getCurrentUser` (lib/auth) for display/email.
- **Layout:**
  - Loading: full-screen spinner (orange border).
  - Not connected: Sparkles icon; “Connect your CRM”; bullet list (Read contacts/deals, Create emails/notes, Update workflow messages); “Connect” button; “Skip for now” → `/dashboard`.
  - Connected: CheckCircle icon; “Successfully Connected!”; “Connected Account” + email + “Active Connection”; “Continue” → `/onboarding`.
- **Accessibility:** `main id={MAIN_CONTENT_ID}`, `tabIndex={-1}`.
- **README:** [pages/Connection.README.md](../pages/Connection.README.md)

---

### 4. Onboarding (`/onboarding`)

- **File:** `pages/Onboarding.tsx`
- **Purpose:** Set company name and brand tone; save → dashboard.
- **State:** `companyName`, `brandTone` ('professional' | 'friendly' | 'persuasive'), `saving`.
- **Handlers:** `handleSubmit`: `saveUserSettings({ companyName: companyName.trim() || 'My Company', brandTone })` → toast success → navigate `/dashboard`.
- **API:** `saveUserSettings`.
- **Layout:** Centered; Sparkles icon; “Set your brand tone”; form: Company Name (required), Brand Tone radio (Professional / Friendly / Persuasive with descriptions); “Save & Continue”; “You can change these settings anytime”.
- **README:** [pages/Onboarding.README.md](../pages/Onboarding.README.md)

---

### 5. Dashboard (`/dashboard`)

- **File:** `pages/Dashboard.tsx`
- **Purpose:** Main copy generation: type, goal, context, length → generate → /generated. Pre-fill from templateId or regenerateContext in location state.
- **Route state:** `location.state`: `{ templateId?: string; regenerateContext?: string }`.
- **State:**
  - `selectedType`: `CopyTypeId | ''`.
  - `goal`, `context`, `length` ('short' | 'medium' | 'long').
  - `isGenerating`, `stats` (sentThisWeek, totalSent, templateCount), `crmStats` (activeLeadsCount, activeDealsCount, pipelineValue, dealsWonCount, dealsLostCount), `connectionStatus`, `recentActivity` (CopyHistoryItem[]).
- **Effects:** On mount: getCopyHistoryStats, getTemplates, getConnectionStatus, getCopyHistory (slice 0–5 → recentActivity), getDashboardStats → crmStats. When templateId: getTemplateById → setSelectedType, setGoal. When regenerateContext: setContext.
- **Handlers:** `handleGenerate`: getUserSettings, then generateCopy with type, goal, context, length, companyName, brandTone → navigate `/generated` with `{ copy, copyTypeLabel }`.
- **API:** getTemplateById, getUserSettings, generateCopy, getCopyHistoryStats, getCopyHistory, getTemplates, getConnectionStatus, getDashboardStats.
- **Layout:**
  - AppHeader.
  - “Dashboard” label; “Welcome back, {displayName}”; short intro.
  - Connection badge: connected (green) or not (amber); account email; Link “Manage”/“Connect” → `/connect`.
  - Stats grid (2x2 lg:4): “Generated this week”, “Templates”, “Sent to CRM”, “~2 min” Avg. time saved.
  - “Create your content” divider.
  - Section “Create new copy”: Content type grid (5 types: Sales Email, Follow-up, CRM Note, Deal Message, Workflow); when type selected: Message goal dropdown (6 goals), Optional context textarea, Length (Short/Medium/Long), “Generate copy” button (loading spinner when generating). When no type: dashed empty state “Select a content type”.
  - “Recent activity”: last 5 copy history items (type → recipientName, date); “View full history” → `/history`.
  - “More options” divider; quick links: Templates, History, Settings, Connection.
- **Copy types (goals):** Schedule a meeting, Follow up after demo, Request feedback, Share resources, Check in on progress, Close the deal.
- **README:** [pages/Dashboard.README.md](../pages/Dashboard.README.md)

---

### 6. Generated Copy (`/generated`)

- **File:** `pages/GeneratedCopy.tsx`
- **Purpose:** View/edit generated copy, copy to clipboard, adjust tone (shorter/friendlier/persuasive), regenerate (→ dashboard with context), send to CRM.
- **Route state:** `location.state`: `{ copy?: string; copyTypeLabel?: string }`. Default copy: DEFAULT_COPY constant (placeholder text); default label `'Copy'`.
- **State:** `generatedText`, `copyTypeLabel`, `copied` (reset after 2s).
- **Effects:** Sync from state.copy and state.copyTypeLabel when present.
- **Handlers:** handleCopy (clipboard + toast); handleRegenerate → navigate `/dashboard` with `regenerateContext: generatedText.slice(0, 200)`; handleMakeShorter / handleMakeFriendlier / handleMakePersuasive (replace text with fixed snippets); handleSendToCrm → navigate `/send` with `{ copy: generatedText, copyTypeLabel }`.
- **API:** None.
- **Layout:** AppHeader; “Generated Copy”; two-column (md:3): left — “Your Copy” label + Copy button, editable textarea (16 rows, font-mono), “Send to CRM” button; right — “Adjust Copy” (Make shorter, Make friendlier, Make more persuasive, Regenerate); tip box (edit or use buttons).
- **README:** [pages/GeneratedCopy.README.md](../pages/GeneratedCopy.README.md)

---

### 7. Send to CRM (`/send`)

- **File:** `pages/SendToCrm.tsx`
- **Purpose:** Attach copy to a contact or deal (workflow/email show demo notice). Success → Create Another / View History.
- **Route state:** `location.state`: `{ copy?: string; copyTypeLabel?: string }`. Empty state when no copy and !isSent → EmptyState “Go to Dashboard”.
- **State:** objectType ('contact'|'deal'|'workflow'|'email'), contacts, deals, searchQuery, selectedRecord ({ id, name }), isSent, sending, loading.
- **Effects:** On mount, Promise.all(getContacts(), getDeals()) → set contacts, deals, loading false.
- **Derived:** filteredContacts, filteredDeals by searchQuery; records = contacts → { id, name, sub: email } or deals → { id, name, sub: value · stage }.
- **Handlers:** handleSend: sendCopyToCrm with objectType, recordId, recordName, copy, copyTypeLabel → toast success → setIsSent(true).
- **API:** getContacts, getDeals, sendCopyToCrm.
- **Layout:**
  - When !copy && !isSent: AppHeader; EmptyState (FileText icon, “No copy to send”, “Go to Dashboard” → `/dashboard`). Main has no MAIN_CONTENT_ID in this branch.
  - Else: AppHeader; “Send to CRM”; “Select object type” — 4 options (Contact, Deal, Workflow, Email draft). For contact/deal: “Select specific record” + search input + radio list (name + sub); LoadingSpinner while loading; “No records found” when empty. For workflow/email: amber notice “select Contact or Deal”. “Confirm & Send” (disabled when no selection or workflow/email). Success view: CheckCircle; “Successfully Sent!”; “Sent to” card (selectedRecord name + type); “Create Another” → `/dashboard`, “View History” → `/history`.
- **README:** [pages/SendToCrm.README.md](../pages/SendToCrm.README.md)

---

### 8. Templates (`/templates`)

- **File:** `pages/Templates.tsx`
- **Purpose:** Browse templates by category; “Use template” → dashboard with templateId in state.
- **State:** templates (Template[]), loading.
- **Effects:** On mount, getTemplates() → set templates, loading false.
- **Handlers:** handleUseTemplate(template) → navigate('/dashboard', { state: { templateId: template.id } }).
- **API:** getTemplates.
- **Layout:** AppHeader; “Templates”; grid of cards: category icon (Sales, Follow-up, Meetings, Re-engagement), category badge, title, description, “Used X times”, “Use template →”. “How Templates Work” section (sky-50); “Learn more about templates” → `/help`.
- **README:** [pages/Templates.README.md](../pages/Templates.README.md)

---

### 9. History (`/history`)

- **File:** `pages/History.tsx`
- **Purpose:** List copy history; search by copy/type/recipient; copy to clipboard; regenerate (→ dashboard with first 300 chars as context).
- **State:** searchQuery, copiedId (reset after 2s), items (CopyHistoryItem[]), loading.
- **Effects:** On mount, getCopyHistory() → set items, loading false.
- **Handlers:** handleCopy(id, text) — clipboard, setCopiedId, toast; handleRegenerate(item) → navigate('/dashboard', { state: { regenerateContext: item.copy.slice(0, 300) } }).
- **Derived:** filteredItems by search (copy, type, recipientName).
- **API:** getCopyHistory.
- **Layout:** AppHeader; “Copy History”; search input (placeholder “Search your copy history…”); loading spinner; list of articles: type icon, type + “To: {recipientName}”, relative date (formatDate), line-clamp-2 copy, Copy + Regenerate buttons; EmptyState when no items or no search results (“No copy history yet” / “No results found”), optional “Create your first copy” → `/dashboard`.
- **README:** [pages/History.README.md](../pages/History.README.md)

---

### 10. Leads (`/leads`)

- **File:** `pages/Leads.tsx`
- **Purpose:** List, search, create, and edit leads; company/source/status.
- **State:** leads (Lead[]), companies (Company[]), loading, searchQuery, dialogOpen, editingLead (Lead | null), form (name, email, phone, companyId, source, status), saving.
- **Effects:** On mount, getLeads() and getCompanies() → set leads, companies, loading false.
- **Handlers:** openCreate (reset form, set editingLead null, open dialog); openEdit(lead) (set form from lead, set editingLead, open dialog); handleSubmit (createLead or updateLead → update list, toast, close dialog).
- **Derived:** filteredLeads by searchQuery (name, email, phone).
- **API:** getLeads, searchLeads, getCompanies, createLead, updateLead.
- **Layout:** AppHeader; “Leads”; “Add lead” button; search input; list of leads (name, email, company badge, status, source, Edit button) or EmptyState; Dialog for create/edit (name*, email*, phone, company select, source select, status select).

---

### 11. Pipeline (`/pipeline`)

- **File:** `pages/Pipeline.tsx`
- **Purpose:** Deals by stage (Kanban); move deal stage; new deal.
- **State:** deals (Deal[]), loading, createOpen, createForm (name, value, stage, expectedCloseDate, companyId), companies (Company[]), saving.
- **Effects:** On mount, getDeals() → set deals. When createOpen, getCompanies() → set companies.
- **Handlers:** handleMoveStage(dealId, newStage) → updateDeal (stage + isWon when Closed Won/Lost) → refresh list, toast; handleCreate (createDeal → add to list, toast, close dialog); handleDeleteConfirm (deleteDeal → remove from list, toast). Delete button per deal opens confirm dialog.
- **API:** getDeals, updateDeal, createDeal, deleteDeal, getCompanies.
- **Layout:** AppHeader; “Pipeline”; “New deal” button; grid of stage columns (Qualification, Proposal, Negotiation, Closed Won, Closed Lost); each column shows deal cards (name, value, stage Select); Dialog for new deal (name*, value*, stage, optional expected close date, optional company).

---

### 12. Tasks (`/tasks`)

- **File:** `pages/Tasks.tsx`
- **Purpose:** List tasks; filter All/Pending/Overdue; create/edit task; toggle complete.
- **State:** tasks (TaskItem[]), loading, filter ('all' | 'pending' | 'overdue'), dialogOpen, editingTask (TaskItem | null), form (title, description, dueDate, leadId, dealId), leads (Lead[]), deals (Deal[]), saving.
- **Effects:** On mount and when filter changes, getTasks(filter === 'overdue') → set tasks. When dialogOpen, getLeads() and getDeals() → set leads, deals.
- **Handlers:** openCreate, openEdit(task), handleSubmit (createTask or updateTask with leadId, dealId), handleToggleComplete(task) → updateTask(completed: !task.completed).
- **Derived:** filteredTasks (when filter pending: !completed; when overdue: API returns overdue only).
- **API:** getTasks (overdueOnly when filter=overdue), createTask, updateTask, getLeads, getDeals (for create/edit dialog dropdowns).
- **Layout:** AppHeader; “Tasks”; “Add task” button; filter buttons (Pending, Overdue, All); list of tasks (checkbox complete, title, description, due date, overdue badge, Edit) or EmptyState; Dialog for create/edit (title*, description, due datetime, link to lead, link to deal).

---

### 13. Activities (`/activities`)

- **File:** `pages/Activities.tsx`
- **Purpose:** List activities; log activity (call/meeting/email/note) with optional contact/deal link.
- **State:** activities (Activity[]), contacts (Contact[]), deals (Deal[]), loading, filter ('all'|'contact'|'deal'), filterContactId, filterDealId, dialogOpen, form (type, subject, body, contactId, dealId), saving.
- **Effects:** On mount and when filter/filterContactId/filterDealId change: getActivities() or getActivitiesByContact(filterContactId) or getActivitiesByDeal(filterDealId) → set activities. When dialogOpen or filter !== 'all', getContacts() and getDeals() for dropdowns.
- **Handlers:** handleSubmit (createActivity → prepend to list, loadData, toast, close dialog).
- **API:** getActivities, getActivitiesByContact, getActivitiesByDeal, createActivity, getContacts, getDeals.
- **Layout:** AppHeader; “Activities”; “Log activity” button; filter All / By contact / By deal with contact or deal Select; list of activities (type icon, label, subject, body snippet, date) or EmptyState; Dialog for log activity (type select, subject, body, contact select, deal select).

---

### 14. Contacts (`/contacts`)

- **File:** `pages/Contacts.tsx`
- **Purpose:** List contacts (read-only with search). Used when sending copy to CRM and filtering activities.
- **State:** contacts (Contact[]), loading, searchQuery.
- **Effects:** On mount, getContacts() → set contacts, loading false.
- **Derived:** filteredContacts by searchQuery (name, email, phone).
- **API:** getContacts.
- **Layout:** AppHeader; "Contacts"; search input; list of contacts (name, email, phone) or EmptyState.

---

### 15. Companies (`/companies`)

- **File:** `pages/Companies.tsx`
- **Purpose:** List companies; add and edit companies. Used when creating leads and linking deals.
- **State:** companies (Company[]), loading, searchQuery, dialogOpen, editingCompany (Company | null), formName, saving.
- **Effects:** On mount, getCompanies() → set companies, loading false.
- **Handlers:** openCreate, openEdit(company), handleSubmit (createCompany or updateCompany → update list, toast, close dialog).
- **Derived:** filteredCompanies by searchQuery (name).
- **API:** getCompanies, createCompany, updateCompany.
- **Layout:** AppHeader; "Companies"; "Add company" button; search input; list of companies (icon, name, id, Edit button) or EmptyState with "Add company" action; Dialog for add/edit (name*).

---

### 16. Settings (`/settings`)

- **File:** `pages/Settings.tsx`
- **Purpose:** Brand settings, CRM connection status, Security (2FA enable/disable), Logout, Delete account (two-click confirm → navigate `/`).
- **State:** settings (UserSettings | null), showDeleteConfirm, saving, connected, twoFaEnabled, twoFaLoading, twoFaSecret, twoFaUri, twoFaCode, twoFaDisablePassword, twoFaDisableCode.
- **Effects:** On mount: getUserSettings → setSettings (fallback Acme Corporation + professional); getConnectionStatus → set connected; twoFactorSetup → set twoFaEnabled, twoFaSecret, twoFaUri.
- **Handlers:** handleSave (saveUserSettings); handleDeleteAccount: first click set showDeleteConfirm, second navigate `/`; handleStart2fa (twoFactorSetup, show secret/otpauth); handleEnable2fa (twoFactorEnable with code); handleDisable2fa (twoFactorDisable with password + code).
- **API:** getUserSettings, getConnectionStatus, saveUserSettings, twoFactorSetup, twoFactorEnable, twoFactorDisable.
- **Layout:** AppHeader; when settings === null → LoadingSpinner. Else: “Settings”; Brand Settings (company name input, Brand Tone radios, “Save Changes”); CRM Connection (Connected/Not connected, “Reconnect”/“Connect” → `/connect`); Security — 2FA (Enable / View setup); when !twoFaEnabled && twoFaSecret: secret + otpauth display, 6-digit InputOTP, “Confirm & enable 2FA”; when twoFaEnabled: “2FA is enabled”, password + 6-digit code inputs, “Disable 2FA”; Account Actions: Logout → `/login`, Delete Account (two-click: “Click again to confirm deletion” then navigate `/`).
- **README:** [pages/Settings.README.md](../pages/Settings.README.md)

---

### 17. Help (`/help`)

- **File:** `pages/Help.tsx`
- **Purpose:** What ACI does, How to Generate Copy (4 steps), How CRM Connection Works, Data & Privacy, Contact Support.
- **State:** None.
- **API:** None.
- **Layout:** AppHeader; “How It Works”; sections: What This Tool Does; How to Generate Copy (1–4: choose type, set goal, generate and refine, send to CRM); How CRM Connection Works (OAuth, permissions, disconnect); Data & Privacy (bullets + link to Privacy); Need More Help? (mailto support@example.com).
- **README:** [pages/Help.README.md](../pages/Help.README.md)

---

### 18. Privacy (`/privacy`)

- **File:** `pages/Privacy.tsx`
- **Purpose:** Privacy policy (intro, collection, use, security, sharing, rights, CRM, cookies, transfers, children, changes, contact).
- **Layout:** Custom header (Back to Home, ACI logo); main: prose sections (Introduction, Information We Collect, How We Use, etc.).
- **README:** [pages/Privacy.README.md](../pages/Privacy.README.md)

---

### 19. Terms (`/terms`)

- **File:** `pages/Terms.tsx`
- **Purpose:** Terms of service (agreement, license, description, acceptable use, ownership, CRM, payment, disclaimers, liability, indemnification, termination, changes, governing law, contact).
- **Layout:** Same header pattern as Privacy; main: prose sections.
- **README:** [pages/Terms.README.md](../pages/Terms.README.md)

---

## Shared Conventions

- **Skip link:** `SkipLink` exports `MAIN_CONTENT_ID = 'main-content'`. Pages set `<main id={MAIN_CONTENT_ID}>` except SendToCrm empty state (no main id there).
- **App header:** Used on Dashboard, GeneratedCopy, SendToCrm, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Settings, Help. Nav items: Dashboard, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Help. User dropdown: avatar (initials), name; Settings link; Sign out (clearSession + navigate /login). Mobile: Sheet with same nav + Settings + Sign out.
- **Toasts:** Sonner — `toast.success`, `toast.error`, `toast.message` for API and copy feedback.
- **Navigation:** React Router `Link` and `useNavigate`; state via `navigate(path, { state })`: copy, copyTypeLabel (GeneratedCopy ↔ SendToCrm); templateId (Templates → Dashboard); regenerateContext (History/GeneratedCopy → Dashboard).
- **Loading/empty:** LoadingSpinner (Connection, SendToCrm, Templates, History, Settings); EmptyState (SendToCrm no copy, History empty or no results).
- **CSS:** `px-[var(--page-padding)]` for horizontal padding; orange primary (orange-600); slate neutrals; rounded-2xl cards, focus-visible rings.

---

**Verification:** Frontend routes (`App.tsx`), API modules (`src/app/api/index.ts`), types (`types.ts`), and page API usage checked against backend controllers and endpoints. All aligned as of January 2026. For user flows from beginning through company setup and full CRM, see [USER_FLOWS_REPORT.md](USER_FLOWS_REPORT.md).

*Last updated: January 2026*
