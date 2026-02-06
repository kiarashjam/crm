# Cadence vs. CRM Blueprint — Implementation Verification

This document checks each gap item from **05-BLUEPRINT-GAP-REPORT.md** and records whether it is **implemented** in the current codebase (backend + frontend).

---

## At a glance — implementation status

**Most of the blueprint is implemented.** Backend and frontend together cover:

- **§1 Orgs + roles:** Organizations, Owner/Member/Manager, invite/join, **org members list with role change and remove** (Settings), pipeline/stage/lead-config restricted to Owner/Manager.
- **§2 Objects:** Pipeline, DealStage, LeadSource, LeadStatus, Company (domain/industry/size), Contact (JobTitle, delete), Lead (source/status, convert), Deal (pipeline/stage/currency/assignee), Activity (LeadId, participants), Task (ContactId, assignee), Company/Contact delete.
- **§3 Lead convert:** Full convert flow (create or attach company/contact/deal); Lead set to Converted (read-only); **convert UI** with all options.
- **§4 Pipeline:** Pipeline + DealStage entities, org-level, Pipeline page uses API stages and kanban.
- **§5 Permissions:** Auth + org-scoped data; Owner/Manager for config APIs; invite/join + **member role change and remove** in UI.
- **§6 Minimum fields:** All listed fields present (domain, industry, size, JobTitle, currency, pipelineId, stageId, assigneeId, LeadId, participants, etc.).
- **§7 MVP order:** All six steps implemented (orgs, companies/contacts, leads, convert, deals/pipelines/stages, activities/tasks).

**Not implemented (minor or Phase 2):** Stage-specific required fields; saved views; duplicate/merge; products/line items; custom fields; workflows; soft delete; optional “edit only my records” for Salesperson.

---

## §1 — Core idea: multi-tenant Organizations + roles

| Item | Report status | Implemented? | Notes |
|------|----------------|--------------|--------|
| Organization as top container | Done | ✅ Yes | All CRM entities have `OrganizationId`. |
| Everything belongs to one org | Done | ✅ Yes | Company, Contact, Deal, Lead, Task, Activity, CopyHistory have `OrganizationId`. |
| Owner role | Done | ✅ Yes | `OrgMemberRole.Owner`; org has `OwnerUserId`; invite/join owner-only. |
| Salesperson (default) role | Partial | ⚠️ Partial | `Member` exists; not named "Salesperson" in UI; no separate permission enforcement for "CRUD only." |
| Manager role | Missing | ✅ Yes | `OrgMemberRole.Manager = 2` added; used in Pipelines/DealStages/LeadStatuses/LeadSources (Owner or Manager). |
| Owner: full control + billing | Partial | ⚠️ Partial | No billing in app; owner has invite/join; pipeline/stage/lead-status config is now in backend (owner/manager only). |
| Owner: manage users/roles/settings | Partial | ✅ Yes | Invite + join requests; **Settings** has org members list, role change (Member/Manager), and remove member (with confirm). Pipeline/stages/lead-status/source are GET-only in UI (CRUD is API-only for Owner/Manager). |
| Salesperson cannot change Pipeline/stages/org | Missing | ✅ Yes | Pipelines, DealStages, LeadStatuses, LeadSources **create/update/delete** require Owner or Manager via `RequireOrgOwnerOrManager`. |

---

## §2 — Objects and how they relate

