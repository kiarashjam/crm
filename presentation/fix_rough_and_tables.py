# Fix line 781 (Rough distance) and update §1 §2 §3 §4 tables by line index
path = '05-BLUEPRINT-GAP-REPORT.md'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Line 781 is index 780. Replace with new Rough distance (no stray "my data only")
new_rough = "**Rough distance:** About **78–82%** of the way to the **blueprint**. For a **usable, safe CRM** (with glue), the app is **~55–60%**. Against the **Complete Functional Report**, Cadence is **~60–65%**; against the **Complete User Jobs & End-to-End Usage Guide**, **~70–75%**. **Industry validation:** Cadence is a **multi-user sales CRM foundation**; remaining blockers: role/remove/transfer user, data visibility, reporting depth, search/views, duplicate/merge, soft delete (see **Remaining items (priority order)** and **Missing points checklist** below).\n"
if len(lines) > 780 and 'Rough distance' in lines[780]:
    lines[780] = new_rough
    print('Fixed Rough distance line')
else:
    print('Rough distance line not found or wrong index')

# §1: line 49 = index 48 (Manager role), 52 = index 51 (Salesperson cannot change)
for i, line in enumerate(lines):
    if i == 48 and '| Manager role | **Missing**' in line:
        lines[i] = '| Manager role | **Done** | `OrgMemberRole.Manager`; PipelinesController, DealStagesController, LeadSourcesController, LeadStatusesController use `RequireOrgOwnerOrManager` — only Owner or Manager can create/update/delete pipelines, stages, lead sources, lead statuses. |\n'
        print('Fixed Manager role')
    if i == 51 and '| Salesperson cannot change Pipeline' in line:
        lines[i] = '| Salesperson cannot change Pipeline/stages/org | **Done** | Pipeline, DealStage, LeadSource, LeadStatus create/update/delete require Owner or Manager; Members get 403. |\n'
        print('Fixed Salesperson cannot change')
    if i == 47 and '| Salesperson (default) role | **Partial**' in line:
        lines[i] = '| Salesperson (default) role | **Done** | `OrgMemberRole.Member` (documented as Salesperson); CRUD on records. |\n'
        print('Fixed Salesperson role')
    if i == 49 and '| Owner: full control' in line:
        lines[i] = '| Owner: full control + billing | **Partial** | No billing in app; owner has invite/join and is required for org config. |\n'
        print('Fixed Owner billing')
    if i == 50 and '| Owner: manage users' in line:
        lines[i] = '| Owner: manage users/roles/settings | **Partial** | Invite + join requests ✓; pipeline/stage/lead-source/lead-status config restricted to Owner/Manager ✓; no role-change UI, no org branding/timezone/currency. |\n'
        print('Fixed Owner manage')

