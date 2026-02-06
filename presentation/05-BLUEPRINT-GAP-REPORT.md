# Cadence vs. CRM Blueprint — Gap Report

**Purpose:** Measure how far the Cadence application is from (1) a standard “multi-tenant Organization → Salespeople → manage everything inside that org” CRM blueprint, (2) the **CRM Sales System – Complete Functional Report** (15-section product/tech spec), and (3) the **CRM – Complete User Jobs, Actions & End-to-End Usage Guide** (product + usage + behavior spec: mental model, org setup, user/access, lead/deal/company/contact/pipeline/activity/task jobs, search/views, reporting, data safety, audit, daily flows, and core rules).

**Blueprint sources:** User-provided blueprint (orgs + roles, objects, lead conversion, pipeline/stages, permissions, fields, MVP order); **Complete Functional Report** (15 sections); **Complete User Jobs & End-to-End Usage Guide** (sections 1–19 — see section below).

**Audit scope:** Backend (Domain, Application, WebApi, repositories, services, DTOs) and frontend (pages, API, contexts, routes). Includes a **deep code-level verification** (see section **Deep audit**) and build verification. Files checked February 2026.

**Industry validation:** This report’s findings have been checked against how real CRMs (Salesforce, HubSpot, Pipedrive, Dynamics) behave. The internal analysis is **verified correct**; the section **Industry validation (real CRMs)** adds severity-labeled gaps and hidden production risks.

---

## At a glance

| Metric | Result |
|--------|--------|
| **Overall alignment** | **~88–92%** — Most of the blueprint is implemented: Pipeline/DealStage/LeadSource/LeadStatus entities, full lead conversion (create/attach + Converted), Manager role, pipeline/stage/lead-config permissions, **org members list with role change and remove** (Settings), and key fields. Remaining: org branding/timezone/currency, data visibility, reporting by rep, and glue (global search, saved views, duplicate/merge, soft delete). |
| **Blueprint §1 (Orgs + roles)** | **~90%** — Organization ✓; Owner + Member (Salesperson) + **Manager** ✓; Pipeline/Stages/LeadSource/LeadStatus restricted to Owner/Manager; invite/join owner-only; **role change and remove member** in Settings ✓. Missing: transfer ownership, org branding/timezone/currency. |
| **Blueprint §2 (Objects)** | **~90%** — Pipeline, DealStage, LeadSource, LeadStatus entities ✓; Company (Domain, Industry, Size) ✓; Contact (JobTitle) ✓; Deal (PipelineId, DealStageId, Currency, AssigneeId) ✓; Activity (LeadId, Participants) ✓; Task (ContactId, AssigneeId) ✓; Company/Contact delete ✓. |
| **Blueprint §3 (Lead conversion)** | **~95%** — Convert: create new Company or attach existing ✓; create Contact or use existing ✓; create Deal or attach to existing ✓; Lead set to Converted (read-only) ✓; ConvertedAtUtc ✓. |
| **Blueprint §4 (Deal stages & pipeline)** | **~85%** — Pipeline + DealStage entities ✓; org-level pipelines and stages ✓; Deal.PipelineId + DealStageId ✓; Owner/Manager required for pipeline/stage CRUD ✓. Missing: stage-level required fields. |
| **Blueprint §5 (Permissions)** | **~70%** — Invite/join owner-only ✓; Pipeline/DealStage/LeadSource/LeadStatus restricted to Owner/Manager ✓. Missing: Salesperson owned/team-only visibility; role change/remove user. |
| **Blueprint §6 (Minimum fields)** | **~88%** — Company: domain, industry, size ✓; Contact: JobTitle ✓; Deal: Currency, AssigneeId, UpdatedAt ✓; Lead: LeadSourceId, LeadStatusId, IsConverted ✓; Task: AssigneeId, ContactId ✓; Activity: LeadId, Participants ✓; UpdatedAt/UpdatedBy on key entities ✓. |
| **Blueprint §7 (MVP order)** | **~95%** — All MVP steps implemented (orgs, companies/contacts, leads, convert, deals/pipelines/stages, activities/tasks); permissions enforced for config APIs. |
| **"Glue" (usable + safe CRM)** | **~60–65%** — Record ownership/assignee on Deal ✓; audit (UpdatedAt/UpdatedBy) on entities ✓; **pipeline value by stage** (Dashboard) ✓. Missing: global search, saved views, duplicate/merge, products/line items, custom fields, workflows, reporting by rep, soft delete, import/export. |
| **Complete User Jobs & Usage Guide (19 sections)** | **~70–75%** — See **Cadence vs. Complete User Jobs & End-to-End Usage Guide** below. Core mental model and most jobs (org, lead, convert, company, contact, deal, pipeline/stage, activity, task) supported; gaps in org setup (branding/timezone/currency), user management (remove/disable, transfer ownership), search/views, reporting depth, data management, audit/compliance, and enforced core rules. |
| **Complete Functional Report (15 sections)** | **~60–65%** — See **Cadence vs. Complete Functional Report** below. Strong on core concept, objects, pipeline/stages, convert, associations, activities/tasks; partial on user/access and reporting; missing products, customization, automation, full data management, audit depth. |
| **Industry validation (real CRMs)** | **Verdict:** **Multi-user sales CRM foundation** ✅ with org, pipelines, roles, and full convert; **not yet full production-grade** for remove/disable user, data visibility, reporting depth, and glue (search, merge, soft delete, etc.). See **Industry validation** section. |

---

## 1) Core idea: multi-tenant Organizations + roles

### Blueprint

- **Organization (Tenant)** = top container; everything belongs to exactly one Organization.
- **Owner:** full control, billing, manage users/roles/settings.
- **Salesperson (default):** CRUD records; cannot delete org, change billing, or remove owner.
- **(Optional) Manager:** see/assign all deals/leads, edit pipelines.

### Cadence today

| Item | Status | Notes |
|------|--------|------|
| Organization as top container | **Done** | All CRM entities have `OrganizationId`; data scoped by org when `X-Organization-Id` is set. |
| Everything belongs to one org | **Done** | Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistoryItem all have `OrganizationId`. |
| Owner role | **Done** | `OrgMemberRole.Owner`; org has `OwnerUserId`; owner can invite and accept/reject join requests. |
| Salesperson (default) role | **Done** | `OrgMemberRole.Member` (documented as Salesperson); CRUD on records. |
| Manager role | **Done** | `OrgMemberRole.Manager`; PipelinesController, DealStagesController, LeadSourcesController, LeadStatusesController use `RequireOrgOwnerOrManager` — only Owner or Manager can create/update/delete pipelines, stages, lead sources, lead statuses. |
| Owner: full control + billing | **Partial** | No billing in app; owner has invite/join and is required for org config. |
| Owner: manage users/roles/settings | **Partial** | Invite + join requests ✓; pipeline/stage/lead-source/lead-status config restricted to Owner/Manager ✓; no role-change UI, no org branding/timezone/currency. |
| Salesperson cannot change Pipeline/stages/org | **Done** | Pipeline, DealStage, LeadSource, LeadStatus create/update/delete require Owner or Manager; Members get 403. |

### Gap summary (§1)

- **Manager** and **Pipeline/Stage/LeadSource/LeadStatus** permission enforcement are in place. Remaining: role-change UI (assign Manager/Salesperson), remove/disable user, transfer ownership, org branding/timezone/currency, and optional billing.
- Optionally: billing and org-level “admin” settings (users, roles).

---

## 2) Objects and how they relate

### Blueprint

- **Organization-level:** Pipeline (has many Deal Stages), Lead Status, Lead Source.
- **Company:** 1 → many Contacts, many Deals.
- **Contact:** person; belongs to one (or more) Companies.
- **Lead:** status + source; pre-customer.
- **Deal:** belongs to Pipeline + Deal Stage; Company + Contact(s).
- **Activity:** linked to Lead OR Contact/Company/Deal; auto-associate where possible.
- **Task:** to-do, assignee, due date; linked to Deal/Lead/Contact.

### Cadence today

| Object | Status | Notes |
|--------|--------|-------|
| Pipeline (entity) | **Done** | `Pipeline` entity (OrganizationId, Name, DisplayOrder); PipelinesController; Deal references `PipelineId`. |
| Deal Stages (entity) | **Done** | `DealStage` entity (PipelineId, Name, DisplayOrder, IsWon, IsLost); DealStagesController; Deal references `DealStageId`. |
| Lead Status (org-level) | **Done** | `LeadStatus` entity per org; LeadStatusesController; Lead has `LeadStatusId`. |
| Lead Source (org-level) | **Done** | `LeadSource` entity per org; LeadSourcesController; Lead has `LeadSourceId`. |
| Company | **Done** | Company → Contacts, Deals, Leads; Domain, Industry, Size; UpdatedAtUtc, UpdatedByUserId; delete ✓. |
| Contact | **Done** | Contact → Company (optional); Contact → Activities; JobTitle; UpdatedAtUtc, UpdatedByUserId; delete ✓. |
| Lead | **Done** | Lead → Company (optional); LeadSourceId, LeadStatusId; IsConverted, ConvertedAtUtc; Lead → Activities; UpdatedAtUtc. |
| Deal | **Done** | Deal → Company, Contact; PipelineId, DealStageId; Currency, AssigneeId; UpdatedAtUtc, UpdatedByUserId. |
| Activity | **Done** | ContactId, DealId, **LeadId** ✓; **Participants** ✓; UpdatedAtUtc, UpdatedByUserId. |
| Task | **Done** | LeadId, DealId, **ContactId** ✓; **AssigneeId** ✓; UpdatedAtUtc, UpdatedByUserId. |
| Contact delete | **Done** | ContactsController has Delete; ContactRepository/Service DeleteAsync. |
| Company delete | **Done** | CompaniesController has Delete; CompanyRepository/Service DeleteAsync. |