| Item | Report status | Implemented? | Notes |
|------|----------------|--------------|--------|
| Pipeline (entity) | Missing | ✅ Yes | `Pipeline` entity, table, repository, service, API (`/api/pipelines`). |
| Deal Stages (entity) | Missing | ✅ Yes | `DealStage` entity, table, repository, service, API (`/api/dealstages`). |
| Lead Status (org-level) | Missing | ✅ Yes | `LeadStatus` entity, API (`/api/leadstatuses`). |
| Lead Source (org-level) | Missing | ✅ Yes | `LeadSource` entity, API (`/api/leadsources`). |
| Company | Done | ✅ Yes | Plus Domain, Industry, Size. |
| Contact | Done | ✅ Yes | Plus JobTitle; delete added. |
| Lead | Done | ✅ Yes | Plus LeadSourceId, LeadStatusId, IsConverted, ConvertedAtUtc. |
| Deal | Done | ✅ Yes | Plus PipelineId, DealStageId, Currency, AssigneeId. |
| Activity: Contact and Deal | Done | ✅ Yes | Plus **LeadId**, **Participants**; UpdatedAtUtc/UpdatedByUserId. |
| Task: Lead and Deal | Partial | ✅ Yes | Plus **ContactId**, **AssigneeId**; UpdatedAtUtc/UpdatedByUserId. |
| Contact delete | Missing | ✅ Yes | `DeleteAsync` in repository and service; `DELETE` on ContactsController; frontend delete + confirm. |
| Company delete | Missing | ✅ Yes | `DeleteAsync` in repository and service; `DELETE` on CompaniesController; frontend delete + confirm. |

---

## §3 — Lead → Convert Lead → Deal flow

| Item | Report status | Implemented? | Notes |
|------|----------------|--------------|--------|
| Convert → create Contact | Done | ✅ Yes | Unchanged. |
| Convert → create Deal | Done | ✅ Yes | Plus PipelineId, DealStageId. |
| Convert → create Company | Missing | ✅ Yes | **API:** `CreateNewCompany`, `NewCompanyName` in `ConvertLeadRequest`; LeadService creates Company when requested. |
| Convert → attach to existing Company | Missing | ✅ Yes | **API:** `ExistingCompanyId` in request; used for contact/deal company. |
| Convert → attach to existing Contact | Missing | ✅ Yes | **API:** `ExistingContactId`; link deal to existing contact. |
| Convert → attach to existing Deal | Missing | ✅ Yes | **API:** `ExistingDealId`; attach contact to existing deal (updates deal.ContactId). |
| Lead status Converted / archived | Missing | ✅ Yes | Lead **IsConverted**, **ConvertedAtUtc** set after convert; **read-only** in API (Update returns null if IsConverted). |
| **Convert UI: create/select Company, Contact, Deal** | — | ✅ Yes | Convert dialog exposes: create new company (+ name), use existing company; create new contact, use existing contact; create new deal (name, value, pipeline/stage), attach to existing deal. |

---

## §4 — Deal stages & pipeline design

| Item | Report status | Implemented? | Notes |
|------|----------------|--------------|--------|
| Deal has a stage | Done | ✅ Yes | Plus DealStageId, PipelineId. |
| Pipeline as entity | Missing | ✅ Yes | Pipeline entity + APIs. |
| Deal Stage as entity | Missing | ✅ Yes | DealStage entity + APIs; IsWon, IsLost per stage. |
| Org-level pipeline/stages | Missing | ✅ Yes | Pipelines and DealStages are per-organization; CRUD restricted to Owner/Manager. |
| Stage-specific required fields | Missing | ❌ No | No required-fields config per stage. |
| **Pipeline page uses API stages** | — | ✅ Yes | Pipeline page loads pipelines via `getPipelines()`; uses org deal stages for kanban columns and deal create/edit; fallback to fixed stages when no pipelines. Deal form has Pipeline, Stage, Currency. |

---

## §5 — Permissions: safe defaults

| Item | Report status | Implemented? | Notes |
|------|----------------|--------------|--------|
| Auth required for CRM APIs | Done | ✅ Yes | Controllers use `[Authorize]`. |
| Org-scoped data | Done | ✅ Yes | Filtering by UserId + CurrentOrganizationId. |
| Role-based permission checks | Missing | ✅ Yes | **Pipelines, DealStages, LeadStatuses, LeadSources** create/update/delete require Owner or Manager (`GetMemberRoleAsync` + `RequireOrgOwnerOrManager`). CRM record CRUD remains any-org-member (no "Salesperson can only edit own records"). |
| Restrict Pipeline/Stages/Lead Status-Source edits | N/A | ✅ Yes | Implemented; only Owner/Manager. |
| Restrict org settings / user management | Done (invite/join) | ✅ Yes | Unchanged; invite/join owner-only. |
| "Owned" records only | Missing | ⚠️ Partial | **AssigneeId** on Deal and Task; no "edit only my records" rule or "see all org deals" for Manager. |

