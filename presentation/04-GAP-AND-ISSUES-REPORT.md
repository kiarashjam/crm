# Gap and Issues Report — Doc vs Backend vs Frontend

**Cadence Cloud CRM** — Deep check against the target vision (presentation 01)

---

## At a glance

| | |
|---|--|
| **Purpose** | Compares what the docs say (report 01) with what the backend and frontend actually do. Highlights gaps, misalignments, and recommendations. |
| **Target vision** | Standalone **cloud CRM** — no external CRM; organizations (create/join, owner, invite). No Connection page; Cadence is the only CRM. |
| **Backend** | **Organization layer:** Organization, OrganizationMember, Invite, JoinRequest + APIs. OrganizationsController (list, create, get). InvitesController (create invite, list pending, accept by token/id). JoinRequestsController (create join request, list pending, accept/reject). Data entities have OrganizationId; backfill on create-org. **Data scope:** All CRM data APIs filter by UserId and OrganizationId when `X-Organization-Id` header is set. **No CrmConnection/Connection API** — removed per recommendation. Send to CRM = save in Cadence only. |
| **Frontend** | Login → **Organizations** (create/join/open org) → Onboarding → Dashboard. No `/connect` route; Connection page and connection API removed. Org flow: `/organizations` page, OrgProvider, RequireOrgLayout, org switcher in Settings and AppHeader. `organizations.ts` API module; `http.ts` and `apiClient.ts` send `X-Organization-Id`. Owner UI: invite by email, accept/reject join requests in Settings. Copy: Settings, Help, Privacy, SendToCrm, Homepage, Terms all Cadence-only. |
| **Status** | Backend and frontend aligned with doc 01: org layer, invite/join-request APIs, org flow, owner UI, X-Organization-Id, and Cadence-only copy. See §6 and Verification (§7). |

### Summary: What's missing & open issues

| Category | What's missing or open |
|----------|-------------------------|
| **Docs updated** | **Doc 01** (§1.1 and §6) and **PROJECT_ASPECTS** (§8) now state that the frontend implements the full org flow. **Backend README** updated: CrmConnections removed; Organizations, Pipelines, DealStages, LeadSources, LeadStatuses, and convert added. Optional copy tweaks: Login footer, Pipeline empty state (see Minor copy). |
| **Minor copy** | **Login** footer: "Optional: connect integrations after you sign in" — generic; could reword to "Set up your organization and brand in Settings." **Pipeline** empty state: "Create a deal or connect your CRM to see deals here" — "connect your CRM" implies external CRM; could reword to "Create a deal to see it here." Non-blocking. |
| **Critical bugs** | None identified in org infrastructure. Auth, org scoping, and route guards are consistent. |
| **Cross-system issues** | **10 cross-cutting issues** with full step-by-step implementation plans. **🔴 CRITICAL (fix immediately):** CS-1 Global Search dead code, CS-4 Dead form fields, CS-6 Mock team stats, CS-7 Activity type mismatch. **🟠 HIGH (must do):** CS-3 No detail pages, CS-5 Missing timestamps, CS-8 No notifications, CS-10 Performance. **🟡 MEDIUM:** CS-2 Dead React Query hooks, CS-9 No soft delete. |

---

## Contents