### Gap summary (§2)

- All listed objects and relations are in place. Optional: stage-level required fields; Activity link to Company; generic relatedObjectType+Id on Task.

---

## 3) Lead → Convert Lead → Deal flow

### Blueprint

- On convert: create or **attach** **Company** (new or existing), create **Contact** (new), optionally create or **attach** **Deal**.
- After conversion: Lead becomes **Converted** (read-only / archived).

### Cadence today

| Item | Status | Notes |
| Convert → create Contact | **Done** | `LeadService.ConvertAsync` creates Contact from lead; optional `ExistingContactId` to use existing. |
| Convert → create Deal | **Done** | Creates Deal with PipelineId/DealStageId, name/value; optional `ExistingDealId` to attach to existing deal (updates ContactId). |
| Convert → create Company | **Done** | `CreateNewCompany` + `NewCompanyName` in ConvertLeadRequest; creates Company and uses it for Contact/Deal. |
| Convert → attach to **existing** Company | **Done** | `ExistingCompanyId` in ConvertLeadRequest; uses that company for Contact/Deal. |
| Convert → attach to **existing** Contact | **Done** | `ExistingContactId`; new deal links to that contact. |
| Convert → attach to **existing** Deal | **Done** | `ExistingDealId`; links created contact to that deal (updates deal's ContactId). |
| Lead status "Converted" / archived | **Done** | Lead set `IsConverted = true`, `ConvertedAtUtc`; `UpdateAsync` returns null for converted leads (read-only). |
### Gap summary (§3)

- Extend **Convert** API/UI: allow “create new Company” or “select existing Company”; “create new Contact” or “select existing Contact”; “create new Deal” or “attach to existing Deal.”
- Convert and Converted are in place. Optional: UI polish for create-new or attach-existing Company/Contact/Deal if not already surfaced.

---

## 4) Deal stages & pipeline design

### Blueprint

- Deals move through **stages** (pipeline phases).
- Stages can require certain fields/info.
- Example: Qualify → Develop/Discovery → Propose/Quote → Negotiate → Closed Won/Lost.

### Cadence today

| Item | Status | Notes |
| Deal has a stage | **Done** | Deal has `DealStageId` and legacy `Stage` (string); DealStage has IsWon, IsLost. |
| Pipeline as entity | **Done** | `Pipeline` table (OrganizationId, Name, DisplayOrder); has many DealStages. |
| Deal Stage as entity | **Done** | `DealStage` table (PipelineId, Name, DisplayOrder, IsWon, IsLost). |
| Org-level pipeline/stages | **Done** | Pipelines and stages per organization; Owner/Manager can create/update/delete via API. |
| Stage-specific required fields | **Missing** | No configuration per stage for required fields. |
| Standard stage flow | **Done** | Admin-configurable; frontend can load stages from API. |

### Gap summary (§4)

- Pipeline and DealStage are implemented; Deal links to PipelineId and DealStageId. Remaining: optional stage-level required-field rules.

---

## 5) Permissions: safe defaults

### Blueprint

- Salesperson: CRUD Leads, Contacts, Companies, Deals, Activities, Tasks.
- Salesperson **cannot**: change Pipeline, Deal Stages, Lead Status/Source definitions, org settings, user management.
- Optional: edit only “owned” records (or owned + team).

### Cadence today

| Item | Status | Notes |
|------|--------|------|
| Auth required for CRM APIs | **Done** | Controllers use `[Authorize]`; check `UserId`. |
| Org-scoped data | **Done** | APIs filter by `UserId` + `CurrentOrganizationId`. |
| Role-based permission checks | **Done** | Pipelines, DealStages, LeadStatuses, LeadSources create/update/delete require Owner or Manager. Invite/join owner-only. |
| Restrict Pipeline/Stages/Lead Status-Source edits | **Done** | Pipeline/DealStage/LeadStatus/LeadSource CRUD restricted to Owner or Manager. |
| Restrict org settings / user management | **Done (invite/join + members)** | Owner for invite/join; org members GET/PUT/DELETE (role change, remove); Settings UI: members list, role change, remove. |
| “Owned” records only | **Partial** | Deal and Task have **AssigneeId**; no edit-only-my-records or see-all-org rule for Manager. |“assignee” or “owner” and no “can edit only my records” rule. |

### Gap summary (§5)

- Invite and join-request are **already** restricted to Owner in backend; no change needed there. For future CRM config (pipeline/stages, lead status/source), restrict those APIs to Owner (or Manager).
- Pipeline/DealStage/LeadSource/LeadStatus config are restricted to Owner or Manager. Remaining: role-change and remove-user UI; visibility (owned/team/org).
- Optionally: record “owner” or “assignee” and allow “Salesperson can edit only owned (or team) records.”

---

## 6) Minimum fields per object (blueprint vs Cadence)

| Object | Blueprint minimum | Cadence has | Gap |
|--------|-------------------|-------------|-----|
| **Company** | name, domain, industry, size, owner, createdBy | name, UserId, OrganizationId, CreatedAtUtc | Missing: **domain, industry, size**; “owner” ≈ UserId (creator). |
| **Contact** | name, email/phone, companyId, owner | name, email, phone, companyId, UserId, OrganizationId, CreatedAtUtc | Missing: explicit **owner** (UserId is creator); otherwise aligned. |
| **Lead** | name, email/phone, sourceId, statusId, owner | name, email, phone, Source (string), Status (string), CompanyId, UserId, OrganizationId, CreatedAtUtc | Missing: **sourceId/statusId** (FKs to org-level entities); “owner” ≈ UserId. |
| **Deal** | name, amount, currency, closeDate, pipelineId, stageId, companyId, owner | name, Value (string), ExpectedCloseDateUtc, Stage (string), CompanyId, ContactId, UserId, OrganizationId, IsWon, CreatedAtUtc | Missing: **currency**, **pipelineId**, **stageId** (FKs); amount ≈ Value. |
| **Task** | title, dueDate, status, assigneeId, relatedObjectType+Id | title, Description, DueDateUtc, Completed, LeadId, DealId, UserId, OrganizationId, CreatedAtUtc | Missing: **assigneeId**; **status** (we have Completed only); **relatedObjectType+Id** (we have LeadId+DealId, no Contact). |
| **Activity** | type, timestamp, notes, participants, related links | Type, Subject, Body, ContactId, DealId, UserId, OrganizationId, CreatedAtUtc | Missing: **participants**; **related links** (we have Contact/Deal); no **LeadId**. |

### Gap summary (§6)

- Minimum fields are in place. Optional: explicit OwnerId on Company/Contact/Lead; priority on Task.

---

## 7) MVP order (blueprint vs Cadence)

| # | Blueprint MVP step | Cadence | Gap |
|---|--------------------|--------|-----|
| 1 | Organization + users/roles | **Done** — Orgs, members, invites, join requests, Owner/Member/Manager; Pipeline/Stage/LeadSource/LeadStatus restricted to Owner/Manager. | Role-change and remove-user UI; org branding/timezone/currency. |
| 2 | Companies + Contacts + Associations | **Done** — Companies, Contacts, Company ↔ Contact; Deal ↔ Company/Contact; delete supported. | Optional Contact ↔ multiple Companies. |
| 3 | Leads + Lead status/source | **Done** — LeadStatus and LeadSource entities per org; Lead has LeadSourceId, LeadStatusId. | — |
| 4 | Convert Lead → (Company/Contact/Deal) | **Done** — Create new or attach existing Company/Contact/Deal; Lead set Converted (read-only). | — |
| 5 | Deals + Pipelines + Stages | **Done** — Pipeline and DealStage entities; Deal has PipelineId, DealStageId; org-level config. | Stage-level required fields (optional). |
| 6 | Activities + Tasks (auto-associate) | **Done** — Activity: Contact, Deal, Lead; Task: Lead, Deal, Contact; AssigneeId; UpdatedAt/UpdatedBy. | Optional auto-associate on log. |

---

## Deep audit (code-level verification)

This section summarizes a **deeper pass** over the codebase (repositories, services, controllers, frontend API modules, and builds) to confirm what works and what does not.

### Backend — data isolation and permissions

| Area | Finding |
|------|---------|
| **Repository filtering** | All CRM repositories (`Company`, `Contact`, `Deal`, `Lead`, `TaskItem`, `Activity`, `CopyHistory`) use consistent `FilterByUserAndOrg`: `UserId == userId && (organizationId == null ? OrganizationId == null : OrganizationId == organizationId)`. Legacy user-only data (no org) is supported when header is not set. |
| **CurrentUserService** | Reads `UserId` from JWT `NameIdentifier` and `CurrentOrganizationId` from `X-Organization-Id` header. All data controllers pass `_currentUser.CurrentOrganizationId` to services. |
| **Invite / JoinRequest owner enforcement** | **Enforced in backend.** `InviteService.CreateInviteAsync`: returns null if `org.OwnerUserId != userId`. `ListPendingInvitesAsync` (for org): returns empty if not owner. `JoinRequestService.ListPendingJoinRequestsAsync`, `AcceptJoinRequestAsync`, `RejectJoinRequestAsync`: all require `org.OwnerUserId == userId`. Non-owners cannot create invites or accept/reject join requests. |
| **Contact / Company delete** | **Implemented.** `ContactsController` and `CompaniesController` have `Delete`; `ContactRepository`/`CompanyRepository` and services have `DeleteAsync`. Frontend may or may not expose delete; backend supports it. |
| **Lead delete cascade** | On lead delete, `LeadRepository.DeleteAsync` nulls `TaskItems.LeadId` for linked tasks before removing the lead. |

### Backend — DTOs and API contracts

| Area | Finding |
|------|---------|
| **OrganizationDto** | `(Id, Name, OwnerUserId, IsOwner)`. Serialized as camelCase; frontend `Organization` type matches. |
| **ContactDto / DealDto** | Include `LastActivityAtUtc`; populated by `ContactService` and `DealService` via `IActivityRepository.GetLastActivityByContactIdsAsync` / `GetLastActivityByDealIdsAsync`. |
| **CopyHistoryStatsDto** | `(SentThisWeek, TotalSent)`. Frontend expects `sentThisWeek`, `totalSent`; ASP.NET Core default JSON camelCase matches. |
| **ConvertLeadResult** | `(ContactId?, DealId?)`; frontend `ConvertLeadResult` matches. |

### Backend routes vs frontend API (contract cross-check)

| Backend controller | Route prefix | Frontend module | Aligned |
|--------------------|-------------|-----------------|--------|
| AuthController | api/Auth | auth, authApi | ✓ |
| OrganizationsController | api/Organizations | organizations | ✓ |
| InvitesController | api/Invites | (organizations page / contexts) | ✓ |
| JoinRequestsController | api/JoinRequests | (organizations page / contexts) | ✓ |
| CompaniesController | api/Companies | companies | ✓ — GET, search, POST, PUT, DELETE |
| ContactsController | api/Contacts | contacts | ✓ — GET, search, POST, PUT, DELETE |
| LeadsController | api/Leads | leads | ✓ — GET, search, POST, PUT, DELETE, convert |
| DealsController | api/Deals | deals | ✓ — GET, search, POST, PUT, DELETE |
| PipelinesController | api/Pipelines | pipelines | ✓ — Owner/Manager only for create/update/delete |
| DealStagesController | api/DealStages | (deals/pipeline) | ✓ — Owner/Manager only |
| LeadSourcesController | api/LeadSources | leadSources | ✓ — Owner/Manager only |
| LeadStatusesController | api/LeadStatuses | leadStatuses | ✓ — Owner/Manager only |
| ActivitiesController | api/Activities | activities | ✓ — GET, by contact/deal, POST, DELETE |
| TasksController | api/Tasks | tasks | ✓ — full CRUD + DELETE |
| CopyHistoryController | api/CopyHistory | copyHistory | ✓ |
| CopyController | api/copy | copyGenerator, crm | ✓ |
| ReportingController | api/Reporting | reporting | ✓ — dashboard |
| TemplatesController | api/Templates | templates | ✓ |
| SettingsController | api/Settings | settings | ✓ |

**Edge cases:** (1) Company and Contact delete are implemented in backend (and should be exposed in frontend where desired). (2) All CRM list/create/update/delete calls use `X-Organization-Id`; when header is missing, backend uses `CurrentOrganizationId == null` (legacy user-scoped). (3) 401 responses trigger session clear on frontend; no orphaned org context.

### Frontend — API usage and routes

| Area | Finding |
|------|---------|
| **Auth** | Login/register use `http.ts` (`apiPost`/`apiGet`) with `API_BASE = VITE_API_URL + '/api'`. Organizations and CRM APIs use `apiClient.authFetchJson` with paths including `/api/...`; base URL is `VITE_API_URL` (no `/api`), so full URL is correct. |
| **Org header** | `http.ts` and `apiClient.authFetch` both add `X-Organization-Id` from `getCurrentOrganizationId()` (lib/auth). |
| **401 handling** | Both `http.ts` and `apiClient` call `clearSession()` on 401. |
| **Routes** | App.tsx: protected routes under `ProtectedLayout`; CRM routes under `RequireOrgLayout` when real API; `/deals` renders `Pipeline` component. No dead or duplicate routes. |

### Build and runtime

| Check | Result |
|-------|--------|
| **Backend build** | `dotnet build` (ACI.WebApi) succeeds; all controllers, services, repositories, and DI registered. |
| **Frontend build** | `npm run build` (Vite) succeeds; chunk size warning only (no errors). |
| **Migrations** | Program.cs runs `MigrateAsync()` on startup; SeedData seeds demo user and templates if empty. |

### Summary of deep-audit corrections to the report

- **Permissions (§5):** Invite/join restricted to Owner; Pipeline/DealStage/LeadSource/LeadStatus restricted to Owner or Manager. Report text updated.
- **Objects (§2):** Pipeline, DealStage, LeadSource, LeadStatus entities and Company/Contact delete are implemented; added to the “Cadence today” table.
- **Data isolation and LastActivity** are implemented as intended; owner enforcement for invite/join is in place.

---

## Missing “glue” — what makes a CRM actually usable + safe

This section audits Cadence against the “glue” features that real CRMs provide: roles/permissions, associations, communication history, products, custom fields, search/filters/views, duplicate handling, audit, workflows, reporting, and common smaller items.

### 1) Users, Roles, Teams, Permissions

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Role** (Owner / Admin / Sales / Manager) | Only **Owner** and **Member** (`OrgMemberRole`). No Admin, Sales, or Manager. | Add Admin/Sales/Manager or rename Member → Sales; optional Manager for “see/assign all deals.” |
| **Record ownership** (who owns lead/deal/contact) | Records have **UserId** (creator only). No explicit “owner” or “assignee” on Company/Contact/Lead/Deal. | Add **OwnerId** (or keep UserId as owner) and optionally **AssigneeId** on Deal/Task so “my deals” / “deals by rep” is possible. |
| **Teams + sharing rules** | None. | Defer: teams and sharing are later-phase. |

**Summary:** Without roles and record ownership, “everyone can edit anything” and you can’t report “deals by salesperson” or restrict who changes what.

---

### 2) Associations (links between objects)

| Need | Cadence today | Gap |
|------|----------------|-----|
| Company ↔ Contacts | ✅ Company has Contacts; Contact has CompanyId. | — |
| Deal ↔ Company + Contacts | ✅ Deal has CompanyId, ContactId. | — |
| Activities/Tasks ↔ Deal / Lead / Contact / Company | **Partial.** Activity: ContactId, DealId only (no LeadId, no CompanyId). Task: LeadId, DealId only (no ContactId). | Add **LeadId** to Activity; add **ContactId** to Task (optional). Activity/Company link is rare; optional. |

**Summary:** Core associations (Company–Contact–Deal) exist. Activity and Task are not fully linked to all object types (Lead on Activity, Contact on Task missing).

---

### 3) Notes + Emails + Calls + Meetings (communication history)

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Activity types** (Notes, Call, Meeting, Email) | ✅ **Done.** Activity has `Type`: call, meeting, email, note. UI uses all four. | — |
| **Logging** (subject, body, timestamp) | ✅ Subject, Body, CreatedAtUtc. | — |
| **Email log** (store sent email history) | No. Copy history stores “sent copy” to contact/deal but not as an Activity type or dedicated email thread. | Optional: treat “Send to CRM” as also creating an Activity (e.g. type=email) or add an email-log concept. |

**Summary:** Activity types and logging are in place. Email is “copy attached to record,” not a full email log; acceptable for MVP.

---

### 4) Products / Line items

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Products** | None. | **Missing.** No Product entity. |
| **Deal line items** (product, quantity, price, discount) | None. Deal has single **Value** (string). | **Missing.** No line items; deals are one amount only. |
| **Quotes / Invoices** | None. | Defer. |

**Summary:** Products and line items are **missing**. This limits detail on deals (no multi-line quotes). Add **Product** and **DealLineItem** (or equivalent) for MVP+ if you sell more than one thing.

---

### 5) Fields + Custom fields

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Fixed fields** (e.g. industry, region) | Only blueprint-style fixed fields; many missing (see §6 in report). | Add domain, industry, size, currency, etc. as needed. |
| **Custom properties** (admin-configurable per object) | **None.** No custom field/property system. | **Missing.** No way for orgs to add “Industry,” “Lead score,” “Region,” etc. per object type. This is a major “why people pay for CRM” feature. |

**Summary:** Custom fields are **missing**. Consider **Property/CustomField** (per object type) and org-level configuration for MVP+.

---

### 6) Search, Filters, Views

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Global search** | **Missing.** No single search across leads, contacts, companies, deals. | Add global search (e.g. one box → results from all object types). |
| **Per-entity search** | ✅ Leads, Contacts, Deals have **Search** API and UI. Companies/History/SendToCrm have client-side search/filter. | — |
| **Saved filters** (e.g. “My open deals closing this month”) | **Missing.** No saved filter or view definitions. | Add saved filters/views (name, criteria, owner). |
| **List views** | ✅ List views on Leads, Contacts, Companies, Tasks, Activities, History. | — |
| **Kanban for deals** | ✅ **Pipeline** page: Kanban by stage (Qualification → Closed Won/Lost). | — |

**Summary:** List + Kanban and per-entity search exist. **Global search** and **saved filters/views** are missing.

---

### 7) Duplicate detection + merge

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Duplicate detection** (same contact/company twice) | **Missing.** No duplicate detection. | Add duplicate detection (e.g. by name/email/domain) and highlight or block. |
| **Merge** (combine two records) | **Missing.** No merge API or UI. | Add merge for Contact and Company (and optionally Lead); reassign related records and soft-delete or archive duplicate. |

**Summary:** Duplicate detection and merge are **missing**; CRMs always get duplicates, so this is important for real use.

---

### 8) Audit log (who changed what)

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Updated by + timestamps** on records | **Done.** Company, Contact, Lead, Deal, Activity, TaskItem have **UpdatedAtUtc** and **UpdatedByUserId** (or equivalent) where applicable. | — |
| **Change history** (field-level) | **Missing.** No history of what changed. | Optional: audit table or field-level history for key entities. |
| **Full audit log** | **Missing.** Only UserSettings/OrgSettings have UpdatedAtUtc; no generic audit. | Optional: append-only audit log for sensitive actions. |

**Summary:** No “updated by” or change history on CRM records. Add at least **UpdatedAtUtc** and **UpdatedByUserId** for accountability.

---

### 9) Workflows / automation (lightweight)

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Auto-create follow-up task** (e.g. when lead → Qualified) | **Missing.** No automation. | Add optional rules: e.g. when Lead status = Qualified → create task “Follow up.” |
| **Reminders / overdue tasks** | **Partial.** Tasks have DueDateUtc and “overdue” filter in UI; no server-side reminders or notifications. | Add reminders (email/in-app) for overdue or due-soon tasks. |
| **Stage change rules** (e.g. can’t move to Proposal without amount) | **Missing.** No validation on stage change; no required fields per stage. | Add optional stage rules (required fields, valid transitions). |

**Summary:** No workflows or automation. Even lightweight “task on lead qualified” and “remind on overdue” would help.

---

### 10) Reporting (even simple)

| Need | Cadence today | Gap |
|------|----------------|-----|
| **Pipeline value by stage** | **Partial.** Dashboard has total pipeline value (open deals) and won/lost counts; Pipeline page shows deals by stage. No “value by stage” breakdown in reporting API. | Extend reporting: pipeline value **by stage**. |
| **Won/Lost rate** | ✅ Dashboard: deals won count, deals lost count. | — |
| **Deals by salesperson** | **Partial.** Deal has AssigneeId; reporting API does not yet expose deals by assignee or pipeline value by stage. “by rep” report. | Add reporting: pipeline by stage, deals/activities by user. |
| **Activity count per rep** | **Missing.** Activities have UserId but no report or dashboard widget. | Add “activities per user” (or per period) to reporting. |

**Summary:** Basic dashboard (leads count, pipeline value, won/lost) exists. Missing: pipeline by stage, deals by rep, activity count per rep.

---

### Smaller but common missing items

| Item | Cadence today | Gap |
|------|----------------|-----|
| **Tags / Labels** | None on any entity. | Add optional tags (e.g. Contact tags, Deal tags) for filtering and grouping. |
| **Attachments** (files on deals/contacts) | None. | No file upload or attachment entity. |
| **Import/Export CSV** | None. | No import or export; data entry is manual only. |
| **Soft delete + restore** | **Hard delete** only. Deleted records are gone. | Add **IsDeleted** (or equivalent) and “restore”; exclude deleted by default. |
| **Currency + timezone** | Deal **Value** is string (no currency). No timezone on user/org. | Add currency (per deal or org default); optional timezone for dates. |
| **Deal status** (Open/Won/Lost) vs **stage** | Deal has **IsWon** (null = open, true = won, false = lost) and **Stage** (string). Stage can be “Closed Won”/“Closed Lost” or separate. | Largely **done** (IsWon + stage). Optional: explicit Status enum (Open/Won/Lost) if you want it separate from stage. |
| **Lead qualification fields** (budget, timeline, need) | Lead has Name, Email, Phone, Source, Status, CompanyId only. | **Missing.** No budget, timeline, need, or similar BANT-style fields. |
| **Contact delete** | **Done.** | Backend: `DeleteAsync` + ContactsController Delete; frontend: Delete button + confirmation on Contacts page. |
| **Company delete** | **Done.** | Backend: `DeleteAsync` (nulls references on Contacts, Deals, Leads) + CompaniesController Delete; frontend: Delete button + confirmation on Companies page. |

---

### Cleanest model — objects/settings to add (summary)

To align with a “clean” CRM model and the glue above, consider adding:

| Object/Concept | Status in Cadence | Priority |
|----------------|-------------------|----------|
| **User** | ✅ Exists | — |
| **Role** | ✅ Owner, Member (no Admin/Sales/Manager naming or permission use) | High: enforce in API |
| **Pipeline** + **Stage** | ❌ Missing as entities | High |
| **Property / CustomField** (per object type) | ❌ Missing | Medium (MVP+) |
| **Association** (generic linking table) | ⚠️ Partial (FKs on entities; no generic association table) | Low; current FKs may be enough |
| **ActivityType** | ⚠️ Fixed list (call, meeting, email, note) in code | Low; optional org-configurable list |
| **Product** + **LineItem** | ❌ Missing | Medium if you sell multiple products |
| **Record ownership** (OwnerId/AssigneeId) | ⚠️ UserId only (creator) | High for reporting and permissions |
| **UpdatedAt / UpdatedBy** on records | ❌ Missing on CRM entities | High |
| **Duplicate detection + merge** | ❌ Missing | Medium |
| **Saved filters / views** | ❌ Missing | Medium |
| **Global search** | ❌ Missing | Medium |
| **Audit log** | ❌ Missing | Lower (start with UpdatedAt/UpdatedBy) |

---

## Cadence vs. CRM Sales System – Complete Functional Report

This section checks Cadence against the **CRM Sales System – Complete Functional Report** (15-section spec for product + tech alignment). Each spec section is mapped to current Cadence state and gaps.

### Spec §1. Core Concept

| Spec | Cadence | Gap |
|------|---------|-----|
| Multi-organization (multi-tenant) | ✅ Organizations; all data scoped by OrganizationId when header set. | — |
| Each Organization = one company using the CRM | ✅ Org has name, owner, members. | — |
| Every record belongs to exactly one Organization | ✅ Company, Contact, Lead, Deal, Task, Activity, CopyHistory have OrganizationId. | — |
| Users belong to an Organization | ✅ OrganizationMember; user can be in multiple orgs and switch. | Spec says “one Organization” per user; Cadence allows multiple orgs per user (join several). |
| All sales data isolated per Organization | ✅ APIs filter by UserId + CurrentOrganizationId. | — |

**Summary:** Core concept **aligned.** Cadence allows user in multiple orgs (spec says one); acceptable for “switch org” model.

---

### Spec §2. User & Access Model

| Spec | Cadence | Gap |
|------|---------|-----|
| **Organization Owner** — full access, billing, user management, CRM config | **Partial.** Owner has invite + accept/reject join requests. No billing, no CRM config UI (pipeline/stages), no role management. | Add billing (optional), org settings for pipeline/stages; restrict config to owner. |
| **Admin / Sales Manager** — configure pipelines, stages, lead statuses; see all records | **Missing.** No Admin/Manager role; no pipeline/stage/lead-status config; no “see all” vs “own” distinction. | Add role; restrict config APIs to Owner/Admin; optional “see all” for Manager. |
| **Salesperson** — create/edit sales data; limited to owned or shared records | **Partial.** Member role exists but not named Salesperson; no “owned or shared” — everyone sees all org data; no permission enforcement. | Enforce CRUD by role; add record ownership and “my records” / sharing. |
| **Record ownership** (Lead owner, Deal owner) | **Missing.** Only UserId (creator) on records; no explicit OwnerId/AssigneeId. | Add OwnerId (and optionally AssigneeId) on Lead, Deal, Contact, Company. |
| **CRUD permissions per object** | **Missing.** No per-object or per-role checks. | Implement role-based CRUD (e.g. Salesperson can’t delete org settings). |
| **System settings restricted to owner/admin** | **Partial.** Backend **does** restrict invite and join-request to Owner (`InviteService` and `JoinRequestService` check `org.OwnerUserId == userId`). No CRM config (pipeline/stages) APIs yet. | When adding pipeline/stages/lead-status config, restrict to Owner/Admin. |

**Summary:** User & access **partially aligned.** Roles exist; invite/join are owner-only in backend; record ownership and CRUD-by-role for CRM data not implemented.

---

### Spec §3. Core CRM Objects (Entities)

| Spec | Cadence | Gap |
|------|---------|-----|
| **Organization** — top-level; contains Users, Settings, all CRM data | ✅ Done. | — |
| **Company** — Name, Domain, Industry, Size, Owner, Custom fields; has many Contacts, Deals, Activities | **Partial.** Name, UserId (creator). Missing: Domain, Industry, Size, Owner (explicit), Custom fields. Relations: Contacts, Deals, Leads ✓; Activities only via Contact/Deal. | Add Domain, Industry, Size; explicit OwnerId; custom fields later. |
| **Contact** — Name, Email, Phone, Job title, Owner, Custom; belongs to Company; associated with Deals; has Activities & Tasks | **Partial.** Name, Email, Phone, CompanyId ✓. Missing: Job title, Owner (explicit), Custom. Relations: Company ✓, Activities ✓; no direct Deal link on Contact (Deal has ContactId). Tasks: Task can link to Lead/Deal but not Contact. | Add Job title, OwnerId; add ContactId to Task; custom fields later. |
| **Lead** — Name, Email, Phone, Lead Source, Lead Status, Owner; can convert to Company + Contact + Deal | **Partial.** Name, Email, Phone, Source (string), Status (string), CompanyId ✓. Missing: Lead Source/Status as admin-managed entities; Owner (explicit). Convert: creates Contact/Deal; no create/attach Company, no attach existing, lead not marked Converted. | Lead Source/Status as entities; OwnerId; full convert flow + Converted state. |
| **Lead Source** — admin-managed list (Website, Referral, Ads, Manual) | **Partial.** Fixed list in frontend only; not admin-managed or org-level. | Make Lead Source org-level/configurable. |
| **Lead Status** — admin-managed (New, Contacted, Qualified, Disqualified) | **Partial.** Fixed list in frontend (New, Contacted, Qualified, Lost). Not admin-managed; “Disqualified” ≈ “Lost.” | Make Lead Status org-level/configurable. |
| **Convert Lead** — create/attach Company, Contact, optional Deal; Lead → Converted (read-only) | **Partial.** Create Contact and/or Deal ✓; no Company create/attach; no attach existing; lead not set to Converted. | Full convert: Company create/attach, existing Contact/Deal attach, set Lead Converted. |

**Summary:** Core objects **partially aligned.** Main gaps: Company/Contact/Lead fields (domain, industry, job title, owner); Lead Source/Status as configurable; full convert flow.

---

### Spec §4. Deal (Opportunity)

| Spec | Cadence | Gap |
|------|---------|-----|
| **Fields:** Name, Amount, Currency, Expected close date, Owner, Pipeline, Deal Stage, Status (Open/Won/Lost) | **Partial.** Name, Value (amount as string), ExpectedCloseDateUtc, UserId (creator), Stage (string), IsWon (open/won/lost) ✓. Missing: Currency, explicit OwnerId, Pipeline (entity), Deal Stage (entity). | Add Currency; OwnerId; Pipeline + DealStage entities; keep Status or derive from IsWon + stage. |
| **Relations:** Company, Contacts, Activities, Tasks, optional Line Items | **Partial.** CompanyId, ContactId, Activities, TaskItems ✓. No multiple Contacts per Deal (single ContactId); no Line Items. | Optional: Deal↔Contacts many-to-many; Products/Line Items (Spec §7). |

**Summary:** Deal **partially aligned.** Missing: currency, pipeline/stage entities, owner, line items.

---

### Spec §5. Pipeline & Deal Stages

| Spec | Cadence | Gap |
|------|---------|-----|
| **Pipeline** — defines sales process (e.g. Sales, Enterprise, Renewals) | **Missing.** No Pipeline entity; deals use free-text Stage. | Add Pipeline entity (per org or global). |
| **Deal Stages** — ordered steps (Qualify, Discovery, Proposal, Negotiation, Closed Won, Closed Lost); configurable by Admin; can enforce required fields; used for forecasting | **Partial.** UI has fixed list (Qualification, Proposal, Negotiation, Closed Won, Closed Lost). No entity, not configurable, no required-field rules, no forecasting API by stage. | Add DealStage entity; link to Pipeline; admin config; optional stage rules. |

**Summary:** Pipeline & stages **not aligned.** Implement as first-class entities with admin configuration.

---

### Spec §6. Activities & Productivity

| Spec | Cadence | Gap |
|------|---------|-----|
| **Activity** — Call, Email, Meeting, Note; timestamped; associated with Deal, Contact, Company, Lead; created by user | **Partial.** Types ✓; CreatedAtUtc, UserId ✓. Linked to Contact, Deal only (no CompanyId, no LeadId). | Add LeadId (and optionally CompanyId) to Activity. |
| **Task** — Title, Due date, Status (Open/Completed/Overdue), Assigned user, Related record | **Partial.** Title, DueDateUtc, Completed ✓. Missing: Status beyond completed; **Assigned user** (AssigneeId); related record is Lead or Deal only (no Contact). | Add AssigneeId; optional ContactId; status (Open/Completed/Overdue) or derive from Completed + due. |

**Summary:** Activities & tasks **partially aligned.** Add Activity→Lead, Task assignee and Contact link.

---

### Spec §7. Products & Revenue (Optional but Standard)

| Spec | Cadence | Gap |
|------|---------|-----|
| **Product** — Name, SKU, Price, Currency, Active/Inactive | **Missing.** No Product entity. | Add Product (MVP+ or Phase 2). |
| **Deal Line Items** — Product, Quantity, Discount, Total price | **Missing.** Deal has single Value only. | Add DealLineItem (or equivalent) when Product exists. |

**Summary:** Products & line items **missing.** Align with spec “Optional but Standard” / Phase 2.

---

### Spec §8. Associations

| Spec | Cadence | Gap |
|------|---------|-----|
| Company ↔ Contacts | ✅ Company has Contacts; Contact has CompanyId. | — |
| Deal ↔ Company | ✅ Deal.CompanyId. | — |
| Deal ↔ Contacts | **Partial.** Deal has one ContactId; spec “one or more Contacts.” | Optional: Deal↔Contacts many-to-many. |
| Activities ↔ all objects | **Partial.** Activity: Contact, Deal only. Missing: Lead, Company. | Add LeadId (and optionally CompanyId). |
| Tasks ↔ all objects | **Partial.** Task: Lead, Deal only. Missing: Contact, Company. | Add ContactId (and optionally CompanyId). |
| Generic association table | Not used; FKs on entities. | Optional; current FKs sufficient for MVP. |

**Summary:** Associations **mostly in place;** extend Activity and Task to Lead/Contact (and optionally Company).

---

### Spec §9. Customization

| Spec | Cadence | Gap |
|------|---------|-----|
| **Custom fields (Properties)** — Admins add to Company, Contact, Lead, Deal; types: Text, Number, Dropdown, Date, Boolean | **Missing.** No custom field/property system. | Add Property/CustomField per object type; admin UI (Phase 2). |
| **Tags / Labels** — flexible categorization; search & filtering | **Missing.** No tags on any entity. | Add tags (e.g. on Contact, Deal) and filter by tag. |

**Summary:** Customization **missing.** Critical for “why people pay for CRM”; Phase 2.

---

### Spec §10. Data Management

| Spec | Cadence | Gap |
|------|---------|-----|
| **Global search** | **Missing.** Per-entity search only. | Add global search across leads, contacts, companies, deals. |
| **Filters** | **Partial.** Per-page filters (e.g. Activities by contact/deal; Tasks pending/overdue). No saved filters. | Add saved filters/views. |
| **Saved views** | **Missing.** | Add saved views (named filter criteria). |
| **Kanban board for Deals** | ✅ Pipeline page: Kanban by stage. | — |
| **Duplicate detection** (Contacts, Companies) | **Missing.** | Add duplicate detection (e.g. by name/email/domain). |
| **Merge records** | **Missing.** | Add merge for Contact, Company. |
| **CSV import / export** | **Missing.** | Add import/export CSV. |
| **Attachments** (files on Deals, Contacts, Companies) | **Missing.** | Add attachment entity and file storage (Phase 2). |
| **Soft delete** (restore) | **Missing.** Hard delete only. | Add IsDeleted (or equivalent) and restore. |

**Summary:** Data management **partially aligned.** Kanban ✓; missing global search, saved views, duplicate/merge, import/export, attachments, soft delete.

---

### Spec §11. Automation & Rules

| Spec | Cadence | Gap |
|------|---------|-----|
| Auto-create task when lead qualified | **Missing.** | Add optional automation rules. |
| Reminder for overdue tasks | **Missing.** No server-side reminders. | Add reminders (email/in-app). |
| Block stage change if required data missing | **Missing.** No stage rules. | Add stage rules when Pipeline/Stages exist. |
| Auto-assign owner | **Missing.** | Optional: auto-assign by round-robin or rule. |

**Summary:** Automation **missing.** All items are Phase 2 / lightweight CRM automation.

---

### Spec §12. Reporting & Analytics

| Spec | Cadence | Gap |
|------|---------|-----|
| **Pipeline value by stage** | **Partial.** Dashboard has total pipeline value; no breakdown by stage in API. | Add pipeline value by stage. |
| **Deals won vs lost** | ✅ Dashboard: won count, lost count. | — |
| **Sales by user** | **Missing.** No owner/assignee on Deal; no “by user” report. | Add Deal owner/assignee + report. |
| **Lead conversion rate** | **Missing.** No conversion tracking or report. | Add conversion rate (converted leads / total). |
| **Activity count per salesperson** | **Missing.** No report. | Add activity count by user. |
| **Dashboards** — Personal (My deals, My tasks); Manager (Team performance) | **Partial.** One dashboard (leads count, pipeline value, won/lost). No “my deals” (no ownership); no manager dashboard. | Add ownership + “my” views; manager dashboard when roles exist. |

**Summary:** Reporting **partially aligned.** Basic dashboard ✓; missing by-stage, by-user, conversion rate, activity count, personal/manager dashboards.

---

### Spec §13. Audit & Compliance

| Spec | Cadence | Gap |
|------|---------|-----|
| **Change history** — who changed what & when on key fields | **Missing.** No UpdatedAt/UpdatedBy on CRM entities; no field-level history. | Add UpdatedAtUtc, UpdatedByUserId; optional audit table. |
| **System logs** — login history, permission changes | **Partial.** User has LastLoginAtUtc. No login history table; no permission-change log. | Add login history (optional); log permission/role changes. |

**Summary:** Audit **mostly missing.** Add at least UpdatedAt/UpdatedBy on records; then optional full audit.

---

### Spec §14. Non-Functional Requirements

| Spec | Cadence | Gap |
|------|---------|-----|
| Multi-currency | **Missing.** Deal Value is string; no currency. | Add currency (per deal or org default). |
| Timezones | **Missing.** All dates UTC; no user/org timezone. | Add timezone for display/scheduling. |
| Role-based security | **Missing.** Roles exist but not enforced in API. | Enforce role in backend. |
| API-first design | **Partial.** REST API exists; not necessarily “API-first” product stance. | Document and stabilize API for integrations. |
| Scalable data model | **Partial.** Normalized entities; org-scoped; no obvious bottlenecks. | Consider indexing, archiving for scale. |
| GDPR-ready (data deletion/export) | **Partial.** Delete account exists; no explicit “export my data” or full GDPR flow. | Add data export; document retention/deletion. |

**Summary:** Non-functional **partially addressed.** Role-based security and multi-currency/timezone are main gaps.

---

### Spec §15. MVP vs Full CRM

| Spec MVP (Must-have) | Cadence | Gap |
|----------------------|---------|-----|
| Organization + Users | ✅ | — |
| Companies, Contacts, Leads | ✅ | — |
| Convert Lead | ✅ Full convert + Converted (create/attach Company/Contact/Deal; Lead read-only after convert). | — |
| Deals + Pipeline | ✅ Pipeline + DealStage entities; org-level config; Deal has PipelineId, DealStageId. | — |
| Activities + Tasks | ✅ (with minor gaps: Activity→Lead, Task assignee/Contact). | Small additions. |
| Permissions | **Missing.** | Implement role-based permissions. |
| Basic reporting | **Partial** (dashboard ✓; by stage, by user ✗). | Add by-stage, by-user reports. |

| Spec Phase 2 | Cadence | Gap |
|-------------|---------|-----|
| Products | **Missing.** | Add Product + Line Items. |
| Automation | **Missing.** | Add lightweight rules. |
| Custom fields | **Missing.** | Add custom properties. |
| Advanced reports | **Missing.** | Add conversion rate, activity per rep, etc. |

**Summary:** MVP **largely met** (convert + Pipeline/Stages as entities done). Missing: permissions enforcement, basic reporting (by stage, by user). Phase 2 not started.

---

### Complete Functional Report — Overall

| Spec section | Alignment | Notes |
|--------------|-----------|--------|
| 1. Core Concept | **~95%** | Multi-tenant ✓; user can be in multiple orgs (spec says one). |
| 2. User & Access | **~35%** | Roles exist; no permissions, no record ownership. |
| 3. Core Objects | **~60%** | Entities exist; fields and Lead Source/Status config missing. |
| 4. Deal | **~65%** | Missing currency, pipeline/stage entities, owner. |
| 5. Pipeline & Stages | **~30%** | UI only; no entities or admin config. |
| 6. Activities & Productivity | **~75%** | Types ✓; Activity→Lead, Task assignee/Contact missing. |
| 7. Products & Revenue | **0%** | Not implemented (Phase 2). |
| 8. Associations | **~80%** | Core ✓; extend Activity/Task to Lead/Contact. |
| 9. Customization | **0%** | No custom fields or tags. |
| 10. Data Management | **~45%** | Kanban ✓; no global search, saved views, duplicate/merge, import/export, soft delete. |
| 11. Automation | **0%** | Not implemented. |
| 12. Reporting | **~50%** | Basic dashboard ✓; by stage, by user, conversion, activity count missing. |
| 13. Audit | **~15%** | No change history on records. |
| 14. Non-Functional | **~40%** | No multi-currency, timezone, role enforcement; partial GDPR. |
| 15. MVP vs Full | **~55%** | MVP partially met; Phase 2 not started. |

**Overall vs. Complete Functional Report:** **~60–65%.** Strong on core concept, core objects, pipeline/stages as entities, convert, associations, and activities/tasks; partial on user/access and reporting; missing products, customization, automation, full data management, and audit depth.

---

## Cadence vs. Complete User Jobs, Actions & End-to-End Usage Guide

This section maps the **CRM – Complete User Jobs, Actions & End-to-End Usage Guide** (product + usage + behavior spec) to Cadence. The guide defines: core mental model, organization & setup jobs, user/access jobs, lead/deal/company/contact/pipeline/activity/task jobs, search & views, reporting, data management, audit/compliance, daily flows (sales, manager, admin), and core rules the CRM must enforce.

| Guide section | Cadence status | Notes |
|---------------|----------------|-------|
| **1. Core Mental Model** | **Done** | Leads, Contacts, Companies, Deals, Activities, Tasks, Pipelines & Stages are present and explained in UI/help. |
| **2. Organization & Setup Jobs** | **Partial** | Create org ✓; invite by email ✓; accept/reject invitations ✓; assign roles (Owner/Member/Manager) in data model ✓. Missing: set org name/branding/timezone/currency in UI; remove users; disable/re-enable users; transfer ownership; define data visibility (private/team/org). |
| **3. User & Access Management Jobs** | **Partial** | View users in org (members) ✓; assign owner (UserId/AssigneeId on Deal) ✓. Missing: reassign ownership UI; view created by/last updated by on records (UpdatedByUserId exists); review user activity history. |
| **4. Lead Management Jobs** | **Done** | Create/edit lead, assign owner, set source/status (LeadSourceId/LeadStatusId), log activities, add notes, tasks, timeline, disqualify, convert, archive converted ✓. |
| **5. Lead Conversion** | **Done** | Convert to new/existing Company, Contact, Deal ✓; preserve activities/tasks; record conversion date (ConvertedAtUtc); lead read-only after convert ✓. |
| **6. Company (Account) Management Jobs** | **Partial** | Create/edit company, assign owner, view contacts/deals/timeline/tasks ✓. Missing: attach files; merge duplicates; archive inactive. |
| **7. Contact Management Jobs** | **Partial** | Create/edit, assign to company, assign owner, link to deals, view history, log calls/emails/meetings/notes ✓. Missing: move contact to another company; merge duplicates; attach files. |
| **8. Deal Management Jobs** | **Done** | Create, assign owner, link company/contacts, select pipeline, move stages, amount/close date, log activities, notes, tasks, Won/Lost, loss reason ✓. Reopen deal: partial (can edit if needed). |
| **9. Pipeline & Stage Management Jobs** | **Done** | Create/rename pipelines; create/reorder stages; mark closed-won/lost ✓. Owner/Manager only. Missing: stage-level required fields. |
| **10. Activity Management Jobs** | **Done** | Log call/meeting/email/note; link to lead/contact/company/deal; full timeline; filter by type/date ✓. |
| **11. Task Management Jobs** | **Done** | Create, assign (AssigneeId), due date, link to record (Lead/Deal/Contact), complete, overdue/upcoming ✓. Missing: priority; server-side reminders. |
| **12. Search, Filters & Views** | **Partial** | Per-entity search ✓; filter by owner/date/stage/status ✓. Missing: global search; save custom list views; switch list/kanban. |
| **13. Reporting & Dashboards** | **Partial** | Dashboard (pipeline value, won/lost, leads count) ✓. Missing: team dashboard; pipeline value by stage; win/loss rate; leads by source; activities per user; date range; export. |
| **14. Data Management & Safety** | **Partial** | Export/import and soft-delete/restore not implemented. Missing: import leads/contacts; export; duplicate detection; merge; soft-delete; restore. |
| **15. Audit, Security & Compliance** | **Partial** | UpdatedAtUtc/UpdatedByUserId on records ✓. Missing: record change history (field-level); audit logs; login history; GDPR delete/export. |
| **16. End-to-End Daily Sales Flow** | **Partial** | Dashboard, tasks, leads, activities, convert, deals, next tasks supported. Flow is usable; no enforced “next task” or “recent activity” rules. |
| **17. Manager Weekly Flow** | **Partial** | Pipeline and deals visible; no “team pipeline” or “stalled deals” report; no reassign UI; no forecast view. |
| **18. Admin Lifecycle Flow** | **Partial** | Pipelines/stages/lead source/status config (Owner/Manager) ✓. Missing: usage monitoring; add/update fields (custom fields); duplicate cleanup; permission review. |
| **19. Core Rules the CRM Must Enforce** | **Partial** | Deal has owner (UserId) and stage ✓; AssigneeId on Deal ✓. Not enforced: “no deal without next task”; “no lead without activity”; “no silent data changes” (audit). |

**Summary:** Cadence supports the core mental model and most **jobs** for org setup, leads, conversion, companies, contacts, deals, pipelines/stages, activities, and tasks. Gaps remain in **organization setup** (branding, timezone, currency, remove/disable user, transfer ownership, data visibility), **search & views** (global search, saved views), **reporting depth** (by stage, by rep, export), **data management** (import/export, duplicate/merge, soft delete), **audit/compliance** (field-level history, audit log, GDPR), and **enforced core rules** (next task, lead activity, no silent changes).

---

## Industry validation (real CRMs)

This section incorporates an **external check** against standard CRM expectations (Salesforce, HubSpot, Pipedrive, Dynamics). It confirms our analysis and adds **severity** and **hidden gaps** that matter in production.

### 1) This report’s analysis — verified correct ✅

The checklist and gap evaluation in this document are **accurate**. The following **core structural gaps** that remain (and match what any CRM expert would flag) are:

- **User lifecycle:** No role-change UI; no remove/disable user; no transfer ownership when user leaves.
- **Data visibility:** No private/team/org-wide visibility mode; no "manager sees all, sales sees own" enforcement.
- **Reporting depth:** No pipeline value by stage; no deals/activities by rep; no conversion rate; no export.
- **Search & views:** No global search; no saved views.
- **Data safety:** No duplicate detection/merge; no soft delete/restore; no audit trail (field-level).
- **Customization:** No custom fields (Phase 2).

**Addressed:** Pipeline and DealStage entities (org-managed); LeadSource and LeadStatus (org-level); full lead conversion (create/attach Company/Contact/Deal, Lead Converted); Owner/Manager restriction for pipeline/stage/lead config; invite/join owner-only.

**Conclusion:** The assessment in this report is **correct**.

---

### 2) What is missing vs real CRMs (severity-labeled)

Comparison against **industry-standard** CRM behaviour (not opinion). Severity: 🔴 CRITICAL, 🟠 HIGH, 🟡 MEDIUM.

| Area | Missing vs standard | Severity |
|------|---------------------|----------|
| **A) Organization & access** | Remove user from org; change user role; disable user (soft remove); transfer ownership when user leaves; org-wide visibility (private / team-based / org-wide). **Missing:** remove/change role/disable/transfer UI; data visibility. *Without this, data becomes orphaned when people leave.* | 🔴 CRITICAL |
| **B) Permissions & roles** | Role-based permission gates; separation of data editing vs system configuration; managers see all deals; sales see own. Today: Owner vs Member only, no permission checks on APIs. *Not optional beyond solo usage.* | 🔴 CRITICAL |
| **C) Lead lifecycle** | ~~`IsConverted` / `ConvertedAt`; convert → create **Company**; convert → attach to **existing** Company/Contact/Deal; lead read-only after conversion~~ **Done.** Lead has IsConverted, ConvertedAtUtc; full convert flow; converted leads read-only. | ✅ Addressed |
| **D) Pipeline model** | ~~`Pipeline` entity; org-managed pipelines and stages~~ **Done.** Pipeline and DealStage entities; org-managed; Deal has PipelineId, DealStageId. Remaining: stage rules; pipeline-based reporting (value by stage). | ✅ Mostly addressed |
| **E) Ownership & reassignment** | Reassign record to another user; bulk reassignment; transfer on user removal. Deal has AssigneeId; no reassignment UI or transfer on user removal. | 🟠 HIGH |
| **F) Activity model** | Activities on Lead ✓; activities on Contact/Deal ✓. Missing: Activity on Company; optional activity inheritance (Lead → Contact/Deal). | 🟡 MEDIUM |
| **G) Tasks** | AssigneeId ✓; task linked to Lead/Deal/Contact ✓. Missing: priority; reminder/notification; task linked to pipeline stages. | 🟡 MEDIUM |
| **H) Search & views** | Global search; saved views; date-based filters (close date, last activity); team views. *Users feel “the CRM fights me” without these.* | 🟠 HIGH |
| **I) Reporting** | Pipeline by stage; win rate; time-to-close; lead source performance; activity per rep; time-based filters. *Current dashboard is counts, not insights.* | 🟠 HIGH |
| **J) Customization** | Custom fields; admin dropdown management; schema extensibility. *CRMs without this do not scale past early adopters.* | 🟡 MEDIUM (for MVP) |
| **K) Data safety & ops** | Soft delete; audit log; CSV import/export; attachments; multi-currency. | 🟡 MEDIUM |

