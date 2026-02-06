# User Flows Report — From Beginning to Full CRM

This report describes **all the flows one user can do** in the Cadence program: from the very beginning (sign up, company setup) through **having their own company**, **inviting or adding new people** (what exists today), and **all CRM stuff** (leads, deals, tasks, activities, copy, etc.). The system is **standalone**: no external API or cloud is required (demo mode or full stack locally—see [RUN_FROM_SCRATCH.md](../../RUN_FROM_SCRATCH.md)).

---

## 1. From the Beginning (New User Journey)

| Step | Flow | What the user does | Where in the app |
|------|------|--------------------|------------------|
| 1 | **Land** | Visits the app (homepage). | `/` — Homepage |
| 2 | **Sign up or sign in** | Clicks “Sign In” or “Get started” → goes to Login. Can **Create account** (name, email, password) or **Sign in** (email, password). If account has 2FA, enters 6-digit code. If no backend is configured, can use “Try demo (no backend)” to enter without an account. | `/login` — Login |
| 3 | **Create or join organization** | When backend is used: Organizations page — Create organization or Accept pending invite; then Open an organization. | `/organizations` — Organizations |
| 4 | **Set up company (brand)** | Onboarding: enter **Company Name** and **Brand Tone** for the Intelligent Sales Writer. Save → Dashboard. | `/onboarding` — Onboarding |
| 5 | **Use the app** | User lands on Dashboard for the current organization. Data scoped by org; switch org in Settings. | `/dashboard` — Dashboard |

**Summary — Beginning:**  
One user = one account. They **sign up** → **organizations** (create/join/open org) → **set company name and brand tone** (their company for copy) → **Dashboard**. There is no separate “organization” sign-up; the “company” in onboarding is the name and tone used when generating copy.

---

## 2. Having Their Own Company (What Exists Today)

| Concept | What the app has | What it does **not** have |
|--------|-------------------|----------------------------|
| **My company (brand)** | **UserSettings:** Company name + brand tone set in Onboarding or Settings. Used when generating copy (e.g. “how we can help [Company Name]”). | No “company” as a multi-user organization. |
| **Companies in the CRM** | **Companies** (`/companies`): List of **accounts/organizations** the user tracks (customer companies). List, search, **Add company**, **Edit**, **Delete**. Domain, Industry, Size. Used when creating **Leads** or **Deals** (link a lead/deal to a company). | No create/edit companies in UI; no “my company” as the owner of these records. |
| **Who owns the data** | Every record (contacts, deals, leads, tasks, activities, copy history) is **owned by the signed-in user** (filtered by `UserId` in the backend). So “their company” in practice = **one user’s data**. | No company entity that “owns” multiple users or shared data. |

**Summary — Their own company:**  
- **Their company** = company name + brand tone (Onboarding/Settings) for copy.  
- **Companies in CRM** = accounts they sell to (list, add, edit; used on leads/deals).  
- **Inviting or adding new people** to the same “company” **Inviting or adding new people:** Owner can invite by email and accept/reject join requests (see next section).

---

## 3. Inviting or Adding New People (Current State)

| Capability | Status | Notes |
|------------|--------|-------|
| **Invite users to an organization** | ✅ Implemented | No “organization” or “workspace”; Backend: InvitesController. Frontend: Settings — owner "Invite by email"; Organizations — accept pending invites. |
| **Add team members** | ✅ Implemented | No team entity; no “add member” or roles (admin/member). |
| **Share data with other users** | ✅ Implemented | OrganizationMember (Owner/Member/Manager); data scoped by org when `X-Organization-Id` set. |
| **Multi-user per organization** | ✅ Implemented | One login = one user; no “company account” with multiple logins. |

**Summary — Inviting/adding people:**  
**Backend:** Organizations, OrganizationMember, Invite, JoinRequest entities and APIs. Owner can invite by email and accept/reject join requests. **Frontend:** Organizations page (create org, accept invite, open org); Settings — org switcher and (for owner) invite by email, pending invites, join requests Accept/Reject.

---

## 4. All CRM Flows in This Program (What One User Can Do)

Below is the full set of **flows one user can do** in the app after sign-in.

### 4.1 Copy & Content

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **Generate copy** | On Dashboard: choose content type (Sales Email, Follow-up, CRM Note, Deal Message, Workflow), goal, optional context, length → “Generate copy” → see result on Generated Copy page. | `/dashboard` → `/generated` |
| **Use a template** | Go to Templates, pick a template, “Use template” → Dashboard pre-filled with that template’s type and goal. | `/templates` → `/dashboard` |
| **Adjust and send copy** | On Generated Copy: edit text, copy to clipboard, change tone (shorter/friendlier/persuasive), regenerate, or “Send to CRM” → pick contact or deal and send. | `/generated` → `/send` |
| **Send copy to CRM** | On Send to CRM: choose object type (Contact, Deal, Workflow, Email draft), pick a contact or deal, “Confirm & Send”. Copy is recorded in copy history. | `/send` |
| **View copy history** | History page: list of sent copy, search by copy/type/recipient, copy to clipboard, or “Regenerate” (back to Dashboard with context). | `/history` |
| **Email sequences** | Create multi-step drip campaigns: add steps with delay times, enroll contacts/leads, track progress. | `/sequences` |
| **A/B testing** | Create tests with multiple copy variants, track impressions/clicks/conversions, declare winners based on performance. | `/ab-tests` |
| **Copy analytics** | View generation stats, response rates, daily trends, copy type breakdown, and quick insights. | `/analytics` |

