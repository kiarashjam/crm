# CRM Sales Point of View – Checklist

This report checks that the ACI frontend has everything needed from a **CRM sales** perspective: contacts, deals, copy types, connection, send-to-CRM, history, and sales workflows.

---

## What’s Covered

### Core CRM entities

| Item | Status | Where |
|------|--------|--------|
| **Contacts** | Yes | `api/types` Contact (id, name, email, companyId); `getContacts()`, `searchContacts()`; Send to CRM → Contact |
| **Deals** | Yes | `api/types` Deal (id, name, value, stage, companyId); `getDeals()`, `searchDeals()`; Send to CRM → Deal |
| **Companies** | Data only | `api/types` Company; `mockCompanies` in mockData. Used as `companyId` on Contact/Deal. No company list UI or API. |

### Sales copy and goals

| Item | Status | Where |
|------|--------|--------|
| **Copy types** | Yes | Sales Email, Follow-up, CRM Note, Deal Message, Workflow Message (`api/types` CopyTypeId) |
| **Goals** | Yes | Schedule a meeting, Follow up after demo, Request feedback, Share resources, Check in on progress, Close the deal (Dashboard) |
| **Templates** | Yes | Sales, Follow-up, Meetings, Re-engagement; first contact, follow-up after meeting, demo reminder, closing deal, re-engagement (mockData + Templates page) |
| **Brand / company** | Yes | Company name and brand tone (Professional, Friendly, Persuasive) in Onboarding and Settings; used in `generateCopy` |

### CRM connection and sending

| Item | Status | Where |
|------|--------|--------|
| **Connect CRM** | Yes | Connection page: connect, status, “Continue” → onboarding, “Skip for now” → dashboard |
| **Send to Contact** | Yes | Send to CRM: object type Contact, search, pick contact, send copy |
| **Send to Deal** | Yes | Send to CRM: object type Deal, search, pick deal, send copy |
| **Workflow / Email draft** | Placeholder | Send to CRM: options shown; demo notice “select Contact or Deal”; ready for full CRM integration later |

### History and activity

| Item | Status | Where |
|------|--------|--------|
| **Copy history** | Yes | History page: list, search (copy/type/recipient), copy to clipboard, regenerate from context |
| **History stats** | Yes | Dashboard: “Generated this week”, “Sent to CRM” |
| **Recent activity** | Yes | Dashboard: last 5 items sent to CRM with type, recipient, date |

### Navigation and settings

| Item | Status | Where |
|------|--------|--------|
| **Dashboard** | Yes | Main hub: connection status, stats, copy types, goal, context, length, generate → Generated Copy |
| **Templates** | Yes | Templates page → “Use template” pre-fills Dashboard |
| **Settings** | Yes | Brand (company name, tone), CRM connection status, Security (2FA enable/disable), Logout, Delete account |
| **Help** | Yes | How it works, generate steps, CRM connection, data & privacy, support |

---

## Gaps and recommendations

### 1. Deal stage in “Send to CRM”

- **Data:** `Deal` has `stage` (e.g. Qualification, Proposal, Negotiation, Closed Won in mockData).
- **UI:** Send to CRM deal list shows deal name and **value · stage** (e.g. `$50,000 · Proposal`) so reps can pick the right opportunity.
- **Status:** Implemented.

### 2. Pipeline view

- **Data:** Deals have `stage`; no pipeline/Kanban view.
- **Recommendation:** For a full CRM sales experience later, consider a Pipeline page (deals by stage). Not required for the current “AI copy + send to CRM” focus.

### 3. Companies as first-class list

- **Data:** `Company` type and `companyId` on Contact/Deal exist; no companies API or list UI.
- **Recommendation:** Optional later: company list or company filter when choosing contact/deal. Current flow (pick contact/deal by name/search) is sufficient for copy generation and send.

### 4. “Connect CRM” in main nav

- **Current:** Connection is reached from Dashboard connection badge, Settings, or footer/home links.
- **Recommendation:** Optional: add “Connect” or “Connection” to AppHeader nav so reps see it next to Dashboard/Templates/History. Not blocking.

---

## Summary

From a **CRM sales point of view**, the app has:

- **Contacts and Deals** with search and send-to-CRM.
- **Five copy types** and six goals aligned with sales workflows.
- **Templates** for first contact, follow-up, demo, closing, re-engagement.
- **CRM connection** and **send to Contact/Deal** with history and recent activity.
- **Brand/company settings** used in AI copy.

**Current state:** Deal stage is shown in the Send to CRM deal list (value · stage). Settings includes 2FA (enable/disable). Login supports sign in, register, and 2FA code step; demo mode when no backend.

---

*Last updated: January 2026*