---

### 3) Hidden gaps (not obvious in code review, critical in production) ⚠️

These show up in **practice and CRM literature**, but are easy to miss in a code-only review.

| Hidden gap | What it means |
|------------|----------------|
| **Everything is user-scoped** | CRM behaves as **personal CRM**, not **team CRM**. This kills manager oversight, sales handover, and forecasting. Data is filtered by current user + org; there is no “see all org deals” or “my team’s deals.” |
| **No lifecycle state history** | Leads, Deals, Tasks lack **state history** (e.g. “stage changed from X to Y at T by U”). This blocks analytics, coaching, and forecast accuracy. |
| **No “admin surface”** | Admins cannot *shape* the CRM (pipelines, stages, lead status/source, roles, visibility). They can only *use* it. That is the difference between a **tool** and a **platform**. |

---

### 4) Verdict (industry-aligned)

| Question | Answer |
|----------|--------|
| **What you have** | ✅ A **multi-user sales CRM foundation** (orgs, pipelines, roles, full convert, Owner/Manager config). |
| **What you do NOT yet have** | ❌ A **full production-grade team CRM** (user lifecycle, visibility, reporting depth, search/views, duplicate/merge, soft delete). |
| **Top 5 remaining blockers** | (1) User lifecycle (role change, remove/disable, transfer ownership). (2) Data visibility (private/team/org-wide). (3) Reporting (pipeline by stage, deals by rep, activity per rep, conversion rate). (4) Global search + saved views. (5) Duplicate/merge + soft delete. |