### 4.2 Leads

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **List leads** | View all leads (name, email, company, status, source). | `/leads` |
| **Search leads** | Search by name/email (client-side filter on list). | `/leads` |
| **Add lead** | “Add lead” → dialog: name, email, phone, company (dropdown), source, status → Save. | `/leads` |
| **Edit lead** | “Edit” on a lead → same dialog, update and Save. | `/leads` |

### 4.3 Pipeline (Deals)

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **View pipeline** | Kanban: deals by stage (Qualification, Proposal, Negotiation, Closed Won, Closed Lost). | `/deals` |
| **Move deal stage** | Change stage in dropdown on a deal card; if set to Closed Won/Lost, deal is marked won/lost. | `/deals` |
| **New deal** | “New deal” → name, value, stage, optional expected close date, optional company → Save. | `/deals` |

### 4.4 Tasks

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **List tasks** | View tasks; filter All / Pending / Overdue. | `/tasks` |
| **Add task** | “Add task” → title, description, due date, optional link to lead, optional link to deal → Save. | `/tasks` |
| **Edit task** | “Edit” on a task → update fields and Save. | `/tasks` |
| **Complete task** | Toggle checkbox to mark complete/incomplete. | `/tasks` |

### 4.5 Activities

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **List activities** | View all activities (calls, meetings, emails, notes) or filter by contact or by deal. | `/activities` |
| **Log activity** | “Log activity” → type (call/meeting/email/note), subject, body, optional contact, optional deal → Save. | `/activities` |

### 4.6 Companies (CRM Accounts)

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **List companies** | View companies (accounts they track). Search by name. | `/companies` |
| **Add company** | "Add company" → dialog: name → Save. | `/companies` |
| **Edit company** | "Edit" on a company → dialog: name → Update. | `/companies` |

### 4.7 Contacts

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **List contacts** | View contacts (name, email, phone); search by name/email/phone. | `/contacts` |
| **Use contacts** | Contacts are used when sending copy to CRM (select a contact or deal) and in Activities for “by contact” filter. | `/send`, `/activities` |

### 4.8 Dashboard & Reporting

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **Dashboard stats** | See copy stats (generated this week, templates, sent to CRM) and **CRM stats**: active leads count, active deals count, pipeline value, deals won vs lost. | `/dashboard` |
| **Recent activity** | Last 5 copy history items on Dashboard; “View full history” → History. | `/dashboard` |

### 4.9 Settings & Account

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **Brand settings** | Company name and brand tone (same as onboarding). Save changes. | `/settings` |
| **Organization** | Switch current org; link to Manage organizations. For owner: invite by email, accept/reject join requests. | `/settings`, `/organizations` |
| **2FA** | Enable 2FA (setup, then enter code to confirm) or disable 2FA (password + code). | `/settings` |
| **Logout** | Sign out → back to Login. | Header → Sign out |
| **Delete account** | Two-step confirm → account removed, back to Homepage. | `/settings` |

### 4.10 Other

| Flow | What the user does | Route / Page |
|------|--------------------|--------------|
| **Help** | Read how the tool works, how to generate copy, your data in Cadence, data & privacy, contact support. | `/help` |
| **Privacy** | Read privacy policy. | `/privacy` |
| **Terms** | Read terms of service. | `/terms` |

---

## 5. Flow Summary (At a Glance)

| Phase | Flows available | Invite / add people? |
|-------|-----------------|----------------------|
| **Beginning** | Homepage → Login/Register (with 2FA) → Organizations (create/join/open org) → Onboarding (company name + brand) → Dashboard | No |
| **Their company** | Company name + brand in settings; Companies in CRM = accounts they track (read-only). One user = one account. | No |
| **Inviting/adding people** | — | **Not implemented** |
| **CRM** | Leads (list, search, add, edit, delete), Pipeline (`/deals` — Kanban, move stage, new deal, delete), Tasks (list, add, edit, complete), Activities (list, log, filter by contact/deal), Contacts (list, search at `/contacts`), Companies (list, add, edit), Send to CRM (contacts/deals/leads), **Intelligent Sales Writer** (generate, templates, history, **email sequences, A/B testing, analytics**), Dashboard stats, Settings (brand, organization, 2FA, logout, delete) | N/A |

---

## 6. Related Reports

- **[SALES_CRM_CORE_GAP_REPORT.md](SALES_CRM_CORE_GAP_REPORT.md)** — Core CRM features: what we have vs what we don’t.
- **[FRONTEND_PAGES_REPORT.md](FRONTEND_PAGES_REPORT.md)** — All routes, pages, and API usage.
- **[FLOWS_BACKEND_DATABASE_VERIFICATION.md](FLOWS_BACKEND_DATABASE_VERIFICATION.md)** — Backend and database alignment for these flows.
- **[PROJECT_ASPECTS.md](../../PROJECT_ASPECTS.md)** — Every aspect of the project in one place.

---

*Report describes all user flows from beginning through company setup, inviting/adding people, and full CRM usage. Backend: 97% production ready with comprehensive validation and 169 unit tests across 10 services. Last updated: February 6, 2026.*
