# Sales CRM — Core Features Gap Report

**Scope:** True core only. No nice-to-haves. No extras. Just what a **real Sales CRM must do**.

A Sales CRM exists for one reason: **to control the sales process from lead → deal → close, with full visibility and accountability.** Anything outside the list below is optional.

This report maps each core must-have to **what the system has** vs **what it does not have**.

---

## Implementation status (latest pass)

**Backend (implemented):**

- **Lead:** Entity (name, email, phone, companyId, LeadSourceId, LeadStatusId, IsConverted, ConvertedAtUtc); CRUD API; **POST /api/leads/{id}/convert** (create/attach company, contact, deal; lead set to Converted).
- **Company:** Companies API (GET list, GET by id, POST create, PUT update, DELETE). Domain, Industry, Size. CompanyRepository + CompanyService + CompaniesController.
- **Deal:** Added `ExpectedCloseDateUtc`, `IsWon`; CreateAsync, UpdateAsync; DealsController POST create, PUT update.
- **Contact:** Added `Phone` (optional) in entity and DTOs.
- **Task (TaskItem):** Entity (title, description, dueDateUtc, completed, leadId, dealId); CRUD API (`/api/tasks` — GET list with optional overdueOnly, GET by id, POST create, PUT update).
- **Activity:** Entity (type, subject, body, contactId, dealId); Create + get by user/contact/deal (`/api/activities` — GET list, GET contact/{id}, GET deal/{id}, POST create).
- **Reporting:** Dashboard stats (`/api/reporting/dashboard` — active leads count, active deals count, pipeline value, deals won/lost counts). IReportingService + ReportingService + ReportingController.

**Frontend (implemented):**

- **Types:** Lead, TaskItem, Activity, DashboardStats; Contact.phone, Deal.expectedCloseDateUtc, Deal.isWon.
- **API modules:** leads, companies (getCompanies, createCompany, updateCompany), tasks, activities, reporting, deals, contacts (phone in map), **organizations** (list, create, get; invites and join requests).
- **Pages:** Leads (`/leads` — list, search, create/edit/delete, company/source/status, **convert** to contact/deal); Pipeline (`/deals` — Kanban by stage from API, pipeline/stage/currency, new deal, edit, delete); Tasks (`/tasks` — list, All/Pending/Overdue, create/edit with link to lead/deal, complete toggle); Activities (`/activities` — list, log activity, filter by contact/deal for timeline); **Contacts** (`/contacts` — list and search); **Companies** (`/companies` — list, search, **create, edit**). Dashboard shows getDashboardStats. **Organizations** (`/organizations` — create org, accept invite, open org); Settings has org switcher and (for owner) invite by email, accept/reject join requests.

**Remaining:**

- **User roles:** Owner/Member/Manager exist; pipeline/stage/lead-source/lead-status config restricted to Owner/Manager. No "edit only my records" or manager dashboard yet (see [05-BLUEPRINT-GAP-REPORT.md](../../presentation/05-BLUEPRINT-GAP-REPORT.md)).

---

## Final Minimal Definition

> **A Sales CRM must:**  
> Capture leads, store customer data, track deals through stages, record interactions, manage follow-ups, and provide sales visibility — **with clear ownership and control**.

---

## 1. Lead Management

**Purpose:** Capture and control incoming potential customers.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Create and store leads (name, email, phone, company) | ✅ Backend + API | Lead entity and CRUD API; frontend types and getLeads/createLead/updateLead. |
| Track lead source (website, referral, ads, events, manual) | ✅ Backend + API | Lead.Source; frontend Lead.source. |
| Assign each lead to a salesperson | ⚠️ Backend only | Lead.UserId (owner); API filters by current user. No “assign to” UI. |
| Maintain lead status (New, Contacted, Qualified, Lost) | ✅ Backend + API | Lead.Status; frontend Lead.status. |

**Outcome needed:** Every lead captured, owned, and tracked.  
**Current:** Lead entity, API, and Leads page (`/leads`) with list, search, create/edit dialog, company/source/status.

---

## 2. Contact & Company Management

**Purpose:** Maintain structured customer data.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Centralized database of contacts and companies | Contacts: API (list, search), Send to CRM; delete. **Companies API** (list, get, create, update, delete); Domain, Industry, Size. **Companies page** (`/companies`) — list, search, Add, Edit, Delete. | — |
| Link contacts to companies | ✅ `Contact.companyId` in backend and frontend. | — |
| Link contacts and companies to leads and deals | Leads and deals have `companyId`; contacts have `companyId`. | No **contact–deal** relation (contacts not linked to deals in data model). |
| Access full interaction history per contact/company | Copy history: “sent copy” to contact/deal. **Activity** entity + API + **Activities page** (list, log call/meeting/email/note, filter by contact/deal for timeline). | Last-activity field on contact/deal (deferred). |