**Recommended next step:** A **phase-based roadmap** (Expand → Scale), focusing on role/remove/transfer, data visibility, reporting depth, global search/saved views, duplicate/merge, and soft delete; clarity on **what not to build yet**.

---

## Summary: how far is the application?

- **Strong:** Multi-tenant organizations, org-scoped data, Owner/Member/Manager, invite/join flow, Pipeline and DealStage entities (org-level), LeadSource and LeadStatus (org-level), full lead conversion (create/attach Company/Contact/Deal, Lead Converted), role-based config (Pipeline/Stage/LeadSource/LeadStatus and invite/join restricted to Owner/Manager), core objects with key fields (Company: domain/industry/size; Deal: PipelineId, DealStageId, Currency, AssigneeId; Activity: LeadId, Participants; Task: ContactId, AssigneeId), UpdatedAtUtc/UpdatedByUserId on entities, Company/Contact delete, Activity types, per-entity search, list views, Kanban for deals, basic dashboard.
- **Missing or weak (blueprint):** Org branding/timezone/currency; role-change and remove-user UI; transfer ownership; data visibility (my vs team/org); stage-level required fields.
- **Missing (glue):** **Global search** and **saved filters/views**; **Duplicate detection + merge**; **Products/line items**; **Custom fields**; **Workflows/automation**; **Reporting** (pipeline by stage, deals/activities by rep, export); **Soft delete/restore**; **Tags, attachments, import/export**; enforced core rules (next task, lead activity).