---

## §6 — Minimum fields per object

| Object | Report gap | Implemented? | Notes |
|--------|------------|--------------|--------|
| Company | domain, industry, size | ✅ Yes | Domain, Industry, Size on entity; DTOs and API. |
| Contact | owner (explicit) | ⚠️ Partial | UserId (creator) only; no separate OwnerId. JobTitle added. |
| Lead | sourceId, statusId | ✅ Yes | LeadSourceId, LeadStatusId (optional); legacy Source/Status strings kept. |
| Deal | currency, pipelineId, stageId | ✅ Yes | Currency, PipelineId, DealStageId; AssigneeId added. |
| Task | assigneeId, ContactId | ✅ Yes | AssigneeId, ContactId on TaskItem. |
| Activity | LeadId, participants | ✅ Yes | LeadId, Participants on Activity. |

---

## §7 — MVP order

| # | Blueprint step | Implemented? | Notes |
|---|----------------|--------------|--------|
| 1 | Organization + users/roles | ✅ Yes | Orgs, members, invites, join; Owner/Member/Manager; permission enforcement on config APIs. |
| 2 | Companies + Contacts + Associations | ✅ Yes | Delete added; Domain, Industry, Size, JobTitle. |
| 3 | Leads + Lead status/source | ✅ Yes | LeadStatus/LeadSource entities and APIs; Lead has LeadStatusId/LeadSourceId. |
| 4 | Convert Lead → (Company/Contact/Deal) | ✅ Yes | Full convert API and **UI**: create/select company, contact, deal in convert dialog. |
| 5 | Deals + Pipelines + Stages | ✅ Yes | Pipeline + DealStage entities and APIs; Deal linked; **Pipeline page** loads stages from API; deal form has pipeline, stage, currency. |
| 6 | Activities + Tasks (Lead, Contact, assignee) | ✅ Yes | Activity.LeadId, Task.ContactId, Task.AssigneeId, Activity.Participants. |

---

## Deep audit — Contact/Company delete

| Area | Report finding | Implemented? |
|------|----------------|-------------|
| Contact / Company delete | Not implemented | ✅ Yes — Backend and frontend both have delete. |

---

## Missing “glue” — what is implemented vs not

| Need | Implemented? | Notes |
|------|--------------|--------|
| Role (Owner / Admin / Sales / Manager) | ✅ Yes | Owner, Member, Manager; config APIs restricted. |
| Record ownership (AssigneeId) | ✅ Yes | Deal.AssigneeId, Task.AssigneeId. No OwnerId on Company/Contact/Lead. |
| UpdatedAt / UpdatedBy on records | ✅ Yes | Company, Contact, Lead, Deal, Activity, Task all have UpdatedAtUtc, UpdatedByUserId. |
| Activity → Lead; Task → Contact; Task assignee | ✅ Yes | Implemented. |
| Products / Line items | ❌ No | Not implemented (Phase 2). |
| Custom fields / Tags | ❌ No | Not implemented. |
| Global search | ❌ No | Not implemented. |
| Saved filters / views | ❌ No | Not implemented. |
| Duplicate detection + merge | ❌ No | Not implemented. |
| Pipeline value by stage (reporting) | ✅ Yes | ReportingService has `GetPipelineValueByStageAsync`; `GET /api/reporting/pipeline-by-stage`; Dashboard shows "Pipeline value by stage" table. |
| Deals by salesperson / activity per rep | ✅ Yes | **Pipeline value by assignee:** `GetPipelineValueByAssigneeAsync`; `GET /api/reporting/pipeline-by-assignee`; Dashboard shows "Pipeline value by assignee" table. |
| Global search | ✅ Yes | `GET /api/search?q=`; GlobalSearchService; header search box with dropdown (leads, contacts, companies, deals). |
| Soft delete | ❌ No | Hard delete only. |
| Workflows / automation | ❌ No | Not implemented. |
| Stage-specific required fields | ❌ No | Not implemented. |

