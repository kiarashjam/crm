# Update 05-BLUEPRINT-GAP-REPORT.md: fix Cadence today tables and Rough distance line
import re

path = '05-BLUEPRINT-GAP-REPORT.md'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Fix §1 table rows (Manager, Salesperson, Pipeline)
content = content.replace(
    '| Salesperson (default) role | **Partial** | Only `Owner` and `Member` exist (`OrgMemberRole`). "Member" acts like salesperson but not named so. |',
    '| Salesperson (default) role | **Done** | `OrgMemberRole.Member` (documented as Salesperson); CRUD on records. |'
)
content = content.replace(
    '| Manager role | **Missing** | No Manager role or "see/assign all deals/leads" or "edit pipelines." |',
    '| Manager role | **Done** | `OrgMemberRole.Manager`; PipelinesController, DealStagesController, LeadSourcesController, LeadStatusesController use `RequireOrgOwnerOrManager` — only Owner or Manager can create/update/delete pipelines, stages, lead sources, lead statuses. |'
)
content = content.replace(
    '| Owner: full control + billing | **Partial** | No billing in app; owner has invite/join-request only (no org settings UI for pipeline/stages). |',
    '| Owner: full control + billing | **Partial** | No billing in app; owner has invite/join and is required for org config. |'
)
content = content.replace(
    '| Owner: manage users/roles/settings | **Partial** | Invite + join requests; no role change UI, no org-level pipeline/stage/settings management. |',
    '| Owner: manage users/roles/settings | **Partial** | Invite + join requests ✓; pipeline/stage/lead-source/lead-status config restricted to Owner/Manager ✓; no role-change UI, no org branding/timezone/currency. |'
)
content = content.replace(
    '| Salesperson cannot change Pipeline/stages/org | **Missing** | No permission checks; any authenticated org member can call any API (create/edit/delete companies, deals, etc.). |',
    '| Salesperson cannot change Pipeline/stages/org | **Done** | Pipeline, DealStage, LeadSource, LeadStatus create/update/delete require Owner or Manager; Members get 403. |'
)

# Fix §1 Gap summary
old_gap1 = """### Gap summary (§1)

- Add **Salesperson** (or rename Member) and optional **Manager** if you want parity.
- Implement **role-based permissions**: e.g. Salesperson cannot edit Pipeline/Deal Stages/Lead Status-Source definitions or org settings; only Owner (and optionally Manager) can.
- Optionally: billing and org-level "admin" settings (users, roles)."""
new_gap1 = """### Gap summary (§1)

- **Manager** and **Pipeline/Stage/LeadSource/LeadStatus** permission enforcement are in place. Remaining: role-change UI (assign Manager/Salesperson), remove/disable user, transfer ownership, org branding/timezone/currency, and optional billing."""
content = content.replace(old_gap1, new_gap1)

# Fix §2 table - Pipeline through Company delete
old_s2 = """| Pipeline (entity) | **Missing** | No `Pipeline` table; deals use free-text `Deal.Stage`. Frontend uses fixed `STAGES` array. |
| Deal Stages (entity) | **Missing** | No `DealStage` table; stages are hardcoded in UI (`Pipeline.tsx`: Qualification, Proposal, Negotiation, Closed Won, Closed Lost). |
| Lead Status (org-level) | **Missing** | Fixed list in frontend (`Leads.tsx`: New, Contacted, Qualified, Lost); not configurable per org. |
| Lead Source (org-level) | **Missing** | Fixed list in frontend (website, referral, ads, events, manual); not configurable per org. |
| Company | **Done** | Company → Contacts, Deals, Leads. |
| Contact | **Done** | Contact → Company (optional); Contact → Activities. |
| Lead | **Done** | Lead → Company (optional); Lead → TaskItems; Status/Source as strings. |
| Deal | **Done** | Deal → Company, Contact (optional); Deal → Activities, TaskItems; Stage as string. |
| Activity | **Partial** | Contact and Deal only; no **Lead** link. Blueprint: "Lead OR Contact/Company/Deal." |
| Task | **Partial** | Lead and Deal only; no **Contact** link. No **assignee** (only creator/owner via `UserId`). |
| Contact delete | **Missing** | No `Delete` action on `ContactsController`; no `DeleteAsync` in `ContactRepository`. Frontend has no delete contact. |
| Company delete | **Missing** | No `Delete` action on `CompaniesController`; no `DeleteAsync` in `CompanyRepository`. Frontend has no delete company. |"""
new_s2 = """| Pipeline (entity) | **Done** | `Pipeline` entity (OrganizationId, Name, DisplayOrder); PipelinesController; Deal references `PipelineId`. |
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
| Company delete | **Done** | CompaniesController has Delete; CompanyRepository/Service DeleteAsync. |"""
content = content.replace(old_s2, new_s2)