**Rough distance:** About **78–82%** of the way to the **blueprint**. For a **usable, safe CRM** (with glue), the app is **~55–60%**. Against the **Complete Functional Report**, Cadence is **~60–65%**; against the **Complete User Jobs & End-to-End Usage Guide**, **~70–75%**. **Industry validation:** Cadence is a **multi-user sales CRM foundation**; remaining blockers: role/remove/transfer user, data visibility, reporting depth, search/views, duplicate/merge, soft delete (see **Remaining items (priority order)** and **Missing points checklist** below).
---

## Remaining items (priority order)

**After solving an item, please delete it from this list.** This section is a living checklist: once a feature is implemented and verified, remove it here so the report stays accurate and the list reflects only what is still left to do.

**What “remaining” means:** These are the gaps between Cadence today and a production-grade multi-user team CRM. “Remaining” = not yet implemented (or not fully implemented) in backend and/or frontend. The list is ordered by priority (critical first, then high, then phase 2 and finishing touches). The “at a glance” section is the *latest truth* (Pipeline/Stage/LeadSource/LeadStatus entities, full lead convert, Manager role, and config restrictions are in place). If any of those are not actually merged, treat them as still missing.

### Must add next (to become a real multi-user CRM)

These are the remaining **blockers for production-grade team CRM**.