1. [Target vision (what the doc says)](#1-target-vision-what-the-doc-says)
2. [Backend — current state and gaps](#2-backend--current-state-and-gaps)
3. [Frontend — current state and gaps](#3-frontend--current-state-and-gaps)
4. [Bugs and issues in current code](#4-bugs-and-issues-in-current-code)
5. [Recommendations](#5-recommendations) (includes **Cross-System Issues** — CS-1 through CS-10)
6. [Implementation status](#6-implementation-status)
7. [Verification checklist](#7-verification-checklist-last-run)

---

## 1. Target vision (what the doc says)

From **01-HOW-THE-SYSTEM-WORKS-AND-SALES-USAGE.md**:

- **Standalone CRM only** — The system is **not** based on or connected to other CRMs. No copy from, import to, or sync with Salesforce, HubSpot, or any other CRM. Cadence is the only CRM; all data stays in Cadence and this system handles everything.
- **Organizations** — After login you **create an organization** or **join an organization** (request to join or accept an invite). When you **open an organization**, you go to that org’s CRM — you work as one salesperson with full access to that org’s data (leads, deals, contacts, tasks, activities, copy history).
- **Owner and membership** — Each org has one **owner**. The owner can **accept or reject** requests to join the org, and can **invite** other sales agents by their email address.
- **Data scope** — Data is stored **per organization**; only members of that org see its data. You can be a member of several organizations and switch by opening the org you want.
- **First-time flow** — Sign in → create or join org → open org → (if new org) set company name and brand tone → Dashboard for that org.
- **No external CRM** — No "Connect your CRM" to Salesforce/HubSpot; no Connection page; Settings show "Switch which org you're working in".

---

## 2. Backend — current state and gaps

### What the backend actually does

| Area | Current implementation |
|------|-------------------------|
| **Data scope** | CRM data (Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistoryItem) is scoped by **UserId** and **OrganizationId**. When `X-Organization-Id` header is set, APIs filter by that org; when null, by UserId and OrganizationId == null (legacy). UserSettings, Template remain user-scoped. (CrmConnection removed.) |
| **Entities** | `User`, `UserSettings`, **`Organization`, `OrganizationMember`, `Invite`, `JoinRequest`, `OrgSettings`**, `Company`, `Contact`, `Deal`, `Lead`, `TaskItem`, `Activity`, `Template`, `CopyHistoryItem`. Data entities have nullable `OrganizationId`; backfill on create-org. |
| **Auth** | JWT; login, register, 2FA (TOTP). `ICurrentUserService` exposes **`UserId` and `CurrentOrganizationId`** (from `X-Organization-Id` header). |
| **Org APIs** | `OrganizationsController`: GET list my orgs, POST create org (with backfill), GET by id. `InvitesController`: GET pending (my invites), POST accept (by token), POST {inviteId}/accept, GET organization/{orgId}, POST {orgId} (create invite). `JoinRequestsController`: POST {orgId} (create join request), GET organization/{orgId}, POST {id}/accept, POST {id}/reject. |
| **Send to CRM** | `SendToCrmService` → `CopyHistoryService.AddAsync` — saves to **CopyHistoryItem** in Cadence only; org-scoped when header set. |
| **Repositories / services** | Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistory repositories and services take `Guid? organizationId`; filter by UserId and (organizationId == null ? OrganizationId == null : OrganizationId == organizationId). ReportingService (dashboard stats) and CopyController (send) pass CurrentOrganizationId. |

### Gaps (doc vs backend)

| Doc says | Backend has | Gap |
|----------|-------------|-----|
| Data per **organization** | Data scoped by UserId + OrganizationId when header set | **Done.** Org entity, OrgId on entities, org-scoped queries and create. |
| Create / join **organization** | Create org API; list my orgs; get by id; InvitesController; JoinRequestsController | **Done.** Create, list, get orgs; invite by email (create, list pending, accept); join request (create, list pending, accept/reject). |
| **Owner** accepts/rejects requests, **invites** by email | InvitesController, JoinRequestsController | **Done.** Owner can create invite, list pending invites for org, accept invite (token or id). Owner can list pending join requests, accept/reject. |
| No external CRM; no Connection page | CrmConnection and Connection API **removed** | **Done.** Per recommendation: Connection controller, service, entity, migration (drop CrmConnections table) removed. |
| Settings: "Switch which org" | Org list API (GET my orgs) | **Done.** Frontend has org switcher in Settings and AppHeader; sends X-Organization-Id. |

### Backend — no critical bugs found

- Controllers use `[Authorize]` and check `UserId`; they return `Unauthorized()` when `userId` is null.
- Repositories consistently filter by `UserId`; no cross-user data leak in current design.
- `SendToCrmService` correctly writes to copy history only (no external call).

---

## 3. Frontend — current state and gaps

### What the frontend actually does

| Area | Current implementation |
|------|-------------------------|
| **Routes** | `/`, `/login`, `/organizations`, `/onboarding`, `/dashboard`, `/generated`, `/send`, `/leads`, `/deals`, `/tasks`, `/activities`, `/contacts`, `/companies`, `/templates`, `/history`, `/settings`, `/help`, `/privacy`, `/terms`. **No `/connect`** — Connection page removed. `/organizations` for create/join/open org. |
| **Auth** | Login/register; token in localStorage; `getCurrentUser()`, `getCurrentOrganizationId()`, `setCurrentOrganizationId()`. Org id stored in localStorage; cleared on logout. |
| **Onboarding** | Company name + brand tone → save user settings → navigate to dashboard. Create/join org step is on `/organizations` before or after onboarding. |
| **Post-login flow** | Login → **Organizations** (create org or accept invite, then open org) → (if new org) Onboarding → Dashboard. No Connection step. |
| **Settings** | "Organization" section: "Cadence is your only CRM", org **switcher** dropdown, link to "Manage organizations". For **owner**: invite by email, list pending invites, list join requests with Accept/Reject. No Connection link. |
| **Send to CRM** (`/send`) | "Save your generated copy to a contact or deal in Cadence". Saves via copy history API (org-scoped when X-Organization-Id set). |
| **AppHeader** | **Current org name** and "Switch org" link to `/organizations` (when real API). No Connection menu item. |
| **Data** | API calls send **X-Organization-Id** when `getCurrentOrganizationId()` is set (`http.ts` and `apiClient.ts`). Backend returns org-scoped data. |
| **API layer** | `src/app/api/organizations.ts`: listMyOrganizations, createOrganization, getOrganization, listMyPendingInvites, acceptInvite, acceptInviteById, createInvite, listPendingInvitesForOrg, createJoinRequest, listPendingJoinRequestsForOrg, acceptJoinRequest, rejectJoinRequest. `http.ts` and `apiClient.ts` add `X-Organization-Id` header. |
| **Homepage** (`/`) | Updated for Cadence-only: "Create or join an organization", "Cadence is your CRM", "save it in Cadence", no "Connect your CRM". Aligned with doc 01. |
| **Terms** (`/terms`) | Service Description: "Cadence is your CRM and provides AI-powered copywriting tools built in. All data stays in Cadence; no external CRM integration is required." Aligned with doc 01. |

### Gaps (doc vs frontend)

| Doc says | Frontend has | Gap |
|----------|--------------|-----|
| After login: **create or join organization** | `/organizations` page: create org, accept pending invite, open org | **Done.** Org flow implemented. |
| **Open organization** → that org's CRM | User opens org from `/organizations` or switches in Settings; RequireOrgLayout redirects to `/organizations` if no current org | **Done.** Org switcher in Settings and AppHeader. |
| **Owner** invites by email, accepts/rejects requests | Settings: invite by email, list pending invites for org, list join requests with Accept/Reject | **Done.** Owner UI in Settings. |
| No external CRM; no Connection page | Connection page and API removed | **Done.** |
| Settings: **Organization** — switch org, view org name | Settings: Organization section with switcher, "Cadence is your only CRM", owner invite/join-request UI | **Done.** |
| "Send to CRM" = save in Cadence | Subtext: "Save your generated copy to a contact or deal in Cadence" | **Done.** Functionally correct; Help clarifies. |

### Frontend — route protection

- **Implemented:** A **RequireAuth** component and **ProtectedLayout** wrap all app routes that require login: `/organizations`, `/onboarding`, `/dashboard`, `/generated`, `/send`, `/templates`, `/history`, `/leads`, `/deals`, `/tasks`, `/activities`, `/contacts`, `/companies`, `/settings`. Unauthenticated users are redirected to `/login` with `state.from` so the intended URL is preserved. **RequireOrgLayout** (nested inside ProtectedLayout) then enforces a current organization for CRM routes when real API is used; if none, redirect to `/organizations`. Public routes: `/`, `/login`, `/help`, `/privacy`, `/terms`.
- Demo mode: user can click "Try demo (no backend)" and get a demo user; no token. That's by design. Demo user is considered "logged in" (getCurrentUser() is set), so protected routes are accessible.

### Frontend — deeper notes

| Area | Detail |
|------|--------|
| **Org flow** | After login (real API): user goes to `/organizations`; create org or accept invite, then open org. RequireOrgLayout redirects to `/organizations` if no current org. Demo: no org required; user can go to `/dashboard` directly. |
| **Homepage copy** | `src/app/pages/Homepage.tsx`: step 1 "Create or join an organization"; CTA "Create copy and save it in Cadence"; "Cadence is your CRM"; "One click saves to the right contact or deal in Cadence". Cadence-only; aligned with doc 01. |
| **Terms copy** | `src/app/pages/Terms.tsx`: Service Description "Cadence is your CRM and provides AI-powered copywriting tools built in. All data stays in Cadence; no external CRM integration is required." Cadence-only. |
| **RequireAuth** | `src/app/components/RequireAuth.tsx`: checks `getCurrentUser()` only; no org or token validation. Redirects to `/login` with `state.from`. **RequireOrgLayout** (in App.tsx) enforces current org for CRM routes when real API; redirects to `/organizations` if `hasFetched && !currentOrgId && !isOrgPage`. |
| **Login redirect** | Non-2FA login/register: `navigate(isUsingRealApi() ? '/organizations' : '/dashboard')`. 2FA login: currently navigates to `/dashboard`; RequireOrgLayout then redirects to `/organizations` if no current org. |

### Other documentation (src/app/reports)

| Document | Alignment with doc 01 / current backend |
|----------|----------------------------------------|
| **USER_FLOWS_REPORT.md** | Updated: steps include Organizations (create/join/open org); org flow and invite/join-request status (Implemented). Notes backend org APIs and frontend org flow. No Connection step. |
| **SALES_CRM_CORE_GAP_REPORT.md** | Updated: Companies page create/edit; API modules and pages list include organizations; note on org layer. |
| **FLOWS_BACKEND_DATABASE_VERIFICATION.md** | Updated: executive summary table includes Organizations, InvitesController, JoinRequestsController, org entities and migration AddOrganizationsAndOrgId; CrmConnections removed. |
| **01-HOW-THE-SYSTEM-WORKS (doc 01)** | **Outdated in two places:** §1.1 "Today (current UI)" says "the frontend does not yet show create/join org or org switcher"; §6 Summary says "org flow (create/join, switcher) not yet in the UI". Both false — frontend has `/organizations`, org switcher in Settings and AppHeader, and owner invite/join-request UI. Rest of doc 01 is current. |

---

## 4. Bugs and issues in current code

### Backend

| Item | Severity | Description |
|------|-----------|-------------|
| None critical | — | Auth and UserId checks are consistent. No obvious security bug in data access. |
| ~~CrmConnection naming~~ | **Resolved** | CrmConnection and Connection API have been **removed** per recommendation. |

### Frontend

| Item | Severity | Description |
|------|-----------|-------------|
| ~~No auth guard on protected routes~~ | **Fixed** | `RequireAuth` and `ProtectedLayout` now wrap all app routes; unauthenticated users are redirected to `/login`. |
| ~~AppHeader: org used but useOrgOptional not called~~ | **Fixed** | `AppHeader.tsx` referenced `org` for "current org name" and "Switch org" but did not call `useOrgOptional()`. Added `const org = useOrgOptional();` so the dropdown shows org and link to `/organizations` when using real API. |
| ~~Connection page~~ | **Removed** | Connection page and API removed. |
| ~~Settings Connection link~~ | **Removed** | Settings has Organization section only; no Connection link. |
| Homepage copy | Low | **Addressed.** Homepage updated for Cadence-only: "Create or join an organization", "Cadence is your CRM", "save it in Cadence". No "Connect your CRM". |
| Terms copy | Low | **Addressed.** Terms Service Description: "Cadence is your CRM... All data stays in Cadence; no external CRM integration is required." |
| "Send to CRM" label | Low | Saves in Cadence only; subtext clarifies. Doc 01 clarifies "Send to CRM here means save in Cadence". |
| Login footer / Pipeline empty | Low | Login.tsx footer: "Optional: connect integrations after you sign in" — generic; could reword to "Set up your organization and brand in Settings." Pipeline empty state: "connect your CRM" — could reword to "Create a deal to see it here" for Cadence-only. Non-blocking. |

### Documentation consistency

| Item | Severity | Description |
|------|-----------|-------------|
| ~~02 and 03 vs 01~~ | **Addressed** | 02 and 03 have been updated to align with 01 (org vision, backend support, org flow in UI). |
| ~~PROJECT_ASPECTS.md~~ | **Addressed** | PROJECT_ASPECTS updated: Connection removed, data isolation by UserId + OrganizationId, org entities in backend. |
| ~~PROJECT_ASPECTS §4~~ | **Addressed** | Section 4 "Authentication and security" states frontend has full org flow (create/join/open org, org switcher, owner invite/join-request UI). |
| **Doc 01 §1.1 and §6** | Low | **01-HOW-THE-SYSTEM-WORKS-AND-SALES-USAGE.md** §1.1 says "the frontend does not yet show create/join org or org switcher"; §6 Summary says "org flow (create/join, switcher) not yet in the UI". Both are outdated; frontend now has full org flow. Recommend updating those sentences. |
| **PROJECT_ASPECTS §8** | Low | "Their company" row in §8 says "frontend org flow not yet"; should state that frontend org flow is implemented (create/join/open org, switcher, owner UI). |
| **Backend README.md** | Low | **backend/README.md** Database section still lists "**CrmConnections** – CRM connection status per user". The CrmConnections table was removed (migration RemoveCrmConnection). Remove this line from the README. |

---

## 5. Recommendations

*The following recommendations have been implemented; the product now matches the target vision (doc 01). Kept for reference.*

### To make the product match the doc (target vision)

1. **Backend**
   - Introduce **Organization** (e.g. Id, Name, OwnerUserId, CreatedAtUtc).
   - Introduce **OrganizationMember** (OrganizationId, UserId, Role? e.g. Owner, Member).
   - Optionally **Invite** (OrganizationId, Email, Token, ExpiresAt) and **JoinRequest** (OrganizationId, UserId, Status).
   - Add **OrganizationId** to all data entities (Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistoryItem, UserSettings per org). Migrate existing data (e.g. assign a default org per user).
   - APIs: create org, list my orgs, join request (create/list/accept/reject), invite by email (create/accept). All data APIs should filter by current user's **current org** (e.g. header or query `X-Organization-Id` or store current org in token/session).
   - **Connection:** ~~Either remove CrmConnection/Connection API or rename~~ → **Done.** CrmConnection and Connection API removed (controller, service, entity, UserRepository methods, migration to drop CrmConnections table).

2. **Frontend**
   - After login: show **Create organization** or **Join organization** (list pending invites / join requests). Then **Open organization** (org switcher or default to first org). **Done.**
   - Store **current organization id** in context or session; send it with API calls. **Done.**
   - **Onboarding:** Keep company name + brand tone but scope to current org (first time in that org). **Done.**
   - **Settings:** Replace "CRM connection" with "Organization" — switch org, view org name; for owner: manage members, invites, join requests. **Done.** Connection link removed.
   - **Connection page:** ~~Either remove or repurpose~~ → **Done.** Connection page and `/connect` route **removed**; connection API deleted; Homepage, Help, Dashboard, Settings, AppHeader no longer reference Connection.
   - **Send to CRM:** Consider renaming to "Attach to contact/deal" or "Save to contact/deal" to avoid implying external CRM. Alternatively keep the label and rely on doc + Help to clarify.
   - Add **RequireAuth** (or similar) for routes that need login: `/dashboard`, `/send`, `/leads`, `/deals`, `/tasks`, `/activities`, `/contacts`, `/companies`, `/templates`, `/history`, `/settings`, `/onboarding`, `/generated`.

### To keep docs fully aligned (recommended edits)

1. **01-HOW-THE-SYSTEM-WORKS-AND-SALES-USAGE.md:** In §1.1 "Today (current UI)", replace the sentence that says "the frontend does not yet show create/join org or org switcher" with: "The frontend implements the full org flow: create or join an organization at `/organizations`, open an org, switch org in Settings or AppHeader; owners can invite by email and accept/reject join requests." In §6 Summary, replace "org flow (create/join, switcher) not yet in the UI — backend supports it" with: "org flow (create/join, switcher) is implemented in the UI; backend supports it."
2. **PROJECT_ASPECTS.md:** In §8 "User flows", "Their company" row, change "frontend org flow not yet" to "frontend org flow implemented (create/join/open org, switcher, owner invite/join-request UI)".
3. **backend/README.md:** In the Database section, remove the line "**CrmConnections** – CRM connection status per user" (table was dropped in migration RemoveCrmConnection). Optionally add a note that Organizations, OrganizationMembers, Invites, JoinRequests, OrgSettings are used for the org layer.

### Quick wins (no org model)

1. **Frontend:** ~~Add route guard for protected routes~~ → **Done.** `RequireAuth` component and `ProtectedLayout` in `App.tsx` now wrap all app routes (organizations, onboarding, dashboard, send, leads, deals, tasks, activities, contacts, companies, templates, history, settings). Unauthenticated users are redirected to `/login` with `state.from` so you can redirect back after login if desired. Public routes: `/`, `/login`, `/help`, `/privacy`, `/terms`.
2. **Frontend:** ~~In Help and Privacy, add one line about Cadence-only~~ → **Done.** Help has "Your data in Cadence"; Privacy states Cadence is the only CRM. Connection page removed; no "future integration" copy needed.
3. **Docs:** In **02** and **03**, add a note: "Target product vision (see report 01) includes organizations and owner/invite flow; implemented in current release."

### Cross-System Issues — Problems Spanning Multiple Entities

The following issues are not specific to a single entity (Contact, Deal, Task, etc.) but span the entire CRM. They are discovered by reading ALL entity-specific reports side by side. Each issue references the specific entity reports where it was first identified.

Each issue is classified as:
- **🔴 CRITICAL (Must Fix Immediately)** — Production bugs, data loss, or active deception. Fix before any new feature work.
- **🟠 HIGH PRIORITY (Must Do Before Release)** — Core CRM functionality without which the product is not competitive.
- **🟡 MEDIUM (Should Do)** — Important improvements that enhance usability but the system functions without them.

---

#### CS-1. Global Search Is Dead Code 🔴 CRITICAL

**Effort:** Low (1-2 days — backend already built) | **Affects:** ALL entities

**What exists now:**
- **Backend:** `GlobalSearchService.cs` searches across leads, contacts, companies, and deals in parallel. `SearchController` exposes `GET /api/search?q=`. Results are limited to 10 per entity type. **Fully functional.**
- **Frontend:** `search.ts` defines `globalSearch(query)` that calls the endpoint. `index.ts` barrel-exports it. **No component in the entire frontend ever imports or calls this function.** `AppHeader.tsx` has no search bar. No search results dropdown exists anywhere.

**Why this is critical:**
1. **CRM unusable at scale**: With 500+ contacts, 200+ deals, 100+ companies, and 1000+ leads, users need a single search bar to find anything. Currently, they must navigate to the specific entity page and use that page's local search — 4 different places to look.
2. **Complete implementation wasted**: Both backend (service, controller, DTOs, query logic) and frontend (API client, types) are fully built. The only missing piece is a search input in the header and a results dropdown — less than 100 lines of UI code.
3. **Standard CRM expectation**: Every CRM (Salesforce, HubSpot, Pipedrive, Zoho) has a global search bar in the header. Its absence makes the product feel incomplete.

**How to implement — step by step:**

1. **Frontend — `AppHeader.tsx`**: Add a search input with debounce:
   ```tsx
   const [searchQuery, setSearchQuery] = useState('');
   const [results, setResults] = useState<GlobalSearchResult | null>(null);
   const [showDropdown, setShowDropdown] = useState(false);

   const debouncedSearch = useMemo(() =>
     debounce(async (q: string) => {
       if (q.length < 2) { setResults(null); return; }
       const data = await globalSearch(q);
       setResults(data);
       setShowDropdown(true);
     }, 300),
   []);
   ```

2. **Frontend — `AppHeader.tsx`**: Render search bar in the header (between logo and user menu):
   ```tsx
   <div className="relative flex-1 max-w-md mx-4">
     <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
     <Input
       placeholder="Search contacts, companies, deals..."
       value={searchQuery}
       onChange={(e) => { setSearchQuery(e.target.value); debouncedSearch(e.target.value); }}
       onFocus={() => results && setShowDropdown(true)}
       className="pl-9"
     />
   </div>
   ```

3. **Frontend — Create `SearchResultsDropdown.tsx`**: Grouped results component:
   ```tsx
   <div className="absolute top-full left-0 right-0 mt-1 bg-white rounded-lg shadow-xl border z-50 max-h-[400px] overflow-y-auto">
     {results.contacts?.length > 0 && (
       <div>
         <h4 className="px-3 py-1.5 text-xs font-semibold text-slate-500 bg-slate-50">Contacts</h4>
         {results.contacts.map(c => (
           <Link key={c.id} to={`/contacts/${c.id}`} className="flex items-center gap-2 px-3 py-2 hover:bg-slate-50">
             <User className="w-4 h-4" /><span>{c.name}</span><span className="text-xs text-slate-400">{c.email}</span>
           </Link>
         ))}
       </div>
     )}
     {/* Repeat for Companies, Deals, Leads */}
   </div>
   ```

4. **Frontend — Keyboard navigation**: Add `onKeyDown` handler for arrow keys + Enter to navigate results.

5. **Frontend — Click outside**: Close dropdown when clicking outside using `useRef` + `useEffect` with click listener.

6. **Test:** Type "Acme" in search bar. Verify dropdown shows contacts, companies, and deals matching "Acme." Click a result. Verify navigation to the correct entity page.

**Referenced in:** Company report §18, Contact report §20.

---

#### CS-2. React Query Hooks Are Dead Code Across Multiple Entities 🟡 MEDIUM

**Effort:** Medium (2-3 days per page) | **Affects:** Tasks, Companies, (partially Activities)

**What exists now:**
- `useCompanies.ts`: Defines `useCompanies()`, `useCreateCompany()`, `useUpdateCompany()`, `useDeleteCompany()` with optimistic updates. **Never imported by any component.** `Companies.tsx` uses `useState` + direct API calls.
- `useTasks.ts`: Defines `useTasks()`, `useCreateTask()`, `useUpdateTaskStatus()` with optimistic updates. **Never imported by `Tasks.tsx`.** The main Tasks page uses `useState` + direct API calls.
- `useActivities.ts`: Hooks are partially used by `LeadDetailModal` and `Pipeline` detail sheet, but the main `Activities.tsx` page does NOT use them.

**Why this matters:**
1. **Wasted engineering effort**: Significant work went into building optimistic update logic, cache invalidation, and error handling in these hooks. None of it runs.
2. **Stale data across pages**: When Companies.tsx creates a company via direct API call, the `useCompanies()` cache in other components is unaware. If Contacts page uses `useCompanies()` for the company dropdown, it shows stale data until its own refetch.
3. **Inconsistent architecture**: Some pages use React Query (standard, with caching and optimistic updates), others use raw `useState` (no caching, full re-fetch on every operation). This makes the codebase confusing for developers and creates inconsistent UX (some operations feel instant, others show loading spinners).

**How to implement — step by step:**

1. **Companies.tsx migration** (start here — smallest page):
   - Replace all `useState` for companies data (`companies`, `loading`, `error`) with `const { data: companies, isLoading } = useCompanies()`
   - Replace `createCompany()` direct call with `const createMutation = useCreateCompany()`
   - Replace `updateCompany()` with `useUpdateCompany()` mutation
   - Replace `deleteCompany()` with `useDeleteCompany()` mutation
   - Remove `useEffect` that manually calls `getCompanies()` on mount
   - Remove manual `setCompanies()` state updates after create/update/delete

2. **Tasks.tsx migration** (largest page — do after Companies):
   - Replace task loading with `useTasks()` hook
   - Replace individual task mutations with the corresponding hooks
   - The Kanban drag-and-drop `handleDragEnd` should use `useUpdateTaskStatus()` for optimistic reordering
   - Test thoroughly — the Tasks page has the most complex interactions (Kanban DnD, bulk operations, context menus)

3. **Activities.tsx migration:**
   - Replace direct `getActivities()` call with `useActivities()` hook
   - Replace `createActivity()` with `useCreateActivity()` mutation
   - Ensure `LeadDetailModal` and `Pipeline` activity sections share the same cache via `queryKeys`

4. **Verify cache consistency**: After migration, test cross-page scenarios:
   - Create a company on Companies page → verify company dropdown on Contacts page shows it immediately
   - Complete a task on Tasks page → verify task count on Dashboard updates

5. **Cleanup**: Delete any remaining unused direct API call patterns that were replaced.

6. **Test:** For each page: create, update, delete entities. Verify optimistic updates (instant UI response before server confirms). Verify error rollback (if server rejects, UI reverts).

**Referenced in:** Company report §11, Task report §15, Activity report §12.

---

#### CS-3. No Detail Pages for Most Entities — Deep Linking Impossible 🟠 HIGH PRIORITY

**Effort:** Medium (2-3 days per entity, 8-12 days total) | **Affects:** Contacts, Companies, Tasks

**What exists now:**
- **Contacts:** Grid of cards. No `/contacts/:id` route. Only way to see details is the edit dialog.
- **Companies:** Grid of cards. No `/companies/:id` route. Company names have hover styling but no click handler.
- **Tasks:** Kanban/List view. No `/tasks/:id` route. Only way to see details is `TaskDetailModal` (overlay, not a page).
- **Deals:** Now has `/deals/:id` detail page (implemented in HP-6).

**Why this is critical:**
1. **Cannot share links**: Sales managers cannot send a colleague a link to a specific contact, company, deal, or task. Every reference requires verbal instructions: "Go to the Contacts page and search for Jane Smith."
2. **Browser history useless**: Clicking back/forward never takes you to a specific record. All entity interactions happen on the same URL (`/contacts`, `/companies`, `/tasks`, `/deals`), making browser navigation useless for CRM work.
3. **Bookmarking impossible**: Users cannot bookmark important records (key accounts, critical deals, high-priority tasks) for quick access.
4. **Every competitor has this**: Salesforce, HubSpot, Pipedrive, Close, Zoho — all have deep-linkable detail pages for every entity. This is table-stakes CRM functionality.

**How to implement — step by step:**

**A. Contact Detail Page (`/contacts/:id`) — 2-3 days:**

1. **Frontend — `App.tsx`**: Add route:
   ```tsx
   <Route path="/contacts/:id" element={<Suspense><ContactDetail /></Suspense>} />
   ```

2. **Frontend — Create `src/app/pages/ContactDetail.tsx`**:
   - `useParams()` to get contact ID
   - Fetch: `getContactById(id)`, `getActivitiesByContact(id)`, `getTasksByContact(id)`, `getDeals()` (filter by contactId)
   - **Layout — Header**: Name (h1), email, phone, job title, company name (linked), DoNotContact badge
   - **Layout — Tabs**: Overview (all fields + notes), Deals (linked deals with stage/value), Activities (timeline), Tasks (pending/completed)
   - **Layout — Sidebar**: Company card, quick actions (Edit, Archive, Log Activity, Add Task, Enroll in Sequence)

3. **Frontend — `Contacts.tsx`**: Make contact names clickable `<Link>`:
   ```tsx
   <Link to={`/contacts/${contact.id}`} className="font-medium hover:underline">{contact.name}</Link>
   ```

**B. Company Detail Page (`/companies/:id`) — 2-3 days:**

1. **Frontend — `App.tsx`**: Add route: `<Route path="/companies/:id" element={<Suspense><CompanyDetail /></Suspense>} />`

2. **Frontend — Create `src/app/pages/CompanyDetail.tsx`**:
   - Fetch: company by ID, contacts by company, deals by company, activities (aggregated)
   - **Layout — Header**: Company name, industry, domain, size, location
   - **Layout — Tabs**: Overview, Contacts (list with add), Deals (list with stage/value), Activities (company-wide timeline)
   - **Layout — Sidebar**: Company stats (total contacts, total deal value, active deals)

3. **Frontend — `Companies.tsx`**: Make company names clickable `<Link>`.

**C. Task Detail Page (`/tasks/:id`) — 2-3 days:**

1. **Frontend — `App.tsx`**: Add route: `<Route path="/tasks/:id" element={<Suspense><TaskDetail /></Suspense>} />`

2. **Frontend — Create `src/app/pages/TaskDetail.tsx`**:
   - Fetch: task by ID, linked deal, linked contact, linked lead, assignee
   - **Layout — Header**: Title (editable inline), status badge, priority flag, assignee avatar
   - **Layout — Main content**: Description editor, Notes editor, Due Date + Reminder picker
   - **Layout — Sidebar**: Linked items (deal, lead, contact — all clickable), Comments section (HP-11)
   - **Quick actions**: Edit, Delete, Change Status, Change Priority, Assign

3. **Frontend — Task cards**: Make task titles `<Link to={`/tasks/${task.id}`}>`.

4. **Test for all three**: Click entity → detail page renders. Copy URL → paste in new tab → same entity loads. Click back → returns to list.

**Referenced in:** Contact report HP-1, Company report HP-1, Deal report HP-6, Task report HP-10.

---

#### CS-4. Dead Form Fields — Data Silently Discarded 🔴 CRITICAL BUG

**Effort:** Low (1-2 days total for all entities) | **Affects:** Deals, Companies, Contacts

**What exists now:**
- **Deals:** `DealFormState` has `description` and `probability` fields with full form UI. Backend `Deal.cs` has NEITHER property. User input is silently discarded.
- **Companies:** Create/edit dialog renders Description, Website, and Location input fields. Backend `Company.cs` has none of these. User input is silently discarded.
- **Contacts:** `ContactFormState` has `description` field. Backend `Contact.cs` has no `Description` property. UI doesn't render it (form uses inline state), but the dead type creates confusion.

**Why this is critical:**
1. **Trust destruction**: Users type into visible, active form fields, click Save, see a success message, and believe their data was stored. When they return later and find the fields empty, they lose trust in the entire system. This is the most severe category of UX failure.
2. **Widespread**: This affects 3 of 5 core entities. It's not a one-off bug — it's a systemic pattern where frontend forms were built before backend support.
3. **Simple to fix**: Either add the backend fields (recommended — all are valuable CRM data) or remove the frontend form fields to prevent confusion.

**How to implement — step by step:**

**A. Deals — Description & Probability (half day):**
1. `Deal.cs`: Add `string? Description { get; set; }` and `int? Probability { get; set; }`
2. `DealConfiguration.cs`: `.HasMaxLength(4000)` for Description, no config needed for Probability
3. `DealDto.cs`: Add `Description` and `Probability` to the record
4. `CreateDealRequest.cs` / `UpdateDealRequest.cs`: Add both fields with validation (`[StringLength(4000)]`, `[Range(0,100)]`)
5. `DealService.cs`: Map in `CreateAsync`, `UpdateAsync`, and `Map()`
6. EF Migration: `dotnet ef migrations add AddDealDescriptionProbability`
7. Frontend `deals.ts`: Include `description` and `probability` in create/update API calls
8. Test: Create deal with description + probability → refresh → verify both persist

**B. Companies — Description, Website, Location (half day):**
1. `Company.cs`: Add `string? Description`, `string? Website`, `string? Location`
2. `CompanyConfiguration.cs`: Max lengths (4000, 500, 500 respectively)
3. `CompanyDto.cs`: Add all three fields
4. `CreateCompanyRequest.cs` / `UpdateCompanyRequest.cs`: Add with validation
5. `CompanyService.cs`: Map in Create, Update, and Map()
6. EF Migration: `dotnet ef migrations add AddCompanyDescriptionWebsiteLocation`
7. Frontend `companies.ts`: Include in create/update API calls
8. Test: Create company with all three fields → refresh → verify all persist

**C. Contacts — Description (few hours):**
1. `Contact.cs`: Add `string? Description { get; set; }`
2. `ContactDto.cs`, `CreateContactRequest.cs`, `UpdateContactRequest.cs`: Add `Description`
3. `ContactService.cs`: Map in all methods
4. EF Migration: `dotnet ef migrations add AddContactDescription`
5. Frontend: Add `<Textarea>` for description in contact create/edit form
6. Test: Add description to contact → refresh → verify persists

**Referenced in:** Deal report HP-1/A6/A20-A21, Company report HP-2, Contact report HP-7.

---

#### CS-5. `CreatedAtUtc` and `UpdatedAtUtc` Missing from DTOs 🟠 HIGH PRIORITY

**Effort:** Low (half day for all entities) | **Affects:** Contacts, Companies, Deals

**What exists now:**
- All backend entities (`Contact.cs`, `Company.cs`, `Deal.cs`) have `CreatedAtUtc` and `UpdatedAtUtc` properties on the domain entity.
- `ContactDto`, `CompanyDto`, and `DealDto` do NOT include these timestamps.
- Frontend types have no `createdAtUtc` or `updatedAtUtc` fields.

**Consequences:**
- **"This Week" stat permanently zero**: Companies and Contacts pages have "This Week" stat cards hardcoded to `0` with code comments explaining `createdAtUtc` is unavailable.
- **"Newest First" sort broken**: Sorting by "Newest First" on Contacts and Companies pages silently falls back to alphabetical order because there's no date to sort by.
- **No audit visibility**: Users cannot see when a record was created or last modified. No "Added 3 days ago" or "Last updated by Sarah on March 5th."

**How to implement — step by step:**

1. **Backend — Update DTOs** (all three at once):
   ```csharp
   // ContactDto.cs — add:
   public DateTime CreatedAtUtc { get; init; }
   public DateTime? UpdatedAtUtc { get; init; }

   // CompanyDto.cs — add:
   public DateTime CreatedAtUtc { get; init; }
   public DateTime? UpdatedAtUtc { get; init; }

   // DealDto.cs — add (if not already done in HP-4):
   public DateTime CreatedAtUtc { get; init; }
   public DateTime? UpdatedAtUtc { get; init; }
   ```

2. **Backend — Update Map() functions** in `ContactService.cs`, `CompanyService.cs`, `DealService.cs`:
   ```csharp
   CreatedAtUtc = entity.CreatedAtUtc,
   UpdatedAtUtc = entity.UpdatedAtUtc
   ```

3. **Frontend — Update TypeScript types** in `types.ts`:
   ```typescript
   // Add to Contact, Company, Deal interfaces:
   createdAtUtc: string;
   updatedAtUtc?: string;
   ```

4. **Frontend — Fix "This Week" stats** on Contacts and Companies pages:
   ```typescript
   const thisWeek = contacts.filter(c => {
     const created = new Date(c.createdAtUtc);
     const weekAgo = new Date(); weekAgo.setDate(weekAgo.getDate() - 7);
     return created >= weekAgo;
   }).length;
   ```

5. **Frontend — Fix "Newest First" sorting**:
   ```typescript
   case 'newest':
     return new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime();
   ```

6. **Frontend — Display dates**: Show "Added {date}" or "Updated {date}" on entity cards using `formatRelativeTime()`.

7. **Test:** Create a new contact. Verify "This Week" stat increments. Sort by "Newest First" — verify new contact appears first. Check card shows creation date.

**Referenced in:** Contact report HP-3, Company report HP-3, Deal report HP-4.

---

#### CS-6. Team Member Stats Use Mock/Random Data 🔴 CRITICAL

**Effort:** Low-Medium (1-2 days) | **Affects:** Team page

**What exists now:**
- `MemberDetailPanel.tsx` shows "Activities Logged" per team member as `Math.floor(Math.random() * 200) + 50` — a **random number** regenerated on every render.
- `MemberDetailPanel.tsx` shows "Tasks Completed" as `Math.floor(Math.random() * 50) + 10` — also random.

**Why this is critical:**
1. **Active deception**: Managers making decisions based on these numbers (performance reviews, coaching sessions, workload balancing) are using **random data**. A manager might praise a rep for "187 activities" that is actually a random number.
2. **Erodes trust in all metrics**: Once users discover any metric is fake, they question everything: "Is pipeline value real? Are deal counts accurate?"
3. **Simple to fix**: Replace `Math.random()` with actual queries. The backend already has activity and task data that can be counted per user.

**How to implement — step by step:**

1. **Backend — Create `TeamStatsDto`**:
   ```csharp
   public record TeamMemberStatsDto(
       Guid UserId,
       int ActivitiesLogged,
       int TasksCompleted,
       int TasksPending,
       int DealsWon,
       int DealsActive,
       decimal PipelineValue
   );
   ```

2. **Backend — Add endpoint** to `TeamController.cs` (or `OrganizationController.cs`):
   ```csharp
   [HttpGet("{userId}/stats")]
   public async Task<ActionResult<TeamMemberStatsDto>> GetMemberStats(Guid userId)
   {
       var activities = await _activityRepo.CountByUserAsync(userId, orgId);
       var tasksCompleted = await _taskRepo.CountByStatusAndUserAsync(userId, orgId, TaskStatus.Completed);
       var tasksPending = await _taskRepo.CountByStatusAndUserAsync(userId, orgId, TaskStatus.InProgress);
       var dealsWon = await _dealRepo.CountWonByUserAsync(userId, orgId);
       var dealsActive = await _dealRepo.CountActiveByUserAsync(userId, orgId);
       var pipelineValue = await _dealRepo.SumActiveValueByUserAsync(userId, orgId);
       return Ok(new TeamMemberStatsDto(userId, activities, tasksCompleted, tasksPending, dealsWon, dealsActive, pipelineValue));
   }
   ```

3. **Backend — Add repository methods** for each count query (simple `COUNT(*)` / `SUM()` with `WHERE UserId = @userId AND OrganizationId = @orgId`).

4. **Frontend — `MemberDetailPanel.tsx`**: Replace random numbers:
   ```typescript
   const [stats, setStats] = useState<TeamMemberStats | null>(null);
   useEffect(() => {
     getMemberStats(member.userId).then(setStats);
   }, [member.userId]);

   // Replace: Math.floor(Math.random() * 200) + 50
   // With: stats?.activitiesLogged ?? '—'
   ```

5. **Frontend — API**: Add `getMemberStats(userId)` function in `team.ts`.

6. **Interim fix (if backend takes time)**: Replace `Math.random()` with `'—'` or `'N/A'` immediately. This is a 5-minute change that stops active deception:
   ```tsx
   <span className="text-lg font-bold">—</span>
   <span className="text-xs text-slate-400">Coming soon</span>
   ```

7. **Test:** View team member panel. Verify stats show real numbers matching actual activity/task counts. Have member log an activity → verify count increments.

**Referenced in:** Activity report HP-3, Task report (MemberDetailPanel mock data).

---

#### CS-7. Activity Type Mismatch Breaks Multiple Systems 🔴 CRITICAL BUG

**Effort:** 5 minutes | **Affects:** Activities page, Pipeline deal activities, Tasks activity logging

**What exists now:**
- Frontend defines 7 activity types: `call`, `meeting`, `email`, `note`, `video`, `demo`, `task`.
- Backend only accepts 4: `call`, `meeting`, `email`, `note`.
- **Activities page**: Users selecting "Video Call" or "Demo" get a 400 error.
- **Tasks page**: `logTaskActivity()` creates activities with `type: 'task'`. Since `'task'` is rejected by the backend, EVERY task action that attempts to log an activity **silently fails**. The entire task activity audit trail is broken.
- **Pipeline page**: Deal detail sheet activity form includes "Task" as a type option, which will also fail.

**Impact:** This single mismatch causes failures across 3 different pages. The task activity logging failure is especially insidious because it's silent — no error shown to the user, the activity just never appears.

**How to implement — step by step:**

1. **Backend — `ActivityService.cs`**: Expand the `ValidActivityTypes` set (1 line change):
   ```csharp
   private static readonly HashSet<string> ValidActivityTypes = new(StringComparer.OrdinalIgnoreCase)
   {
       "call", "meeting", "email", "note", "video", "demo", "task"
   };
   ```

2. **Backend — `CreateActivityRequest.cs`**: Update the regex validation (if present):
   ```csharp
   [RegularExpression("^(call|meeting|email|note|video|demo|task)$", ErrorMessage = "Invalid activity type")]
   public string Type { get; init; } = string.Empty;
   ```

3. **Test from all three pages:**
   - Activities page: Log a "video" activity → verify success (no 400 error)
   - Activities page: Log a "demo" activity → verify success
   - Pipeline deal detail: Log a "task" activity → verify success
   - Tasks page: Complete a task → verify the auto-logged activity appears in the activities list

4. **Verify**: Call `GET /api/activities` and confirm all 7 activity types are stored and returned correctly.

**Referenced in:** Activity report HP-1, Deal report A18/HP-2, Task report (logTaskActivity).

---

#### CS-8. No Notification / Email System 🟠 HIGH PRIORITY (SYSTEMIC)

**Effort:** Phase 1: 1 hour | Phase 2: 1-2 weeks | **Affects:** Tasks, Deals, Leads, Settings

**What exists now:**
- `UserSettings.cs` defines: `EmailOnTaskDue`, `EmailOnDealUpdate`, `EmailOnNewLead` — all with UI toggles in Settings.
- **No email service exists**: No SMTP configuration, no SendGrid/Mailgun integration, no email sending code, no `IEmailService` interface.
- **No background job**: No `BackgroundService` or Hangfire/Quartz jobs to check for due tasks, deal changes, or new leads.
- Frontend `NotificationsSection.tsx` renders professional toggle switches that control absolutely nothing.

**Impact:** The entire notification system is a **facade**. Users enable notifications expecting them to work, then miss task deadlines and deal updates because no notification is ever sent. This is one of the most fundamental CRM capabilities — without notifications, the system is purely a data entry tool, not an active sales management tool.

**How to implement — step by step (phased):**

**Phase 1 — Stop the Deception (1 hour):**

1. **Frontend — `NotificationsSection.tsx`**: Disable all toggles and add "Coming soon" labels:
   ```tsx
   <div className="flex items-center gap-2 opacity-60">
     <Switch disabled checked={false} />
     <Label>Email when a task is due</Label>
     <Badge variant="outline" className="text-xs">Coming soon</Badge>
   </div>
   ```

2. Repeat for all three toggles (`EmailOnTaskDue`, `EmailOnDealUpdate`, `EmailOnNewLead`).

3. Add a note at the top: "Email notifications will be available in a future release. In-app notifications coming soon."

**Phase 2 — Task Reminders (1-2 weeks):**

1. **Backend — Create `IEmailService` interface:**
   ```csharp
   public interface IEmailService
   {
       Task SendTaskDueReminderAsync(TaskItem task, User user);
       Task SendTaskAssignedAsync(TaskItem task, User assignee, User assigner);
       Task SendDealStageChangedAsync(Deal deal, string fromStage, string toStage, User assignee);
   }
   ```

2. **Backend — Create `SmtpEmailService.cs`** implementing `IEmailService`:
   ```csharp
   public class SmtpEmailService : IEmailService
   {
       private readonly SmtpClient _client;
       public SmtpEmailService(IOptions<EmailSettings> settings) { /* configure SMTP */ }

       public async Task SendTaskDueReminderAsync(TaskItem task, User user)
       {
           var message = new MailMessage(
               from: _settings.FromAddress,
               to: user.Email,
               subject: $"Task Due: {task.Title}",
               body: $"Your task '{task.Title}' is due {task.DueDateUtc:MMM d}."
           );
           await _client.SendMailAsync(message);
       }
   }
   ```

3. **Backend — `appsettings.json`**: Add email configuration:
   ```json
   "Email": {
     "SmtpHost": "smtp.sendgrid.net",
     "SmtpPort": 587,
     "FromAddress": "notifications@cadencecrm.com",
     "FromName": "Cadence CRM",
     "ApiKey": "SG.xxxx"
   }
   ```

4. **Backend — Create `TaskReminderBackgroundService.cs`**: Background service that runs every 5 minutes, checks for tasks with `ReminderDateUtc <= now AND ReminderSentAtUtc IS NULL`, and sends emails.

5. **Backend — `TaskItem.cs`**: Add `DateTime? ReminderSentAtUtc` to prevent duplicate sends.

6. **Backend — `Program.cs`**: Register services:
   ```csharp
   builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
   builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
   builder.Services.AddHostedService<TaskReminderBackgroundService>();
   ```

7. **Frontend**: Re-enable the `EmailOnTaskDue` toggle, remove "Coming soon" badge.

**Phase 3 — Full Notifications (additional 1-2 weeks):**

1. Deal assignment: When `AssigneeId` changes on a deal, send email to new assignee
2. Deal stage change: When `DealStageId` changes, notify assignee
3. New lead: When a lead is created, notify assigned team member
4. In-app notifications: Add `Notification` entity, notification bell icon in header, notification dropdown

**Referenced in:** Task report HP-2/HP-3, Deal report (emailOnDealUpdate dead setting).

---

#### CS-9. No Soft Delete / Archive for Most Entities 🟡 MEDIUM

**Effort:** Medium (1-2 days per entity) | **Affects:** Deals, Companies, Tasks, Activities

**What exists now:**
- **Contacts:** Full archive/unarchive support (entity fields, backend endpoints, frontend API, React Query hooks). **But:** UI buttons are missing — only hard delete exposed.
- **Deals:** No archive. Only hard delete. `DealRepository.DeleteAsync` is permanent.
- **Companies:** No archive. Hard delete with FK nullification on related entities.
- **Tasks:** Has `Cancelled` status but no archive. Cancelled tasks stay visible in the main list.
- **Activities:** Hard delete only. No archive. Deleted activities are permanently lost.

**Why this matters:**
1. **Accidental data loss**: A user accidentally deleting a deal with 50+ linked activities loses all that data permanently. No recovery, no undo, no recycle bin.
2. **Regulatory requirements**: In some industries, records cannot be permanently deleted for compliance reasons. Soft delete / archive satisfies "mark as inactive" while preserving data.
3. **Historical analysis**: Deleted records are lost from analytics. If a company is deleted, all historical pipeline analysis involving that company becomes incomplete.

**How to implement — step by step:**

1. **Backend — Add fields to each entity** (Deal, Company, TaskItem, Activity):
   ```csharp
   public bool IsArchived { get; set; } = false;
   public DateTime? ArchivedAtUtc { get; set; }
   public Guid? ArchivedByUserId { get; set; }
   ```

2. **Backend — Update repositories**: Add `IsArchived = false` filter to all default queries:
   ```csharp
   // In GetPagedAsync, SearchAsync, etc.:
   .Where(e => !e.IsArchived)
   ```

3. **Backend — Add archive/unarchive endpoints** for each controller:
   ```csharp
   [HttpPost("{id}/archive")]
   public async Task<ActionResult> Archive(Guid id) { /* set IsArchived = true */ }

   [HttpPost("{id}/unarchive")]
   public async Task<ActionResult> Unarchive(Guid id) { /* set IsArchived = false */ }

   [HttpGet("archived")]
   public async Task<ActionResult<List<EntityDto>>> GetArchived() { /* return where IsArchived = true */ }
   ```

4. **Backend — EF Migration**: `dotnet ef migrations add AddSoftDeleteAllEntities`

5. **Frontend — Replace "Delete" with "Archive"** in dropdown menus for Deals, Companies:
   ```tsx
   <DropdownMenuItem onClick={() => archiveEntity(entity.id)}>
     <Archive className="w-4 h-4 mr-2" />
     Archive
   </DropdownMenuItem>
   ```

6. **Frontend — Add "Show Archived" toggle** on each entity page (Contacts, Companies, Pipeline, Tasks):
   ```tsx
   <div className="flex items-center gap-2">
     <Switch checked={showArchived} onCheckedChange={setShowArchived} />
     <Label className="text-sm">Show archived</Label>
   </div>
   ```

7. **Frontend — Archived entity styling**: Grey out archived entities, show "Archived" badge, and "Unarchive" action.

8. **Frontend — Contact page**: Wire the existing `useArchiveContact` / `useUnarchiveContact` hooks to the UI dropdown (already built, just needs a button).

9. **Test:** Archive a deal. Verify it disappears from Pipeline. Toggle "Show Archived." Verify it appears greyed out. Unarchive. Verify it returns to normal view.

**Referenced in:** Contact report HP-5.

---

#### CS-10. Performance — Pages Load ALL Related Data Client-Side 🟠 HIGH PRIORITY

**Effort:** Medium (2-3 days per page) | **Affects:** Companies, Pipeline, Contacts, Tasks pages

**What exists now:**
- **Companies.tsx:** Loads ALL contacts (`getContacts()`) and ALL deals (`getDeals()`) on every page load just to compute per-company counts.
- **Pipeline.tsx:** Loads ALL deals (`getDeals()`), ALL contacts (`getContacts()`), ALL companies (`getCompanies()`) for name lookups.
- **Tasks.tsx:** Loads ALL tasks (see Task HP-1), ALL deals, ALL leads, ALL contacts, ALL org members.
- **Contacts.tsx:** Loads ALL contacts and ALL companies.

**Why this matters:**
1. **Won't scale past ~500 records per entity**: At 1000+ contacts, 500+ deals, and 200+ companies, these pages will download megabytes of JSON on every load. Mobile users will experience multi-second load times.
2. **Bandwidth waste**: The Companies page downloads ALL contacts/deals just to count how many belong to each company. This could be a single SQL `COUNT(*)` aggregation on the backend.
3. **Memory pressure**: Keeping thousands of records in React state across multiple pages causes high memory usage, especially on lower-end devices.

**How to implement — step by step:**

**A. Companies Page — Server-Side Stats (1 day):**

1. **Backend — Create `CompanyWithStatsDto`**:
   ```csharp
   public record CompanyWithStatsDto(
       Guid Id, string Name, string? Domain, string? Industry, string? Size,
       int ContactCount, int DealCount, decimal TotalDealValue,
       DateTime CreatedAtUtc, DateTime? UpdatedAtUtc
   );
   ```

2. **Backend — `CompanyService.cs`**: New method with SQL aggregation:
   ```csharp
   public async Task<List<CompanyWithStatsDto>> GetWithStatsAsync(Guid orgId) =>
       await _context.Companies
           .Where(c => c.OrganizationId == orgId)
           .Select(c => new CompanyWithStatsDto(
               c.Id, c.Name, c.Domain, c.Industry, c.Size,
               c.Contacts.Count(),
               c.Deals.Count(),
               c.Deals.Sum(d => d.Value),
               c.CreatedAtUtc, c.UpdatedAtUtc
           )).ToListAsync();
   ```

3. **Backend — Endpoint**: `GET /api/companies/with-stats`

4. **Frontend — `Companies.tsx`**: Replace separate `getContacts()` + `getDeals()` calls with single `getCompaniesWithStats()`. Use `company.contactCount` and `company.dealCount` directly instead of filtering arrays.

**B. Pipeline Page — Enriched DTOs (already done in Deal HP-4):**

The `DealDto` enrichment (adding `contactName`, `companyName`, `assigneeName`) eliminates the need to load all contacts and companies. Verify `Pipeline.tsx` uses `deal.contactName` instead of `contacts.find(c => c.id === deal.contactId)?.name`.

**C. Tasks Page — Server-Side Filtering (1-2 days):**

1. **Frontend — `Tasks.tsx`**: Replace `getTasks()` (gets 20 items) with `getTasksAll()` or server-side filtered endpoint:
   ```typescript
   // Instead of loading ALL tasks and filtering client-side:
   const tasks = await getTasksPaged({ status: statusFilter, priority: priorityFilter, search: query, pageSize: 100 });
   ```

2. **Backend**: Ensure `GET /api/tasks` supports `status`, `priority`, `search` query parameters for server-side filtering.

3. **Frontend**: Remove client-side filter logic that duplicates server-side capability.

**D. Contacts Page — Paginated Loading (1 day):**

The Contacts page already uses `getContactsPaged()` which is correct. Verify it doesn't also load all companies unnecessarily. If companies are only needed for the dropdown, fetch lazily on dialog open rather than on page load.

5. **Test per page:** With 1000+ records in the database, verify each page loads in under 2 seconds. Verify network tab shows reasonable payload sizes (< 100KB per page load).

**Referenced in:** Company report HP-7, Deal report HP-4/HP-5, Task report HP-1.

---

### Next: toward a production-grade team CRM

For **what to build next** so Cadence becomes a real multi-user team CRM (not just a foundation), see **05-BLUEPRINT-GAP-REPORT.md**:

- **Remaining items (priority order)** — Must add: (1) User management lifecycle (change role, remove/disable user, transfer ownership), (2) Data visibility & access rules (private/team/org-wide), (3) Reporting (pipeline by stage, deals by rep, activity per rep, conversion rate). Then glue: global search, saved views, duplicate detection + merge, soft delete + restore. Phase 2: products/line items, custom fields, light automation.
- **Clean sprint list** in that report: **Sprint 1 (team-ready):** Remove/disable user + transfer ownership; role change UI + enforcement; visibility mode. **Sprint 2 (manager-ready):** Reports (pipeline by stage, deals by rep, activity per rep); global search; saved views. **Sprint 3 (data-quality):** Duplicate detection + merge; soft delete + restore.

If the target is **solo CRM** vs **team CRM**, the blueprint report suggests trimming the list (e.g. skip visibility mode and transfer ownership for solo).

---

## 6. Implementation status

| Gap / item | Status | Notes |
|------------|--------|--------|
| Route guard (RequireAuth) | **Done** | Protected routes redirect to `/login` when not authenticated. |
| Backend: Organization, OrganizationMember, Invite, JoinRequest | **Done** | Entities + migration + OrganizationsController (list, create, get). InvitesController and JoinRequestsController (create invite, list pending, accept by token/id; create join request, list pending, accept/reject). |
| Backend: OrgId on data entities + backfill | **Done** | Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistoryItem have OrganizationId; backfill on create-org. |
| Backend: Current org (X-Organization-Id header) | **Done** | ICurrentUserService exposes CurrentOrganizationId from header. |
| Backend: Data APIs scope by org | **Done** | Repositories and services filter by OrganizationId when X-Organization-Id header is set; controllers pass CurrentOrganizationId. |
| Frontend: Org flow (create/join, open org, switcher) | **Done** | `/organizations` page (create org, accept invite, open org); OrgProvider + RequireOrgLayout; Login → /organizations when real API; org switcher in Settings and AppHeader. |
| Frontend: Owner invite + join requests UI | **Done** | Settings: invite by email, list pending invites for org, list pending join requests with Accept/Reject. |
| Copy: Settings, Help, Privacy, Send to CRM | **Done** | Connection removed; Settings "Organization"; Help/Privacy/SendToCrm Cadence-only. |
| Copy: Homepage, Terms | **Done** | Homepage and Terms updated for Cadence-only (no "Connect your CRM" / "integrate with your CRM"). |
| Frontend API: organizations | **Done** | `src/app/api/organizations.ts`; list/create/get orgs, invites, join requests; `http.ts` and `apiClient.ts` send `X-Organization-Id` when set. |
| AppHeader org display | **Done** | AppHeader calls `useOrgOptional()` and shows current org name + "Switch org" link when real API (fixed missing hook). |
| Docs: src/app/reports (USER_FLOWS, SALES_CRM_CORE_GAP) | **Done** | USER_FLOWS and SALES_CRM_CORE_GAP updated (org flow, Companies create/edit). |
| Docs 02, 03, PROJECT_ASPECTS | **Done** | 02, 03, and PROJECT_ASPECTS updated to align with 01 (org vision, org flow in UI). |
| Docs: Doc 01 §1.1/§6, PROJECT_ASPECTS §8, backend README | **Open** | Three small edits: Doc 01 two sentences; PROJECT_ASPECTS "Their company" row; backend README remove CrmConnections line. See §4 and §5. |
| Remove CrmConnection/Connection (backend + frontend) | **Done** | Backend: ConnectionController, IConnectionService, ConnectionService, CrmConnection entity, CrmConnectionConfiguration, User.CrmConnection, UserRepository GetConnection/UpsertConnection, migration RemoveCrmConnection. Frontend: Connection page, `/connect` route, connection API, Settings/AppHeader/Help/Homepage/Dashboard Connection references. |

---

## 7. Verification checklist (last run)

The following were checked to ensure the report matches the codebase. This report was updated after a **deep read** of backend (Program.cs, CurrentUserService, Organizations/Invites/JoinRequests and data controllers, DependencyInjection, repositories, services) and frontend (App.tsx, auth, http, apiClient, organizations API, OrgContext, RequireAuth, AppHeader, Connection, Login, Organizations, Settings, Homepage, Terms, SendToCrm) and related docs (01–03, PROJECT_ASPECTS, USER_FLOWS, SALES_CRM_CORE_GAP, FLOWS_BACKEND_DATABASE_VERIFICATION).

| Check | Result |
|-------|--------|
| Backend build | **Pass** — `dotnet build` succeeds. |
| Data controllers pass CurrentOrganizationId | **Pass** — Companies, Contacts, Deals, Leads, Tasks, Activities, CopyHistory, Reporting, Copy (send) all pass `_currentUser.CurrentOrganizationId` to services. |
| Data repositories accept organizationId | **Pass** — Company, Contact, Deal, Lead, TaskItem, Activity, CopyHistoryRepository all have `Guid? organizationId` and filter by UserId + org. |
| Org APIs | **Pass** — OrganizationsController: list, create (with backfill), get. |
| Frontend http client | **Pass** — `http.ts` and `apiClient.ts` send `X-Organization-Id` when `getCurrentOrganizationId()` is set. |
| Frontend copy: Connection | **Pass** — Connection page and API removed; no Connection copy in app. |
| Frontend copy: Settings | **Pass** — "Organization" section (switcher, invite/join for owner), "Cadence is your only CRM"; no Connection link. |
| Frontend copy: Help | **Pass** — "Cadence is the only CRM you need", "Send to CRM" = save in Cadence. |
| Frontend copy: Privacy | **Pass** — "Cadence is the only CRM", no external CRM required. |
| Frontend copy: SendToCrm | **Pass** — "Save your generated copy to a contact or deal in Cadence". |
| Frontend copy: Homepage | **Pass** — Updated for Cadence-only (create/join org, save in Cadence, no external CRM). |
| Frontend copy: Terms | **Pass** — Service Description: "Cadence is your CRM"; no external integration. |
| Frontend API: organizations | **Pass** — `organizations.ts`; X-Organization-Id in `http.ts` and `apiClient.ts`. |
| Route guard | **Pass** — RequireAuth + ProtectedLayout wrap app routes. |
| Docs alignment (01–04, PROJECT_ASPECTS) | **Pass** — All reports updated for consistency with vision (org layer, Connection removed, org flow implemented). Doc 01 §1.1 and §6 each have one outdated sentence; see below. |
| Backend: Invite/JoinRequest APIs | **Pass** — InvitesController and JoinRequestsController with create, list pending, accept/reject. |
| Backend: Templates/Settings user-scoped | **Confirmed** — TemplatesController, SettingsController do not pass CurrentOrganizationId; Template and UserSettings remain user-scoped. |
| Post-login flow (no Connection) | **Pass** — Login → `/organizations` (real API) or `/dashboard` (demo). No Connection step. |
| Login post-auth redirect | **Pass** — Non-2FA: `/organizations` when real API, `/dashboard` when demo. 2FA: `/dashboard` (RequireOrgLayout redirects to `/organizations` if no current org). |
| src/app/reports alignment | **Pass** — USER_FLOWS and SALES_CRM_CORE_GAP updated (org flow, Companies create/edit). |
| Doc 01 section 1.1 and §6 | **Note** — Doc 01 §1.1 says "the frontend does not yet show create/join org or org switcher"; §6 Summary says "org flow (create/join, switcher) not yet in the UI". Both are outdated; frontend now has `/organizations`, org switcher, and owner invite/join-request UI. **Recommendation:** Update those two sentences in 01-HOW-THE-SYSTEM-WORKS-AND-SALES-USAGE.md to state that the frontend implements the full org flow. |
| AppHeader org context | **Pass** — AppHeader calls `useOrgOptional()` and shows current org name + "Switch org" link when real API and org set. (Previously missing hook; fixed.) |
| Backend README Database section | **Fail** — Still lists CrmConnections; table was removed. Remove that line (see §4 Documentation consistency). |

*Report generated from deep codebase read. Verified: backend (Program.cs, CurrentUserService, OrganizationsController, InvitesController, JoinRequestsController, CompaniesController, LeadsController, DealsController, ContactsController, TasksController, ActivitiesController, CopyHistoryController, ReportingController, CopyController — all data controllers pass CurrentOrganizationId; AppDbContext has no CrmConnections; MigrateAsync on startup), frontend (App.tsx routes and RequireOrgLayout, lib/auth.ts ORG_ID_KEY, api/http.ts and apiClient.ts X-Organization-Id, contexts/OrgContext, AppHeader useOrgOptional and org dropdown, Login redirect to /organizations when real API, pages Organizations/Settings/Help/Homepage/Terms/SendToCrm, no Connection page or API), doc 01, PROJECT_ASPECTS, USER_FLOWS, FLOWS_BACKEND_DATABASE_VERIFICATION. Last verified: February 2026.*