# Fix §3 table
old_s3 = """| Convert → create Contact | **Done** | `LeadService.ConvertAsync` creates Contact from lead name/email/phone/company. |
| Convert → create Deal | **Done** | Creates Deal with optional name/value/stage; uses lead's CompanyId and new ContactId. |
| Convert → create Company | **Missing** | Conversion does not create a new Company; it only uses `lead.CompanyId` for the new Contact/Deal. |
| Convert → attach to **existing** Company | **Missing** | No UI or API to pick existing Company; only current lead company is used. |
| Convert → attach to **existing** Contact | **Missing** | No option to attach deal to an existing contact. |
| Convert → attach to **existing** Deal | **Missing** | No option to attach to existing deal. |
| Lead status "Converted" / archived | **Missing** | Lead is not updated after convert (no status change, no read-only/archived flag). |"""
new_s3 = """| Convert → create Contact | **Done** | `LeadService.ConvertAsync` creates Contact from lead; optional `ExistingContactId` to use existing. |
| Convert → create Deal | **Done** | Creates Deal with PipelineId/DealStageId, name/value; optional `ExistingDealId` to attach to existing deal (updates ContactId). |
| Convert → create Company | **Done** | `CreateNewCompany` + `NewCompanyName` in ConvertLeadRequest; creates Company and uses it for Contact/Deal. |
| Convert → attach to **existing** Company | **Done** | `ExistingCompanyId` in ConvertLeadRequest; uses that company for Contact/Deal. |
| Convert → attach to **existing** Contact | **Done** | `ExistingContactId`; new deal links to that contact. |
| Convert → attach to **existing** Deal | **Done** | `ExistingDealId`; links created contact to that deal (updates deal's ContactId). |
| Lead status "Converted" / archived | **Done** | Lead set `IsConverted = true`, `ConvertedAtUtc`; `UpdateAsync` returns null for converted leads (read-only). |"""
content = content.replace(old_s3, new_s3)

# Fix §3 Gap summary
old_gap3 = """### Gap summary (§3)

- Extend **Convert** API/UI: allow "create new Company" or "select existing Company"; "create new Contact" or "select existing Contact"; "create new Deal" or "attach to existing Deal."
- After successful convert: set Lead to **Converted** (e.g. status or `IsConverted` flag) and treat as read-only / archived in UI and API."""
new_gap3 = """### Gap summary (§3)

- Full convert flow and Converted state are implemented. UI should expose create vs. select existing for Company/Contact/Deal where not already present."""
content = content.replace(old_gap3, new_gap3)

# Fix §4 table
old_s4 = """| Deal has a stage | **Done** | `Deal.Stage` (string); UI uses fixed list. |
| Pipeline as entity | **Missing** | No Pipeline table; no "has many Deal Stages." |
| Deal Stage as entity | **Missing** | No DealStage table; stages are hardcoded in frontend. |
| Org-level pipeline/stages | **Missing** | Same stages for all orgs; no admin UI to add/rename/reorder stages. |
| Stage-specific required fields | **Missing** | No configuration per stage. |
| Standard stage flow | **Partial** | UI stages: Qualification, Proposal, Negotiation, Closed Won, Closed Lost — similar to blueprint but not configurable. |"""
new_s4 = """| Deal has a stage | **Done** | Deal has `DealStageId` and legacy `Stage` (string); DealStage has IsWon, IsLost. |
| Pipeline as entity | **Done** | `Pipeline` table (OrganizationId, Name, DisplayOrder); has many DealStages. |
| Deal Stage as entity | **Done** | `DealStage` table (PipelineId, Name, DisplayOrder, IsWon, IsLost). |
| Org-level pipeline/stages | **Done** | Pipelines and stages per organization; Owner/Manager can create/update/delete via API. |
| Stage-specific required fields | **Missing** | No configuration per stage for required fields. |
| Standard stage flow | **Done** | Admin-configurable; frontend can load stages from API. |"""
content = content.replace(old_s4, new_s4)

# Fix Rough distance line: remove stray "my data only" and simplify
old_rough = """**Rough distance:** About **78–82%** of the way to the **blueprint**. For a **usable, safe CRM** (with glue), the app is **~55–60%**: core data and UI exist, but roles/permissions, record ownership, audit, duplicate handling, custom fields, products/line items, and reporting depth are largely missing. Against the **CRM Sales System – Complete Functional Report** (15 sections), Cadence is **~60–65%** (Functional Report) and **~70–75%** (User Jobs guide) aligned. **Industry validation:** Cadence is a **multi-user sales CRM foundation**; it is **not yet a real multi-user sales CRM**. Remaining blockers: role/remove/transfer user, data visibility, reporting depth, search/views, duplicate/merge, soft delete (see **Remaining items (priority order)** below). "my data only\""""
new_rough = """**Rough distance:** About **78–82%** of the way to the **blueprint**. For a **usable, safe CRM** (with glue), the app is **~55–60%**. Against the **Complete Functional Report**, Cadence is **~60–65%**; against the **Complete User Jobs & End-to-End Usage Guide**, **~70–75%**. **Industry validation:** Cadence is a **multi-user sales CRM foundation**; remaining blockers: role/remove/transfer user, data visibility, reporting depth, search/views, duplicate/merge, soft delete (see **Remaining items (priority order)** and **Missing points checklist** below)."""
content = content.replace(old_rough, new_rough)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)
print('Done')