---

## API, database, and frontend implementation matrix

This section confirms that each **API** has a **database** backing and that **frontend** uses it where expected. All CRM and org-related APIs are listed; auth/copy/templates/settings are summarized.

### Database (entities and DbSets)

| Entity | DbSet in AppDbContext | Used by |
|--------|------------------------|--------|
| User | Users | Auth, org members |
| UserSettings | UserSettings | Settings API |
| Organization | Organizations | OrganizationsController |
| OrganizationMember | OrganizationMembers | Org members, role, assignee |
| OrgSettings | OrgSettings | (reserved) |
| Invite | Invites | InvitesController |
| JoinRequest | JoinRequests | JoinRequestsController |
| Pipeline | Pipelines | PipelinesController |
| DealStage | DealStages | DealStagesController, Deal |
| LeadStatus | LeadStatuses | LeadStatusesController, Lead |
| LeadSource | LeadSources | LeadSourcesController, Lead |
| Company | Companies | CompaniesController |
| Contact | Contacts | ContactsController |
| Deal | Deals | DealsController |
| Lead | Leads | LeadsController |
| TaskItem | TaskItems | TasksController |
| Activity | Activities | ActivitiesController |
| Template | Templates | TemplatesController |
| CopyHistoryItem | CopyHistoryItems | CopyHistoryController |

All entities above are in `ACI.Infrastructure.Persistence.AppDbContext` and have EF configurations in `Persistence/Configurations/`. Migrations are applied on startup.

### Backend API → Database → Frontend

