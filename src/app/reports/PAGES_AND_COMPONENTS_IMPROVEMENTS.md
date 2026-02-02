# Frontend: Pages & Components — Meaning & Improvements

**Purpose:** Explains what each page and shared component does, and lists concrete improvements to make the app friendlier, clearer, and easier to use.

**Audience:** Developers and product owners implementing or prioritizing UI changes.

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [Component reference](#2-component-reference)
3. [Page reference & improvements](#3-page-reference--improvements)
4. [Cross-cutting improvements](#4-cross-cutting-improvements)
5. [Implementation checklist](#5-implementation-checklist)
6. [Implementation status](#6-implementation-status)

---

## 1. Executive summary

| Area | What it is | Top improvements |
|------|------------|------------------|
| **Components** | SkipLink, LoadingSpinner, EmptyState, AppHeader | Add loading labels; `aria-current="page"`; optional second CTA in empty states |
| **Public pages** | Homepage, Login, Connection, Onboarding | Forgot password link; loading message on Connection; tone examples on Onboarding |
| **Core flow** | Dashboard → Generated Copy → Send to CRM | Default goal; copy-type badge; main id on Send empty; success summary |
| **CRM pages** | Leads, Pipeline, Tasks, Activities, Contacts, Companies | Result counts; clickable rows; filter counts; de-emphasize raw IDs |
| **Support pages** | Templates, History, Settings, Help, Privacy, Terms | “Send again” in History; 2FA steps in Settings; table of contents in Help |

**Priority key:** **P0** = high impact, low effort · **P1** = high impact or unblocks users · **P2** = polish / optional

---

## 2. Component reference

### 2.1 SkipLink

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Lets keyboard and screen-reader users jump to main content without tabbing through the full header. Visible only on focus. |
| **Where used** | Global (once per app). |
| **Exports** | `MAIN_CONTENT_ID = 'main-content'` for pages to set on `<main>`. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| C1 | P2 | Add `transition-transform duration-150` when link appears on focus so it doesn’t feel abrupt. |
| C2 | P0 | Ensure the active nav link in AppHeader has `aria-current="page"` (for screen readers). |

---

### 2.2 LoadingSpinner

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Single, consistent loading indicator (orange accent). Used on list pages and during async actions. |
| **Where used** | Connection, SendToCrm, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Settings. |
| **Props** | `size?: 'sm' \| 'md' \| 'lg'`, `className?`. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| C3 | P1 | Add optional `label?: string` (e.g. "Loading leads…") and render in `<span className="sr-only">` or below spinner for context. |
| C4 | P1 | On full-page loads (Connection, Settings initial), wrap spinner in a centered container with short text: "Loading…" or "Checking connection…". |

---

### 2.3 EmptyState

| Attribute | Detail |
|-----------|--------|
| **Meaning** | "No data" or "No results" state: icon, title, optional description, optional primary CTA (link or button). |
| **Where used** | Leads, Pipeline, Tasks, Activities, Contacts, Companies, History, SendToCrm. |
| **Props** | `icon`, `title`, `description?`, `actionLabel?`, `actionHref?` or `onAction?`, `className?`. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| C5 | P2 | Add optional `secondaryActionLabel` and `secondaryActionHref` / `onSecondaryAction` (e.g. "Add lead" + "Import from CRM"). |
| C6 | P1 | When search returns no results, show a one-line hint under the search box: "No results for ‘X’. Try a different search or add one." |
| C7 | P2 | Optional `variant?: 'default' \| 'compact'` for inline or narrow layouts. |

---

### 2.4 AppHeader

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Main app chrome: logo → nav (Dashboard, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Help) → user dropdown (Settings, Sign out). Sticky; on mobile, nav in Sheet. |
| **Where used** | All app pages except Homepage, Login, Connection, Onboarding, Privacy, Terms. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| C8 | P0 | Set `aria-current="page"` on the active nav `<Link>` when `isActive` is true. |
| C9 | P1 | In user dropdown, add a line: "CRM: Connected" or "CRM: Not connected" with link to `/connect`. |
| C10 | P2 | Optionally group nav with labels or separators: e.g. "CRM" (Leads, Pipeline, Tasks, Activities, Contacts, Companies) and "Copy" (Templates, History). |

---

## 3. Page reference & improvements

### 3.1 Homepage (`/`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Public landing: hero, value props, how it works, CTAs. Converts visitors to sign-in. |
| **API** | None. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P1 | P2 | Add a one-line testimonial or trust line near CTA (e.g. "Join teams using ACI"). |
| P2 | P1 | Add a short FAQ (e.g. "Do I need a CRM?" "Is my data secure?") to reduce sign-up anxiety. |

---

### 3.2 Login (`/login`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Sign in / register; optional 2FA step; demo mode when no backend. |
| **API** | login, loginWithTwoFactor, register; setSession / setDemoUser. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P3 | P1 | Add "Forgot password?" link (to `/help` or "Contact support" until feature exists). |
| P4 | P1 | On register, show password requirements (e.g. "At least 8 characters"). |
| P5 | P1 | After failed login, show friendly line: "Check your email and password, or reset your password." |
| P6 | P0 | Ensure email has `autoComplete="username"` and password has `current-password` / `new-password`. |

---

### 3.3 Connection (`/connect`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | One-step "Connect your CRM"; after connect, "Continue" → onboarding; else "Skip for now" → dashboard. |
| **API** | getConnectionStatus, setConnectionStatus. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P7 | P1 | During loading, show text "Checking connection…" (not only spinner). |
| P8 | P2 | If user came from Settings (e.g. via state), after connect offer "Back to Settings" in addition to "Continue". |

---

### 3.4 Onboarding (`/onboarding`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Set company name and brand tone once; save → dashboard. |
| **API** | saveUserSettings. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P9 | P1 | Add one-line example under each tone (e.g. Professional: "We are pleased to inform you…"). |
| P10 | P2 | Optional "Skip for now" that saves defaults and goes to dashboard. |

---

### 3.5 Dashboard (`/dashboard`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Central hub: CRM status, stats, "Create new copy" (type → goal → context → length → generate), recent activity, quick links. |
| **API** | getTemplateById, getUserSettings, generateCopy, getCopyHistoryStats, getCopyHistory, getTemplates, getConnectionStatus, getDashboardStats. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P11 | P1 | Default "Message goal" to first option (e.g. "Schedule a meeting") so one click after picking type is enough. |
| P12 | P1 | When no type selected, add prominent suggestion: "Start with Sales Email" or link to "Try a template" → `/templates`. |
| P13 | P1 | Add tooltip or hint next to "Optional context": "More context improves the AI output." |
| P14 | P2 | In recent activity, make each row clickable (e.g. to History) and add "Copy" button per row. |
| P15 | P1 | If CRM not connected, add inline CTA in create section: "Connect CRM to send copy to contacts and deals." |

---

### 3.6 Generated Copy (`/generated`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | View/edit generated text; copy to clipboard; adjust tone (shorter / friendlier / persuasive); regenerate; send to CRM. |
| **API** | None (clipboard; navigate with state). |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P16 | P1 | Show copy-type badge at top (e.g. "Sales Email") so context is clear when returning from Send to CRM. |
| P17 | P1 | Add character or word count under the textarea. |
| P18 | P2 | Keep "Copied!" visible ~3 s or show checkmark on button. |
| P19 | P2 | Add note near "Adjust Copy": "Sample adjustments; edit the text to refine" (or wire buttons to API if available). |

---

### 3.7 Send to CRM (`/send`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Attach generated copy to a contact or deal; workflow/email show demo notice. Success → "Create Another" / "View History". |
| **API** | getContacts, getDeals, sendCopyToCrm. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P20 | P0 | When no copy (empty state), add `<main id={MAIN_CONTENT_ID}>` and a heading so skip-link and structure are correct. |
| P21 | P1 | On success, show one-line summary: "Copy added to [Contact/Deal name]." |
| P22 | P2 | When list is long, show "Showing X of Y" or record count. |

---

### 3.8 Leads (`/leads`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | List, search, create, edit, delete leads; link to company; set source and status. |
| **API** | getLeads, getCompanies, createLead, updateLead, deleteLead. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P23 | P1 | Add result count: "12 leads" or "Showing 12 leads". |
| P24 | P1 | Make list rows clickable to open edit (not only Edit button). |
| P25 | P2 | In create/edit, add short hints: "Source: where you found this lead"; "Status: where they are in your process." |
| P26 | P2 | Humanize source in UI (e.g. "Website", "Referral") if API stores lowercase. |

---

### 3.9 Pipeline (`/pipeline`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Kanban of deals by stage; move stage via dropdown; create deal; delete deal. |
| **API** | getDeals, updateDeal, createDeal, deleteDeal, getCompanies. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P27 | P1 | Add "Edit deal" (dialog or panel: name, value, close date, company) so users can change details without only moving stage. |
| P28 | P2 | Show total value per column (e.g. "Qualification · $120k"). |
| P29 | P2 | When creating deal, suggest "Expected close date" preset (e.g. "In 30 days"). |

---

### 3.10 Tasks (`/tasks`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | List tasks; filter Pending / Overdue / All; create/edit; link to lead or deal; toggle complete. |
| **API** | getTasks, createTask, updateTask, getLeads, getDeals (for dialog). |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P30 | P1 | Show counts on filter tabs: "Pending (5)", "Overdue (2)", "All (7)". |
| P31 | P1 | In list, show linked lead or deal name when present (e.g. "Acme Corp"). |
| P32 | P2 | Optional "Due today" or "Next 7 days" filter. |

---

### 3.11 Activities (`/activities`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Timeline of calls, meetings, emails, notes; filter All / By contact / By deal; log activity with optional contact/deal link. |
| **API** | getActivities, getActivitiesByContact, getActivitiesByDeal, createActivity, getContacts, getDeals. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P33 | P1 | When filtering by contact or deal, show selected entity name in title or chip: "Activities for Jane Doe." |
| P34 | P1 | In list, show contact/deal name when present. |
| P35 | P2 | Add placeholder examples in "Log activity": "e.g. Discovery call – discussed pricing." |

---

### 3.12 Contacts (`/contacts`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Read-only list of contacts; search; used when sending copy and filtering activities. |
| **API** | getContacts. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P36 | P1 | Add count near title: "X contacts". |
| P37 | P2 | Add note when empty: "Contacts sync from your CRM. Connect to sync." |
| P38 | P2 | Make rows clickable (e.g. "Send copy to this contact" or detail view if added later). |

---

### 3.13 Companies (`/companies`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | List companies; create/edit; used when creating leads and linking deals. |
| **API** | getCompanies, createCompany, updateCompany. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P39 | P1 | Add count: "X companies". |
| P40 | P1 | De-emphasize raw "ID" in list (show in tooltip or only in edit). |

---

### 3.14 Templates (`/templates`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Browse templates by category; "Use template" → dashboard with template pre-filled. |
| **API** | getTemplates. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P41 | P2 | If many templates, add category filter or search. |
| P42 | P2 | Empty state when no templates: "No templates yet. Use the dashboard to create copy from scratch." |

---

### 3.15 History (`/history`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | List of sent copy; search; copy to clipboard; regenerate (→ dashboard with context). |
| **API** | getCopyHistory. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P43 | P1 | Add "X items" or "X results" near search. |
| P44 | P1 | Add "Send again" per item → navigate to Send to CRM with copy pre-filled. |
| P45 | P2 | Optional filter by type or date range; exact date on hover. |

---

### 3.16 Settings (`/settings`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Brand (company name, tone); CRM connection; Security (2FA); Logout; Delete account (two-click confirm). |
| **API** | getUserSettings, saveUserSettings, getConnectionStatus, twoFactorSetup, twoFactorEnable, twoFactorDisable. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P46 | P1 | Group sections with clear headings/icons: "Brand", "CRM connection", "Security", "Account". |
| P47 | P1 | In 2FA setup, add step list: "1. Scan QR code. 2. Enter 6-digit code. 3. Click Enable." |
| P48 | P1 | For delete account, add stronger confirm (e.g. type "DELETE" or checkbox "I understand my data will be removed"). |
| P49 | P2 | Add "Need help?" link to `/help` at bottom. |

---

### 3.17 Help (`/help`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | What ACI does; how to generate copy; how CRM connection works; data & privacy; contact support. |
| **API** | None. |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P50 | P1 | Add table of contents at top (anchor links to each section). |
| P51 | P2 | Add "Common questions" with anchors (e.g. "Why didn’t my copy send?"). |

---

### 3.18 Privacy (`/privacy`) & 3.19 Terms (`/terms`)

| Attribute | Detail |
|-----------|--------|
| **Meaning** | Legal pages; minimal header (Back to Home, logo). |

**Improvements**

| # | Priority | Action |
|---|----------|--------|
| P52 | P2 | Sticky "Back to home" on scroll so users don’t have to scroll up to leave. |
| P53 | P0 | Ensure heading order is logical (single h1, then h2/h3) for accessibility. |

---

## 4. Cross-cutting improvements

### 4.1 Copy and errors

| # | Priority | Action |
|---|----------|--------|
| X1 | P1 | Use consistent error message: "Something went wrong. Please try again." + optional "Need help?" link. |
| X2 | P2 | Use consistent success messages: "Lead created", "Deal moved", "Copy sent to CRM." |
| X3 | P2 | Add short hints on key actions (e.g. "Generate copy" → "We’ll create a first draft in a few seconds."). |

### 4.2 Accessibility

| # | Priority | Action |
|---|----------|--------|
| X4 | P0 | Every page: one `<h1>`, logical heading order. |
| X5 | P0 | Form errors announced (e.g. `aria-describedby` or live region; toasts already help). |
| X6 | P0 | Add `aria-label` on icon-only buttons (Copy, Edit, Delete, etc.). |
| X7 | P0 | Ensure skip-link works on all pages (including SendToCrm empty state via P20). |

### 4.3 Loading and errors

| # | Priority | Action |
|---|----------|--------|
| X8 | P1 | Use LoadingSpinner with contextual message where helpful (C3, C4). |
| X9 | P1 | On API error, show retry or "Try again" in empty state where appropriate. |
| X10 | P2 | Avoid full-page spinner when only a section loads (e.g. dashboard stats); use inline or skeleton. |

### 4.4 Mobile

| # | Priority | Action |
|---|----------|--------|
| X11 | P0 | Tap targets at least 44px (Add lead, New deal, etc.). |
| X12 | P2 | Pipeline: test horizontal scroll and column width on small screens. |
| X13 | P2 | Optional bottom nav for Dashboard, Leads, Pipeline, Tasks on mobile. |

---

## 5. Implementation checklist

Use this to prioritize work. Items are ordered by priority (P0 first), then by area.

### P0 — Fix first (accessibility & structure)

| ID | Item |
|----|------|
| C2 | AppHeader: `aria-current="page"` on active nav link |
| P6 | Login: correct autocomplete on email/password |
| P20 | SendToCrm: add main id + heading when empty |
| P53 | Privacy/Terms: logical heading order (h1 → h2 → h3) |
| X4 | Every page: one h1, logical headings |
| X5 | Form errors announced |
| X6 | aria-label on icon-only buttons |
| X7 | Skip-link works everywhere (P20 covers SendToCrm) |
| X11 | Tap targets ≥ 44px on mobile |

### P1 — High value (clarity & UX)

| ID | Item |
|----|------|
| C3, C4 | LoadingSpinner: optional label; loading message on full-page |
| C6 | EmptyState: hint under search when no results |
| C9 | AppHeader: CRM status in user dropdown |
| P3–P5 | Login: Forgot password; password requirements; friendly error |
| P7 | Connection: "Checking connection…" during load |
| P9 | Onboarding: tone examples |
| P11–P13, P15 | Dashboard: default goal; suggestion when no type; context tooltip; inline Connect CTA |
| P16, P17 | Generated Copy: copy-type badge; character/word count |
| P21 | SendToCrm: success summary line |
| P23, P24 | Leads: result count; clickable rows |
| P27 | Pipeline: Edit deal |
| P30, P31 | Tasks: filter counts; show linked lead/deal |
| P33, P34 | Activities: show filter entity name; show contact/deal in list |
| P36 | Contacts: count |
| P39, P40 | Companies: count; de-emphasize ID |
| P43, P44 | History: item count; "Send again" |
| P46–P48 | Settings: section grouping; 2FA steps; safer delete confirm |
| P50 | Help: table of contents |
| X1, X8, X9 | Consistent errors; contextual loading; retry where useful |

### P2 — Polish (optional)

| ID | Item |
|----|------|
| C1, C5, C7 | SkipLink transition; EmptyState second CTA; variant |
| C10 | AppHeader: nav grouping |
| P1, P2 | Homepage: testimonial; FAQ |
| P8, P10 | Connection/Onboarding: back to Settings; Skip |
| P14, P18, P19 | Dashboard/Generated: recent activity copy; Copied duration; Adjust note |
| P22 | SendToCrm: "Showing X of Y" |
| P25, P26 | Leads: hints; humanize source |
| P28, P29 | Pipeline: column value total; close date preset |
| P32 | Tasks: Due today / Next 7 days |
| P35, P37, P38 | Activities/Contacts: placeholders; sync note; clickable rows |
| P41, P42 | Templates: filter; empty state |
| P45 | History: type/date filter; exact date on hover |
| P49, P51 | Settings/Help: Need help link; Common questions |
| P52 | Privacy/Terms: sticky Back |
| X2, X3, X10, X12, X13 | Success copy; hints; inline loading; Pipeline mobile; bottom nav |

---

## 6. Implementation status

*Last implementation pass: February 2026*

### Components — Done

| ID | Status |
|----|--------|
| C1 | Done — SkipLink: `transition-transform duration-150` |
| C2 | Done — AppHeader: `aria-current="page"` on active nav link |
| C3 | Done — LoadingSpinner: optional `label` prop |
| C4 | Done — Connection/Settings: loading message with label |
| C8 | Done — AppHeader: `aria-current="page"` |
| C9 | Done — AppHeader: CRM status + link to `/connect` in user dropdown |

### Pages — Done

| ID | Page | Status |
|----|------|--------|
| P3–P6 | Login | Forgot password link; password requirements; error message; autocomplete |
| P7 | Connection | "Checking connection…" with LoadingSpinner label |
| P9 | Onboarding | One-line tone examples |
| P11, P12, P15 | Dashboard | Default goal; "Try a template" in empty state; inline Connect CTA |
| P16–P19 | Generated Copy | Copy-type badge; character/word count; Copied 3s; Adjust note |
| P20, P21 | Send to CRM | main id + heading when empty; success summary line |
| P23, P24, X6 | Leads | Result count; clickable rows; aria-label on Edit/Delete |
| P27, P28, P29 | Pipeline | Edit deal dialog; column value total; "In 30 days" / "In 90 days" preset |
| P30, P31 | Tasks | Filter counts; linked lead/deal names |
| P33–P35 | Activities | Filter entity in title; contact/deal in list; placeholder in Log activity |
| P36–P38 | Contacts | Count; empty note "Contacts sync from your CRM…"; clickable rows → /send |
| P39, P40 | Companies | Count; ID only in tooltip (de-emphasized) |
| P43, P44 | History | "X items" near search; "Send again" per item; aria-labels on buttons |
| P46–P49 | Settings | Sections: Brand, CRM connection, Security, Account; 2FA step list; delete confirm (type DELETE + checkbox); "Need help?" link |
| P50 | Help | Table of contents at top with anchor links |
| P52, P53 | Privacy/Terms | Sticky header with Back; heading order (h1 → h2) |

### Cross-cutting — Done

| ID | Status |
|----|--------|
| X6 | aria-label on icon-only buttons (Leads, Pipeline DealCard, History) |
| X7 | Skip-link: main id on SendToCrm empty state (P20) |
| X11 | Privacy/Terms: Back link min tap target 44px |

### Not implemented (optional / P2)

- C5, C6, C7 (EmptyState: second CTA; no-results hint; variant)
- C10 (AppHeader nav grouping)
- P1, P2 (Homepage testimonial; FAQ)
- P8, P10 (Connection/Onboarding: Back to Settings; Skip)
- P14, P22, P25, P26, P32, P35, P37, P38 (various P2 page polish)
- P41, P42, P45 (Templates/History P2)
- X2, X3, X10, X12, X13 (success copy; hints; inline loading; mobile)

---

**Related reports**

- [FRONTEND_PAGES_REPORT.md](./FRONTEND_PAGES_REPORT.md) — Routes, state, API, layout per page  
- [UI_DESIGN_RATIONALE.md](./UI_DESIGN_RATIONALE.md) — Why the UI looks the way it does  
- [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md) — Every aspect of the project in one place  

*Last updated: February 2026*