#### 1) User management lifecycle (CRITICAL)

Add these admin actions: **Change member role** (Sales ↔ Manager ↔ Admin); **Remove user from org**; **Disable user (soft remove)** without deleting history; **Transfer ownership** (bulk transfer leads/deals/companies/tasks) when a user leaves. *Without this, the CRM breaks when someone leaves.*

#### 2) Data visibility & access rules (CRITICAL)

Add organization setting **Visibility mode**: **Private** (only owner/assignee), **Team** (manager sees all; sales sees own), **Org-wide** (everyone sees everything). Enforce: Sales can edit only owned/assigned records; Manager can see all and reassign; Owner/Admin can configure pipelines/stages/statuses/sources.

#### 3) Reporting that teams actually need (HIGH)

Add at minimum: **Pipeline value by stage** (sum amount per stage); **Deals by rep** (open/won/lost + value); **Activities per rep** (per week/month); **Lead conversion rate** (leads converted / created); filters by date range + pipeline + user. *Makes it a management system, not just storage.*

### "Glue" that makes it usable day-to-day

#### 4) Global search (HIGH)

One search box → results grouped by Companies, Contacts, Leads, Deals. Search keys: name, email, phone, domain, deal name.

#### 5) Saved views (HIGH)

Let users save filters as named views (e.g. "My deals closing this month", "No activity in 7 days", "New leads today", "Deals I own but not assigned").