| Backend controller | Endpoint (method) | Database / service | Frontend module | Frontend page / usage |
|--------------------|--------------------|--------------------|----------------|----------------------|
| **Organizations** | GET /api/organizations | OrganizationRepository | organizations.ts | OrgContext, Settings, Organizations |
| | GET /api/organizations/{id} | OrganizationRepository | organizations.ts | Organizations, switch org |
| | POST /api/organizations | OrganizationService | organizations.ts | Organizations (create) |
| | GET /api/organizations/{id}/members | OrganizationRepository (members) | organizations.ts | Pipeline (assignee), Tasks (assignee), Settings (members list) |
| | PUT .../members/{userId}/role | OrganizationService | organizations.ts | Settings (role change) |
| | DELETE .../members/{userId} | OrganizationService | organizations.ts | Settings (remove member) |
| **Invites** | GET pending, POST accept, GET org, POST create | InviteRepository/Service | organizations.ts | Settings, invite flow |
| **JoinRequests** | POST create, GET org, POST accept/reject | JoinRequestRepository/Service | organizations.ts | Settings, Organizations |
| **Companies** | GET, GET {id}, POST, PUT {id}, DELETE {id} | CompanyRepository/Service | companies.ts | Companies, Leads (convert), Pipeline (deal company) |
| **Contacts** | GET, GET search, GET {id}, POST, PUT {id}, DELETE {id} | ContactRepository/Service | contacts.ts | Contacts, Leads (convert), Pipeline (deal contact), Activities |
| **Leads** | GET, GET search, GET {id}, POST, PUT {id}, DELETE {id}, POST {id}/convert | LeadRepository/Service | leads.ts | Leads (list, create, edit, delete, convert) |
| **Deals** | GET, GET search, GET {id}, POST, PUT {id}, DELETE {id} | DealRepository/Service | deals.ts | Pipeline, Leads (convert), SendToCrm |
| **Pipelines** | GET, GET {id}, POST, PUT {id}, DELETE {id} | PipelineRepository/Service | pipelines.ts | Pipeline (kanban, deal form), Leads (convert) |
| **DealStages** | GET ?pipelineId=, GET {id}, POST, PUT {id}, DELETE {id} | DealStageRepository/Service | (stages via pipelines) | Pipeline page gets stages from pipeline payload; no separate dealStages.ts. Create/update/delete stages: **API only** (Owner/Manager), no admin UI. |
| **LeadStatuses** | GET, GET {id}, POST, PUT {id}, DELETE {id} | LeadStatusRepository/Service | leadStatuses.ts | Leads (create/edit dropdown). Create/update/delete: **API only**, no admin UI. |
| **LeadSources** | GET, GET {id}, POST, PUT {id}, DELETE {id} | LeadSourceRepository/Service | leadSources.ts | Leads (create/edit dropdown). Create/update/delete: **API only**, no admin UI. |
| **Activities** | GET, GET contact/{id}, GET deal/{id}, POST, DELETE {id} | ActivityRepository/Service | activities.ts | Activities, Contacts (by contact), Deals (by deal) |
| **Tasks** | GET, GET {id}, POST, PUT {id}, DELETE {id} | TaskRepository/Service | tasks.ts | Tasks |
| **Reporting** | GET dashboard, GET pipeline-by-stage, GET pipeline-by-assignee | ReportingService (Deal/Lead repos) | reporting.ts | Dashboard (stats + pipeline-by-stage + pipeline-by-assignee tables) |
| **Search** | GET ?q= (global) | GlobalSearchService (Lead/Contact/Company/Deal) | search.ts | AppHeader search box (dropdown results) |
| **CopyHistory** | GET, GET stats | CopyHistoryRepository/Service | copyHistory.ts | Dashboard, History |
| **Copy** | POST generate, POST send | CopyGenerator, SendToCrmService | copyGenerator.ts, crm.ts | Dashboard (generate), SendToCrm, History |
| **Templates** | GET, GET {id} | TemplateRepository | templates.ts | Dashboard, Templates |
| **Settings** | GET, PUT | UserSettings (in-memory or stored) | settings.ts | Settings |
| **Auth** | POST login, login/2fa, register, GET me, 2fa setup/enable/disable | Auth, User | auth.ts, authApi.ts | Login, Register, Settings (2FA) |

### Summary

- **API:** Every controller and endpoint listed above exists in `ACI.WebApi/Controllers/` and is registered (no orphan routes).
- **Database:** Every CRM and org entity has a corresponding DbSet and repository; services use repositories; no controller talks directly to DbContext without a service.
- **Frontend:** All **read** and **write** operations used by the main user flows (companies, contacts, leads, deals, pipeline, tasks, activities, org members, reporting, copy, templates, settings, auth) are implemented in `src/app/api/*.ts` and used by the corresponding pages. **Pipeline/DealStage/LeadStatus/LeadSource** create/update/delete are backend-only (Owner/Manager); frontend uses GET only for dropdowns and kanban.

---

## Frontend gaps (API exists but UI does not use it)