# §2: Pipeline, DealStage, etc. - lines 78-89 = indices 77-88
replacements_s2 = [
    (77, '| Pipeline (entity) | **Done** | `Pipeline` entity (OrganizationId, Name, DisplayOrder); PipelinesController; Deal references `PipelineId`. |\n'),
    (78, '| Deal Stages (entity) | **Done** | `DealStage` entity (PipelineId, Name, DisplayOrder, IsWon, IsLost); DealStagesController; Deal references `DealStageId`. |\n'),
    (79, '| Lead Status (org-level) | **Done** | `LeadStatus` entity per org; LeadStatusesController; Lead has `LeadStatusId`. |\n'),
    (80, '| Lead Source (org-level) | **Done** | `LeadSource` entity per org; LeadSourcesController; Lead has `LeadSourceId`. |\n'),
    (81, '| Company | **Done** | Company → Contacts, Deals, Leads; Domain, Industry, Size; UpdatedAtUtc, UpdatedByUserId; delete ✓. |\n'),
    (82, '| Contact | **Done** | Contact → Company (optional); Contact → Activities; JobTitle; UpdatedAtUtc, UpdatedByUserId; delete ✓. |\n'),
    (83, '| Lead | **Done** | Lead → Company (optional); LeadSourceId, LeadStatusId; IsConverted, ConvertedAtUtc; Lead → Activities; UpdatedAtUtc. |\n'),
    (84, '| Deal | **Done** | Deal → Company, Contact; PipelineId, DealStageId; Currency, AssigneeId; UpdatedAtUtc, UpdatedByUserId. |\n'),
    (85, '| Activity | **Done** | ContactId, DealId, **LeadId** ✓; **Participants** ✓; UpdatedAtUtc, UpdatedByUserId. |\n'),
    (86, '| Task | **Done** | LeadId, DealId, **ContactId** ✓; **AssigneeId** ✓; UpdatedAtUtc, UpdatedByUserId. |\n'),
    (87, '| Contact delete | **Done** | ContactsController has Delete; ContactRepository/Service DeleteAsync. |\n'),
    (88, '| Company delete | **Done** | CompaniesController has Delete; CompanyRepository/Service DeleteAsync. |\n'),
]
for idx, new_line in replacements_s2:
    if len(lines) > idx and ('| **Missing** |' in lines[idx] or '| **Partial** |' in lines[idx] or 'Company | **Done** | Company →' in lines[idx]):
        lines[idx] = new_line
        print(f'Fixed §2 line {idx+1}')

# §3: Convert table lines 108-114 = indices 107-113
replacements_s3 = [
    (106, '| Convert → create Contact | **Done** | `LeadService.ConvertAsync` creates Contact from lead; optional `ExistingContactId` to use existing. |\n'),
    (107, '| Convert → create Deal | **Done** | Creates Deal with PipelineId/DealStageId, name/value; optional `ExistingDealId` to attach to existing deal (updates ContactId). |\n'),
    (108, '| Convert → create Company | **Done** | `CreateNewCompany` + `NewCompanyName` in ConvertLeadRequest; creates Company and uses it for Contact/Deal. |\n'),
    (109, '| Convert → attach to **existing** Company | **Done** | `ExistingCompanyId` in ConvertLeadRequest; uses that company for Contact/Deal. |\n'),
    (110, '| Convert → attach to **existing** Contact | **Done** | `ExistingContactId`; new deal links to that contact. |\n'),
    (111, '| Convert → attach to **existing** Deal | **Done** | `ExistingDealId`; links created contact to that deal (updates deal\'s ContactId). |\n'),
    (112, '| Lead status "Converted" / archived | **Done** | Lead set `IsConverted = true`, `ConvertedAtUtc`; `UpdateAsync` returns null for converted leads (read-only). |\n'),
]
for idx, new_line in replacements_s3:
    if len(lines) > idx:
        lines[idx] = new_line
        print(f'Fixed §3 line {idx+1}')

# §4: lines 134-139 = indices 133-138
replacements_s4 = [
    (133, '| Deal has a stage | **Done** | Deal has `DealStageId` and legacy `Stage` (string); DealStage has IsWon, IsLost. |\n'),
    (134, '| Pipeline as entity | **Done** | `Pipeline` table (OrganizationId, Name, DisplayOrder); has many DealStages. |\n'),
    (135, '| Deal Stage as entity | **Done** | `DealStage` table (PipelineId, Name, DisplayOrder, IsWon, IsLost). |\n'),
    (136, '| Org-level pipeline/stages | **Done** | Pipelines and stages per organization; Owner/Manager can create/update/delete via API. |\n'),
    (137, '| Stage-specific required fields | **Missing** | No configuration per stage for required fields. |\n'),
    (138, '| Standard stage flow | **Done** | Admin-configurable; frontend can load stages from API. |\n'),
]
for idx, new_line in replacements_s4:
    if len(lines) > idx:
        lines[idx] = new_line
        print(f'Fixed §4 line {idx+1}')

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(lines)
print('All updates written')