**Outcome needed:** One reliable source of customer truth.  
**Current:** Contacts and company link in data; Companies API (list, get, create, update); Companies page (`/companies`) list, search, create, edit. Activity entity + API; Activities page (list, log activity, filter by contact/deal for timeline). **Note:** Backend also has organization layer (Organizations, Invites, JoinRequests); data can be scoped by organization via `X-Organization-Id`.

---

## 3. Deal (Opportunity) Management

**Purpose:** Track revenue-generating opportunities.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Create deals linked to contacts/companies | **Pipeline** new deal UI (name, value, stage, pipeline, expected close date, **company**, contact, currency); createDeal API; Deal has CompanyId, ContactId, PipelineId, DealStageId, Currency, AssigneeId. | — |
| Define deal value and expected close date | ✅ Deal **value** + **ExpectedCloseDateUtc** (backend + frontend). |
| Assign deal ownership | Backend: `Deal.UserId`; API filters by current user. | Frontend does not show owner; no “assign to” flow. |
| Mark deals as Won or Lost | ✅ **Deal.IsWon** (backend + API + frontend); reporting has dealsWonCount/dealsLostCount. |

**Outcome needed:** All potential revenue visible and controlled.  
**Current:** Deals with value, stage, expected close date, isWon; createDeal/updateDeal API; Pipeline page (`/deals`) — Kanban by stage, move stage (sets isWon when Closed Won/Lost), new deal (name, value, stage, expected close date, company).

---

## 4. Sales Pipeline (Stage Tracking)

**Purpose:** Structure the sales process.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Stage-based pipeline (e.g. Prospect → Qualified → Negotiation → Closed) | **Pipeline** and **DealStage** entities (org-level); Deal has PipelineId, DealStageId; APIs for pipelines and deal stages (Owner/Manager only). Frontend loads stages from API; Kanban by stage. | — |
| One current stage per deal | ✅ One `stage` per deal. | — |
| Ability to move deals between stages | ✅ Backend + API + UI | updateDeal; Pipeline page stage dropdown per deal. |
| Visual overview of all deals by stage | ✅ Backend + API + UI | — (Pipeline page: Kanban by stage.) |

**Outcome needed:** Clear understanding of where every deal stands.  
**Current:** Pipeline page with deals by stage (Qualification, Proposal, Negotiation, Closed Won, Closed Lost); move stage via dropdown; new deal dialog.

---

## 5. Activity Tracking

**Purpose:** Maintain interaction history.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Log calls, meetings, emails, and notes | ✅ Backend + API | **Activity** entity (type: call/meeting/email/note) + createActivity; get by contact/deal. |
| Timestamped activity timeline per lead/deal | ✅ Backend + API + UI | getActivitiesByContact, getActivitiesByDeal. Activities page filter "By contact" / "By deal" for timeline view. |
| Visibility of last interaction | — | No “last activity” field or UI on contact/deal. |

**Outcome needed:** Full visibility of customer communication.  
**Current:** Only “copy sent” is recorded; Activity entity + API; Activities page (list, log, filter by contact/deal for timeline). Last-activity field on contact/deal (deferred).

---

## 6. Task & Follow-Up Management

**Purpose:** Ensure consistent sales execution.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Create tasks linked to leads or deals | ✅ Backend + API + UI | TaskItem + CRUD API; **Tasks page** create/edit dialog with optional link to lead and link to deal. |
| Due dates and ownership | ✅ Backend + API | TaskItem.DueDateUtc, UserId (owner). |
| View pending and overdue tasks | ✅ Backend + API + UI | GET /api/tasks?overdueOnly=true; Tasks page (`/tasks`) with All/Pending/Overdue filter, complete toggle. |

**Outcome needed:** Nothing falls through the cracks.  
**Current:** Tasks page with list, create/edit dialog, complete toggle, due date and overdue display.

---

## 7. Basic Reporting & Visibility

**Purpose:** Enable management oversight.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Number of active leads and deals | ✅ Backend + API + UI | getDashboardStats; Dashboard shows active leads, active deals. |
| Deals won vs lost | ✅ Backend + API + UI | getDashboardStats; Dashboard shows won/lost counts. |
| Total pipeline value | ✅ Backend + API + UI | getDashboardStats; Dashboard shows pipeline value. |
| Salesperson performance overview | Backend filters by user (each user sees own data). | No manager view of team; no performance metrics by rep (roles deferred). |

**Outcome needed:** Management always knows the current sales situation.  
**Current:** Dashboard displays getDashboardStats row: active leads, active deals, pipeline value, won/lost (in addition to copy stats).

---

## 8. User Roles & Access Control

**Purpose:** Protect data and clarify responsibility.

**Includes:**

| Requirement | Have | Don't Have |
|-------------|------|------------|
| Sales reps access their own data | Backend: contacts, deals, copy history filtered by `UserId`. Authenticated API. | Frontend does not show owner; demo/mock can bypass real user. |
| Managers access team data | — | No manager role; no team hierarchy; no “see team’s contacts/deals”. |
| Admin configuration rights | — | No admin role; no config/permission separation. |