| Feature | API / backend | Frontend |
|---------|----------------|----------|
| Pipelines & stages | ✅ GET/POST/PUT/DELETE pipelines and deal stages | ✅ **Done.** Pipeline page loads `getPipelines()`; kanban and create/edit use org deal stages; fallback to fixed stages when no pipelines. |
| Lead statuses & sources | ✅ GET (and CRUD for owner/manager) lead statuses/sources | ✅ **Done.** Leads page loads `getLeadStatuses()` / `getLeadSources()`; create/edit use API options with fallback. |
| Convert: create company / existing company, contact, deal | ✅ ConvertLeadRequest supports CreateNewCompany, ExistingCompanyId, ExistingContactId, ExistingDealId | ✅ **Done.** Convert dialog: create new company (+ name), use existing company; create contact / use existing contact; create deal (name, value, pipeline/stage) / attach to existing deal. |
| Company fields (domain, industry, size) | ✅ DTOs and API accept them | ✅ **Done.** Companies page form has Name, Domain, Industry, Size (create and edit). |
| Deal currency / pipeline / stage / assignee | ✅ DTOs and API | ✅ **Done.** Create and edit deal forms have Pipeline, Stage, Currency, Assignee (org members dropdown). Tasks page has Assignee dropdown. |
| Contact job title | ✅ API | ✅ **Done.** Contacts form has Job title field (create and edit). |
| Org members list (role change, remove) | ✅ GET members, PUT role, DELETE member | ✅ **Done.** Settings → Organization: "Organization members" table; owners can change role (Member/Manager) and remove members (with confirm). |
| Company delete | ✅ DELETE API | ✅ **Done.** Companies page: Delete button + AlertDialog confirmation; calls `deleteCompany()`. |
| Contact delete | ✅ DELETE API | ✅ **Done.** Contacts page: Delete button + AlertDialog confirmation; calls `deleteContact()`. |
| Pipeline / DealStage / LeadStatus / LeadSource **admin** (create, edit, delete) | ✅ POST/PUT/DELETE on all four | ⚠️ **API only.** Frontend uses GET only (dropdowns, kanban). No org settings page for managing pipelines, stages, lead statuses, or sources. Owner/Manager can use API directly. |

---

## Detailed frontend verification (code-level)

Each item from the gap report that has a **frontend** aspect was checked in the codebase. Evidence below.

### 1. Pipeline page — API stages and deal form

| Check | Location | Evidence |
|-------|----------|----------|
| Load pipelines on mount | `src/app/pages/Pipeline.tsx` | `Promise.all([getDeals(), getContacts(), getPipelines()])` in `useEffect`; `setPipelines(pipelinesData ?? [])`. |
| Derive stages from pipeline | Same | `getStageList(pipelines, selectedPipelineId)` returns org deal stages or `FALLBACK_STAGES`; `groupDealsByStage(deals, stageList)` groups by `dealStageId` or `stage`. |
| Kanban columns from API | Same | `dealsByStage` from `groupDealsByStage`; columns render `stageId`, `stageName`; drag/drop and dropdown send `dealStageId` + `stage` when GUID. |
| Create deal: Pipeline, Stage, Currency | Same | `createForm` has `pipelineId`, `stageId`, `currency`; form has Pipeline dropdown (when `pipelines.length > 0`), Stage dropdown from `createStageList`, Currency input; `createDeal()` receives `pipelineId`, `dealStageId`, `currency`, `stage`. |
| Edit deal: Pipeline, Stage, Currency | Same | `editForm` has `pipelineId`, `stageId`, `currency`; dialog has Pipeline, Stage, Currency; `updateDeal()` receives same. |
| Move deal (stage change) | Same | `handleMoveStage(dealId, newStageId, newStageName)`; payload includes `dealStageId` when GUID and `stage` name; `isWon` set for Closed Won/Lost. |

### 2. Leads page — Lead statuses/sources and convert dialog