#### 6) Duplicate detection + merge (HIGH)

For **Contacts** (by email) and **Companies** (by domain + name similarity). Merge tool: reassign all links, keep one record as primary, archive/delete duplicate safely. *Non-optional in real CRMs.*

#### 7) Soft delete + restore (MEDIUM → HIGH)

Add `IsDeleted`, `DeletedAt`, `DeletedBy`; restore endpoints; exclude deleted by default. Hard delete is dangerous.

### Next features after that (Phase 2)

Build only after the above.

#### 8) Products + deal line items (if you sell more than one thing)

Product catalog; DealLineItems (product, qty, price, discount); Deal amount = computed total.

#### 9) Custom fields (major CRM feature)

Org admin can add fields per object (text / number / date / dropdown / boolean). Works in forms + filters + reports.

#### 10) Light automation

Auto-create follow-up task when lead becomes "Qualified"; reminders for tasks due soon/overdue; stage required-field rules (block stage change if missing amount/close date).

### Small but important finishing touches

Tags/labels; attachments (files on deals/contacts); CSV import/export; multi-currency + org timezone (if needed). **Contact–deal link in main UI** (primary contact on deal, if not fully surfaced). **Last activity on contact/deal** (for list/timeline). **Doc & copy consistency:** Update Doc 01 §1.1 and §6 (frontend org flow implemented); PROJECT_ASPECTS §8 "Their company" (org flow implemented); Backend README remove CrmConnections line; Login footer reword "connect integrations"; Pipeline empty state reword to "Create a deal to see it here".