**Outcome needed:** Secure and controlled system usage.  
**Current:** Per-user data isolation in backend; no roles (rep/manager/admin) or team/admin access.

---

## Summary

### What the system has

| Area | What we have |
|------|----------------|
| **Contacts** | API (list, search); Send to CRM; **Contacts page** (`/contacts`) list and search; `companyId`, phone; backend ownership. |
| **Companies** | Companies API (list, by id, **create, update**); **Companies page** (`/companies`) list, search, **Add company, Edit**; used in Leads, Pipeline. |
| **Deals** | API (list, search, create, update); value, stage, expectedCloseDateUtc, isWon; Pipeline page (Kanban, move stage, new deal). |
| **Copy history** | “Sent copy” to contact/deal (type, recipient, timestamp); History page; dashboard recent + “Sent to CRM” stats. |
| **Auth & isolation** | Auth (JWT/demo); login, register, 2FA; backend restricts data to current user. |
| **Leads** | Entity + CRUD API; Leads page (list, search, create/edit/**delete**, company/source/status). |
| **Tasks** | TaskItem entity + CRUD API; Tasks page (list, All/Pending/Overdue, create/edit, complete). |
| **Activity** | Entity + API (list, by contact/deal, create); Activities page (list, log activity with type and optional contact/deal). |
| **Reporting** | getDashboardStats; Dashboard row: active leads, active deals, pipeline value, won/lost. |

### What the system does not have

| Area | What we don't have |
|------|--------------------|
| **Roles** | No rep/manager/admin; no team or admin access (deferred). |
| **Last interaction** | No "last activity" field or UI on contact/deal. |
| **Contact–deal link** | Contacts not linked to deals in data model (no Deal.contactId). |
| **Contacts list page** | ✅ Implemented | `/contacts` page (list, search); nav link. |
| **Convert lead** | No "convert lead to contact/deal" flow. |
| **Lead/deal delete** | ✅ Implemented | DELETE API and UI for leads and deals (confirm dialog). |
| **Company CRUD** | ✅ Implemented | Create/update API and Companies page (`/companies` — Add company, Edit). |

---

## Core Sales CRM Checklist (at a glance)

| Core area | Implemented | Notes |
|-----------|-------------|--------|
| **1. Lead management** | Yes | Leads page: list, search, create/edit, company, source, status. |
| **2. Contact & company** | Yes | Contacts: API + Send to CRM + Contacts page (`/contacts` list, search). Companies: API + Companies page (list, create, edit). |
| **3. Deal management** | Yes | Pipeline: create deal (name, value, stage, expected close date, company); move stage; isWon on Closed Won/Lost. |
| **4. Sales pipeline** | Yes | Pipeline page: Kanban by stage (Qualification → Closed Won/Lost); move deals between stages. |
| **5. Activity tracking** | Yes | Activities page: list, log call/meeting/email/note; filter by contact/deal for timeline. |
| **6. Task & follow-up** | Yes | Tasks page: list, All/Pending/Overdue; create/edit with link to lead/deal; complete toggle. |
| **7. Reporting & visibility** | Yes | Dashboard: active leads, active deals, pipeline value, won/lost (getDashboardStats). |
| **8. User roles & access** | Deferred | Backend per-user isolation; no rep/manager/admin roles or team view. |

---

**Current system:** Implements core Sales CRM: **Leads** (list/create/edit/**delete**), **Pipeline** (deals by stage, move stage, new deal, **delete**), **Tasks** (list/create/edit, pending/overdue), **Activities** (list, log call/meeting/email/note with optional contact/deal), **Contacts** (list, search at `/contacts`), **Companies** (list, search, **create, edit**), **Reporting** (dashboard stats). Plus Intelligent Sales Writer, Send to CRM, copy history, auth (including 2FA). Remaining gaps: **User roles** (deferred), last-interaction visibility, contact–deal link, convert lead.

---

**Verified against:** `App.tsx` routes (including /leads, /deals, /tasks, /activities, /companies); `src/app/api/*` (leads including searchLeads, companies, tasks, activities, reporting, deals create/update, contacts with phone); `src/app/pages/*` (Leads, Pipeline, Tasks, Activities, Companies, Dashboard with getDashboardStats); backend controllers (Leads, Deals, Tasks, Activities, Companies, Reporting, Contacts) and DTOs/entities as listed. Frontend–backend alignment checked: endpoints and payloads match.

**Re-verification:** Frontend and backend re-checked; all flows have matching API modules, types, and controller endpoints. See [FLOWS_BACKEND_DATABASE_VERIFICATION.md](FLOWS_BACKEND_DATABASE_VERIFICATION.md), [FRONTEND_PAGES_REPORT.md](FRONTEND_PAGES_REPORT.md), and [USER_FLOWS_REPORT.md](USER_FLOWS_REPORT.md) for full user flows. For every aspect of the project, see [PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md).

*Last updated: February 6, 2026. Backend: 97% production ready with comprehensive validation (DataAnnotations + ValidationHelper), 169 unit tests passing across 10 services, Result pattern across all services.*