| Check | Location | Evidence |
|-------|----------|----------|
| Load lead statuses and sources | `src/app/pages/Leads.tsx` | `Promise.all([getLeads(), getCompanies(), getLeadStatuses(), getLeadSources()])`; `setLeadStatuses`, `setLeadSources`. |
| Status/Source dropdowns from API | Same | `statusOptions` = `leadStatuses.length > 0 ? leadStatuses : FALLBACK_STATUSES.map(...)`; `sourceOptions` same; form Source/Status `<Select>` use `sourceOptions`/`statusOptions`; form stores `leadSourceId`, `leadStatusId` and sends in `createLead`/`updateLead`. |
| Convert: Company section | Same | "Create new company" checkbox + `newCompanyName` input; "Use existing company" dropdown (`existingCompanyId`) when `!createNewCompany && companies.length > 0`. |
| Convert: Contact section | Same | "Create new contact" checkbox; "Use existing contact" dropdown (`existingContactId`) when `!createContact && convertOptions.contacts.length > 0`. |
| Convert: Deal section | Same | "Create new deal" checkbox; "Attach to existing deal" dropdown (`existingDealId`) when `!createDeal && convertOptions.deals.length > 0`. New deal: deal name, value, Pipeline/Stage dropdowns from `convertOptions.pipelines` and `convertStageList`. |
| Convert options loaded on open | Same | `openConvert(lead)` calls `Promise.all([getContacts(), getDeals(), getPipelines()])` and `setConvertOptions(...)`. |
| Convert validation | Same | `!convertForm.createContact && !convertForm.createDeal && !convertForm.existingDealId` → toast error; then `convertLead(convertDialogLead.id, convertForm)`. |

### 3. Companies page — Domain, Industry, Size

| Check | Location | Evidence |
|-------|----------|----------|
| Form state | `src/app/pages/Companies.tsx` | `form` = `{ name, domain, industry, size }`; `openEdit` sets `domain`, `industry`, `size` from company. |
| Form inputs | Same | Labels/inputs for Domain (`company-domain`), Industry (`company-industry`), Size (`company-size`). |
| Create/update payload | Same | `createCompany({ name, domain: form.domain.trim() \|\| undefined, ... })`; `updateCompany(id, { name, domain, industry, size })`. |

### 4. Contacts page — Job title and delete

| Check | Location | Evidence |
|-------|----------|----------|
| Form state and field | `src/app/pages/Contacts.tsx` | `form` includes `jobTitle`; "Job title" input `id="contact-jobtitle"`; `openEdit` sets `jobTitle: contact.jobTitle ?? ''`. |
| Create/update payload | Same | `createContact(..., jobTitle: form.jobTitle.trim() \|\| undefined)`; `updateContact(..., jobTitle: ...)`. |
| Delete contact | Same | Delete button; `deleteConfirmContact` state; `AlertDialog`; `handleDeleteConfirm` calls `deleteContact(deleteConfirmContact.id)`. |

### 5. Company delete

| Check | Location | Evidence |
|-------|----------|----------|
| Delete company | `src/app/pages/Companies.tsx` | `deleteCompany` from API; `deleteConfirmCompany` state; `AlertDialog` "Delete company?"; `handleDeleteConfirm` calls `deleteCompany(deleteConfirmCompany.id)`. |

### 6. Deal and Task assignee + org members

| Check | Location | Evidence |
|-------|----------|----------|
| Org members API | `src/app/api/organizations.ts` | `getOrgMembers(organizationId)`, `OrgMemberDto` (userId, name, email, role); `updateMemberRole`, `removeMember`. |
| Assignee on Pipeline (deals) | `src/app/pages/Pipeline.tsx` | Fetches `getOrgMembers(currentOrgId)`; create/edit deal forms include Assignee dropdown; payload sends `assigneeId`. |
| Assignee on Tasks | `src/app/pages/Tasks.tsx` | Fetches org members; create/edit task forms include Assignee dropdown; payload sends `assigneeId`. |
| Org members in Settings | `src/app/pages/Settings.tsx` | "Organization members" table (name, email, role); owners can change role (Member/Manager) via dropdown; Remove button with confirm modal (disabled for owner and self). |

### 7. §7 MVP order — frontend alignment

| MVP step | Frontend | Evidence |
|----------|----------|----------|
| 4 — Convert Lead → (Company/Contact/Deal) | ✅ | Full convert dialog with company/contact/deal create or select (see §2 above). |
| 5 — Deals + Pipelines + Stages | ✅ | Pipeline page uses API pipelines/stages; deal form has pipeline, stage, currency (see §1 above). |

---

## Summary