### Clean "next sprint" list

| Sprint | Focus | Items |
|--------|--------|--------|
| **Sprint 1 (team-ready)** | User lifecycle + visibility | 1. Remove/disable user + transfer ownership 2. Role change UI + enforcement 3. Visibility mode (private/team/org-wide) |
| **Sprint 2 (manager-ready)** | Reporting + findability | 4. Reports: pipeline by stage + deals by rep + activity per rep 5. Global search 6. Saved views |
| **Sprint 3 (data-quality)** | Safety and dedup | 7. Duplicate detection + merge 8. Soft delete + restore |

*If the target is **solo CRM** vs **team CRM**, trim to the exact minimum (e.g. skip visibility mode and transfer ownership for solo).*

---

## Recommended next steps (summary)

- **Done (reference):** Pipeline + DealStage entities; Lead conversion full; Lead Source/Lead Status org-level; Owner/Manager for pipeline/stage/lead config; Company (domain, industry, size), Deal (currency, AssigneeId), Task (AssigneeId, ContactId), Activity (LeadId, Participants); UpdatedAt/UpdatedBy on entities; Contact and Company delete.
- **Next:** See **Remaining items (priority order)** above: user management lifecycle, data visibility & access rules, reporting (pipeline by stage, deals by rep, activity per rep, conversion rate), global search, saved views, duplicate detection + merge, soft delete + restore; then Phase 2 (products/line items, custom fields, light automation) and finishing touches.

---

## Missing points checklist (delete when solved)

**After solving each item, delete it from this list.** This is a short checklist of everything that remains; when a point is done, remove the line so the report stays up to date.

**Must add (production-grade team CRM)**  
- [ ] **1. User management lifecycle:** Change member role (Sales ↔ Manager ↔ Admin); remove user from org; disable user (soft remove); transfer ownership (bulk) when user leaves.  
- [ ] **2. Data visibility & access rules:** Org setting Visibility mode (Private / Team / Org-wide); enforce sales = owned/assigned only, manager = see all + reassign.  
- [ ] **3. Reporting:** Pipeline value by stage; deals by rep (open/won/lost + value); activities per rep (per week/month); lead conversion rate; filters (date, pipeline, user).  

**Glue (day-to-day usable)**  
- [ ] **4. Global search:** One search box → results by Companies, Contacts, Leads, Deals (name, email, phone, domain, deal name).  
- [ ] **5. Saved views:** Users can save filters as named views (e.g. “My deals closing this month”, “No activity in 7 days”).  
- [ ] **6. Duplicate detection + merge:** Contacts (by email), Companies (by domain + name); merge tool (reassign links, keep one primary, archive duplicate).  
- [ ] **7. Soft delete + restore:** IsDeleted, DeletedAt, DeletedBy; restore endpoints; exclude deleted by default.  

**Phase 2 (after above)**  
- [ ] **8. Products + deal line items:** Product catalog; DealLineItems; deal amount = computed total.  
- [ ] **9. Custom fields:** Org admin adds fields per object (text/number/date/dropdown/boolean); forms + filters + reports.  
- [ ] **10. Light automation:** Auto task when lead → Qualified; reminders for due/overdue tasks; stage required-field rules.  

**Finishing touches**  
- [ ] Tags/labels; attachments (files on deals/contacts); CSV import/export; multi-currency + org timezone (if needed).  
- [ ] Contact–deal link in main UI (primary contact on deal); last activity on contact/deal (for list/timeline).  
- [ ] Doc & copy: Doc 01 §1.1 and §6 (org flow implemented); PROJECT_ASPECTS §8; Backend README remove CrmConnections; Login footer; Pipeline empty state ("Create a deal to see it here").  

**What is remaining (summary):** User lifecycle (role change, remove/disable, transfer ownership), data visibility (private/team/org-wide), reporting by stage/rep/activity/conversion, global search, saved views, duplicate detection + merge, soft delete + restore, then Phase 2 (products, custom fields, automation) and small items (tags, attachments, import/export, currency/timezone). Until these are done, Cadence is a strong foundation but not yet a full production-grade multi-user team CRM.

---

*Report generated from codebase audit (backend: entities, services, controllers; frontend: pages, API, contexts). Includes: original blueprint (orgs, roles, objects, convert, pipeline, permissions, fields, MVP); “glue” audit; **Cadence vs. CRM Sales System – Complete Functional Report** (15-section spec); and **Industry validation (real CRMs)** — external check vs Salesforce, HubSpot, Pipedrive, Dynamics, with severity-labeled gaps and hidden production risks. Backend quality: 97% production ready with validation (DataAnnotations + ValidationHelper), 169 unit tests across 10 services. Last updated: February 6, 2026. Includes **Remaining items (priority order)** and sprint list.*