**Verdict: The majority of the blueprint is implemented** (backend and frontend). Core CRM flows—organizations, roles, companies, contacts, leads, convert, deals, pipeline/stages, activities, tasks, assignee, org member management (role change, remove), reporting (dashboard + pipeline by stage)—are in place.

- **Blueprint §1–§6 and §7 (MVP order):** Effectively all items are **implemented** (backend + frontend where applicable). Manager role, Pipeline/DealStage/LeadStatus/LeadSource entities and APIs, role-based restriction on config APIs, full convert flow (API), Contact/Company delete, and all requested fields (domain, industry, size, currency, assignee, LeadId, ContactId on task, participants, JobTitle, UpdatedAt/UpdatedBy) are in place.
- **Still missing (backend):** Stage-specific required fields; optional “owned records only” / “see all org” for Manager.
- **Frontend:** Pipeline page **uses** API pipelines and stages and **assignee** (org members); Leads page **uses** API lead statuses/sources; Convert dialog **exposes** create/select company, contact, and deal; Company form **has** domain, industry, size; Deal form **has** currency, pipeline, stage, assignee; Tasks form **has** assignee; Contact form **has** job title; Settings **has** organization members list with role change and remove (owner only). Delete for Company and Contact is in the UI. Dashboard **shows** pipeline value by stage.
- **Glue (report):** Products/line items, custom fields, saved views, duplicate/merge, soft delete, workflows are **not** implemented (Phase 2). **Global search**, **pipeline value by stage**, and **pipeline value by assignee** **are** implemented.

**Frontend wiring completed (Feb 2025):** Pipeline page uses API stages and assignee (org members); Tasks page uses assignee; Leads page uses API lead statuses/sources; Convert dialog exposes full company/contact/deal options; Company form has domain/industry/size; Contact form has job title; Deal create/edit have pipeline, stage, currency, assignee; Settings has org members list with role change and remove; Dashboard has pipeline value by stage table.

---

## Bugs fixed and side-effect checks

These issues were identified and fixed so the implementation stays safe and consistent.

### Fixed

| Issue | Risk | Fix |
|-------|------|-----|
| **Company delete with related records** | Deleting a company that has Contacts, Deals, or Leads would hit FK constraint and throw. | Before delete, null out `CompanyId` on all Contacts, Deals, and Leads that reference the company (then delete the company). |
| **Contact delete with related records** | Deleting a contact that has Deals or Activities would hit FK constraint and throw. | Before delete, null out `ContactId` on all Deals and Activities that reference the contact (then delete the contact). |
| **Convert with invalid ExistingCompanyId / ExistingContactId** | Passing a non-existent or out-of-scope company/contact id could cause FK errors or wrong data. | In `LeadService.ConvertAsync`, validate `ExistingCompanyId` and `ExistingContactId` with `GetByIdAsync` (same user + org); if not found, return `null` (conversion fails cleanly). |

### Side effects and intentional behavior

| Area | Behavior | Notes |
|------|----------|--------|
| **Convert “attach to existing deal”** | Only deals **created by the current user** can be updated (deal’s `ContactId`). | `DealRepository.GetByIdAsync` filters by `userId`; no “attach to any org deal” to avoid cross-user writes without a broader permission model. |
| **Lead Update: clear LeadSourceId / LeadStatusId** | API cannot clear these once set. | `UpdateLeadRequest` uses `Guid?`; “not sent” and “send null” both map to null in JSON. Backend only sets when `request.LeadSourceId.HasValue` / `request.LeadStatusId.HasValue`, so clearing would require a separate convention (e.g. explicit “clear” flag) if needed later. |
| **Migration** | New columns are nullable or have defaults. | Existing rows get `NULL` or default (e.g. `IsConverted = false`); no data backfill required. |
| **Backward compatibility** | Old API clients that don’t send new fields. | New request fields use optional/default parameters; old payloads still work. `ConvertLeadResult` now has `companyId`; clients that only use `contactId`/`dealId` are unaffected. |
