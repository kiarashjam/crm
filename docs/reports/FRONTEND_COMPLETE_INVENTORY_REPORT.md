# Frontend Complete Inventory Report

**Cadence CRM** — Full inventory of pages, components, and all user-facing text.  
*Generated: February 2026*

---

## 1. Routes & Pages Summary

| Route | Page Component | Auth Required |
|-------|----------------|---------------|
| `/` | Homepage | No |
| `/login` | Login | No |
| `/help` | Help | No |
| `/privacy` | Privacy | No |
| `/terms` | Terms | No |
| `/organizations` | Organizations | Yes |
| `/onboarding` | Onboarding | Yes |
| `/dashboard` | Dashboard | Yes |
| `/generated` | GeneratedCopy | Yes |
| `/send` | SendToCrm | Yes |
| `/templates` | Templates | Yes |
| `/history` | History | Yes |
| `/leads` | Leads | Yes |
| `/deals` | Pipeline | Yes |
| `/tasks` | Tasks | Yes |
| `/activities` | Activities | Yes |
| `/contacts` | Contacts | Yes |
| `/companies` | Companies | Yes |
| `/settings` | Settings | Yes |

---

## 2. All Components

### 2.1 App-Level Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **AppHeader** | `components/AppHeader.tsx` | Main nav bar: logo, nav links, global search, user dropdown, mobile menu |
| **DemoBanner** | `components/DemoBanner.tsx` | Amber banner shown in demo mode (no backend) |
| **EmptyState** | `components/EmptyState.tsx` | Icon + title + description + optional CTA(s) for empty lists |
| **LoadingSpinner** | `components/LoadingSpinner.tsx` | Spinner with optional label (sm/md/lg) |
| **RequireAuth** | `components/RequireAuth.tsx` | Route guard: redirects to /login if not authenticated |
| **SkipLink** | `components/SkipLink.tsx` | Skip to main content (accessibility, visible on focus) |

### 2.2 UI Primitives (shadcn/ui)

Located in `components/ui/`:

| Component | File | Purpose |
|-----------|------|---------|
| accordion | accordion.tsx | Collapsible sections |
| alert-dialog | alert-dialog.tsx | Confirmation dialogs |
| alert | alert.tsx | Alert messages |
| aspect-ratio | aspect-ratio.tsx | Aspect ratio container |
| avatar | avatar.tsx | User avatar |
| badge | badge.tsx | Badge/chip |
| breadcrumb | breadcrumb.tsx | Breadcrumb nav |
| button | button.tsx | Button variants |
| calendar | calendar.tsx | Date picker |
| card | card.tsx | Card layout |
| carousel | carousel.tsx | Carousel |
| chart | chart.tsx | Charts (Recharts) |
| checkbox | checkbox.tsx | Checkbox |
| collapsible | collapsible.tsx | Collapsible content |
| command | command.tsx | Command palette |
| context-menu | context-menu.tsx | Right-click menu |
| dialog | dialog.tsx | Modal dialog |
| drawer | drawer.tsx | Drawer/sheet |
| dropdown-menu | dropdown-menu.tsx | Dropdown menu |
| form | form.tsx | Form helpers |
| hover-card | hover-card.tsx | Hover card |
| input-otp | input-otp.tsx | 6-digit OTP input |
| input | input.tsx | Text input |
| label | label.tsx | Form label |
| menubar | menubar.tsx | Menu bar |
| navigation-menu | navigation-menu.tsx | Nav menu |
| pagination | pagination.tsx | Pagination |
| popover | popover.tsx | Popover |
| progress | progress.tsx | Progress bar |
| radio-group | radio-group.tsx | Radio group |
| resizable | resizable.tsx | Resizable panels |
| scroll-area | scroll-area.tsx | Scrollable area |
| select | select.tsx | Select dropdown |
| separator | separator.tsx | Divider |
| sheet | sheet.tsx | Slide-out panel |
| sidebar | sidebar.tsx | Sidebar |
| skeleton | skeleton.tsx | Loading skeleton |
| slider | slider.tsx | Slider |
| sonner | sonner.tsx | Toast notifications |
| switch | switch.tsx | Toggle switch |
| table | table.tsx | Table |
| tabs | tabs.tsx | Tabs |
| textarea | textarea.tsx | Textarea |
| toggle-group | toggle-group.tsx | Toggle group |
| toggle | toggle.tsx | Toggle button |
| tooltip | tooltip.tsx | Tooltip |

### 2.3 Other Components

| Component | Location | Purpose |
|-----------|----------|---------|
| ImageWithFallback | components/figma/ImageWithFallback.tsx | Image with fallback |

---

## 3. Page-by-Page Text Inventory

### 3.1 Homepage (`/`)

**Header:**
- "Cadence" (logo text)
- "Sign In" (link to /login)

**Hero:**
- Trust badge: "Your CRM and copy—one app"
- "Find your rhythm. Close more deals."
- Subtext: "Cadence is your full sales CRM: leads, pipeline, contacts, companies, tasks, and activities—plus on-brand copy. Generate emails, follow-ups, and notes in seconds and save them to the right contact or deal. One place, no tab-switching."
- Buttons: "Get started", "How it works"

**Pillars (CRM features):**
- Leads – Capture & qualify
- Deals – Pipeline & stages
- Contacts – People & history
- Companies – Accounts & orgs
- Tasks – Follow-ups & due dates
- Activities – Calls, meetings, notes

**Section: "All your CRM in one application"**

**Section: "Sales features – Your sales hub"**
- "Pipeline, contacts, and on-brand copy in one app. Manage everything in Cadence—no external CRM, no tab switching."
- Feature cards: Manage leads, Pipeline & deals, Contacts, Companies, Tasks & follow-ups, Activities & timeline, Sales copy, Templates, Send to CRM, History, Brand & settings

**Section: "How It Works"**
- "Create or join a team, generate copy in seconds, then save to the right contact or deal—all inside Cadence."
- Step 1: "Create or join an organization"
- Step 2: "Pick type & goal, get copy"
- Step 3: "Review, tweak, send"

**Section: "Your CRM. Your voice. One app."**
- Sales email, Follow-up, CRM note, Deal message, Workflow message, Templates, History, Send to CRM, Settings

**Section: "Before Cadence vs With Cadence"**
- Without: "Write every email and note from scratch", "Jump between CRM, docs, and email to copy-paste", "Inconsistent tone and hours lost to writing"
- With: "Copy drafted in your brand voice in seconds", "One click saves to the right contact or deal—all inside Cadence", "Consistent, on-brand copy and more time for selling"

**Section: "Why Cadence"**
- Save time, Simple setup, Your brand voice (with descriptions)

**Section: "Simple as 1-2-3, Built to Trust"**
- Clear goal, Secure & in one place, Instant send

**CTA:**
- "Your only CRM—pipeline and copy in one place"
- "No credit card required • Free forever plan"
- "Get Started Free"

**Footer:**
- Cadence tagline, Product links (Dashboard, Leads, Deals, Contacts, Companies, Tasks, Activities, Templates, History, Settings, How it works), Legal (Privacy Policy, Terms of Service, Help Center), Copyright, Twitter, LinkedIn, GitHub

---

### 3.2 Login (`/login`)

- "Back to home"
- "Welcome back"
- "Sign in to Cadence and start creating AI-powered copy" / "Enter your 2FA code to continue"
- "Sign in" / "Create account" (tabs)
- "Name" (label), placeholder "Jane Doe"
- "Email" (label), placeholder "you@company.com"
- "Password" (label)
- "Create account" / "Sign in" (submit)
- "Forgot password?"
- "or" (divider)
- "Try demo (no backend)"
- "Authentication code" (2FA)
- "Verify & sign in"
- "Back"
- "Optional: connect integrations after you sign in"
- "Privacy Policy", "Terms of Service"
- "Having trouble? See help"

---

### 3.3 Help (`/help`)

- "How It Works"
- "A quick guide to getting the most out of Cadence"
- "On this page" (TOC): What Cadence does, Generating copy (4 steps), Your data in Cadence, Data & privacy, Contact support
- **What Cadence Does:** Paragraphs on copy generation and Send to CRM
- **Generating Copy (4 steps):** Pick what you're writing, Set the goal, Generate and tweak, Send to your CRM
- **Your Data in Cadence:** "Cadence is the only CRM you need" / "Send to CRM" saves to Cadence
- **Data & Privacy:** Bullets + "Full Privacy Policy →"
- **Contact Support:** "Something unclear or broken? We're here to help." / "Contact Support"

---

### 3.4 Privacy (`/privacy`)

- "Back to Home"
- "Cadence"
- "Privacy Policy"
- "Last updated: January 28, 2026"
- Sections: Introduction, Information We Collect, How We Use Your Information, Data Security, Data Sharing, Your Rights, Cadence as your CRM, Cookies, International Transfers, Children's Privacy, Changes to This Policy, Contact Us
- Email: privacy@aci.com, Address: 123 Privacy Street, San Francisco, CA 94102

---

### 3.5 Terms (`/terms`)

- "Back to Home"
- "Cadence"
- "Terms of Service"
- "Last updated: January 28, 2026"
- Sections: Agreement to Terms, Use License, Service Description, Acceptable Use, Content Ownership, CRM Integration, Payment Terms, Disclaimers, Limitation of Liability, Indemnification, Termination, Changes to Terms, Governing Law, Contact Information
- Email: legal@aci.com, Address: 123 Legal Street, San Francisco, CA 94102

---

### 3.6 Organizations (`/organizations`)

- "Back"
- "Organizations"
- "Create an organization or open one to continue to onboarding and the dashboard." / "Create an organization or open one you belong to."
- "Your organizations"
- "Owner" / "Member"
- "Open"
- "Create organization"
- Placeholder: "Organization name"
- "Create"
- "Open an organization above to set your company name and brand tone, then go to the dashboard."
- "Loading organizations…"
- "Pending invites"
- "Invited you to join"
- "Accept"
- "Create your first organization above, or ask an owner to invite you by email."

---

### 3.7 Onboarding (`/onboarding`)

- "Set your brand tone"
- "This helps AI generate copy that matches your voice"
- "Company Name"
- Placeholder: "Enter your company name"
- "Brand Tone"
- Options: Professional (Formal, business-focused), Friendly (Warm, approachable), Persuasive (Compelling, action-oriented)
- Examples: "e.g. We are pleased to inform you…", "e.g. Hey! Hope you're having a great day…", "e.g. Consider this: you could save 40%…"
- "Save & Continue"
- "You can change these settings anytime"

---

### 3.8 Dashboard (`/dashboard`)

- "Welcome back, {name}"
- "Here's your CRM at a glance."
- North star: "Total pipeline value" or "Copy generated this week"
- KPI labels: "Copy generated this week", "Saved templates", "Sent to contacts & deals", "Avg. time saved per piece"
- CRM KPIs: "Active leads", "Open deals", "Deals won", "Deals lost"
- "Deal value by stage" (Stage, Deals, Value)
- "Deal value by assignee" (Assignee, Deals, Value)
- "Generate sales copy" / "Pick a format, choose your goal, add context—then generate."
- "Copy type"
- Copy types: Sales Email, Follow-up, CRM Note, Deal Message, Workflow
- "Message goal"
- Goals: Schedule a meeting, Follow up after demo, Request feedback, Share resources, Check in on progress, Close the deal
- "Optional context", placeholder: "e.g. previous conversation, company details, pain points, or key talking points"
- "Add context for more relevant, on-brand copy."
- Length: "Short (2–3 sentences)", "Medium (1 paragraph)", "Long (2+ paragraphs)"
- "Generate copy" / "Generating..."
- "Choose a copy type" / "Select one of the formats above to start generating sales-ready copy."
- "Browse templates"
- "Recent copy activity"
- "No copy sent yet" / "Generate copy from the panel on the left, then send it to a contact or deal."
- "View all copy history"
- "Quick links": Leads, Contacts, Deals, Tasks, Companies, Activities, Templates, Settings

---

### 3.9 GeneratedCopy (`/generated`)

- "Generated Copy"
- "Review and adjust your AI-generated content"
- "Your Copy"
- "Copy" / "Copied!"
- "Send to CRM"
- "Adjust Copy"
- "Sample adjustments; edit the text above to refine."
- "Make shorter", "Make friendlier", "Make more persuasive", "Regenerate"
- Tip: "You can edit the text directly or use the adjustment buttons to refine your copy."

---

### 3.10 SendToCrm (`/send`)

- "No copy to send" / "Generate copy on the dashboard first, then come back here to send it to a contact or deal." / "Go to Dashboard"
- "Send to CRM"
- "Save your generated copy to a contact or deal in Cadence"
- "Select object type"
- Contact (Individual person), Deal (Sales opportunity), Workflow (Automation sequence), Email draft (Save as template)
- "Select specific record"
- Placeholder: "Search contacts...", "Search deals..."
- "No results for ... Try a different search."
- "No records found"
- "Confirm & Send" / "Sending..."
- Demo notice: "In the demo, select Contact or Deal to send your copy. Workflow and Email draft will be available with a full CRM integration."
- "Successfully Sent!"
- "Your copy has been added to your CRM and is ready to use."
- "Copy added to {name} ({type})."
- "Sent to"
- "Create Another", "View History"

---

### 3.11 Templates (`/templates`)

- "Templates"
- "Quick-start templates for common scenarios"
- Categories: Sales, Follow-up, Meetings, Re-engagement
- "Used {n} times"
- "Use template →"
- "How Templates Work"
- "Templates pre-fill the copy generator with common settings and goals. You can still customize everything before generating."
- "Learn more about templates →"

---

### 3.12 History (`/history`)

- "Copy History"
- "View and reuse your previously generated copy"
- Placeholder: "Search your copy history..."
- "{n} items"
- "No results for ... Try a different search."
- "To: {recipient}"
- "Copy", "Copied!"
- "Send again"
- "Regenerate"
- "No copy history yet" / "Copy you send to CRM will appear here. Generate and send your first copy to get started."
- "No results found" / "Try a different search term."
- "Create your first copy"

---

### 3.13 Leads (`/leads`)

- "Leads"
- Search placeholder: "Search by name, email, or phone..."
- "Add lead"
- "No leads yet" / "No results found"
- Edit, Delete, Convert buttons
- Form labels: Full name, Email, Phone, Company, Source, Status
- Placeholders: "Full name", "email@example.com", "Optional"
- Convert dialog: Create contact, Create deal, Create new company, Existing company, etc.
- Create lead, Edit lead, Delete lead dialogs

---

### 3.14 Pipeline / Deals (`/deals`)

- "Deals" / "Pipeline"
- Kanban stages: Qualification, Proposal, Negotiation, Closed Won, Closed Lost
- "No deals yet"
- Add deal, Edit, Delete
- Placeholders: "e.g. Acme Corp - Enterprise", "e.g. $50,000", "e.g. USD"
- Create deal, Edit deal dialogs

---

### 3.15 Tasks (`/tasks`)

- "Tasks"
- Filters: All, Pending, Overdue
- "No tasks yet" / "No tasks match this filter"
- Placeholders: "e.g. Follow up with Acme", "Optional details"
- Form: Title, Description, Due date, Lead, Deal, Assignee
- "Add task"

---

### 3.16 Activities (`/activities`)

- "Activities"
- Filters: All, By contact, By deal
- "Log activity"
- Types: Call, Meeting, Email, Note
- Placeholders: "e.g. Discovery call – discussed pricing", "Notes or summary"
- "No activities yet"

---

### 3.17 Contacts (`/contacts`)

- "Contacts"
- Search placeholder: "Search by name, email, or phone..."
- "Add contact"
- "No contacts yet" / "No results found"
- Form: Full name, Email, Phone, Job title, Company

---

### 3.18 Companies (`/companies`)

- "Companies"
- Search placeholder: "Search by name..."
- "Add company"
- "No companies yet" / "No results found"
- Form: Company name, Domain, Industry, Size
- Placeholders: "e.g. acme.com", "e.g. Technology", "e.g. 50-200"

---

### 3.19 Settings (`/settings`)

- "Settings"
- "Manage your account and preferences"
- **Brand:** Company Name, Brand Tone (Professional, Friendly, Persuasive)
- **Organization:** "Cadence is your only CRM", "All data stays in Cadence — no external CRM required", "Switch organization", "Manage organizations", "Invite by email", "Pending invites", "Join requests", "Organization members"
- Roles: Owner, Member, Manager
- "Invite", "Sending…"
- **Security:** "Two-factor authentication (2FA)", "Enabled" / "Optional (recommended)", "Enable" / "View setup"
- 2FA steps, "Confirm & enable 2FA", "2FA is enabled. You'll be asked for a code when signing in.", "Disable 2FA"
- **Account:** "Logout", "Delete Account"
- Delete confirm: "I understand my data will be removed and this cannot be undone.", "Type DELETE to confirm"
- "Need help?"

---

## 4. AppHeader Text

- Logo: "Cadence"
- Search placeholder: "Search leads, contacts, companies, deals..."
- "Searching..."
- "No results"
- Sections: Leads, Contacts, Companies, Deals
- User dropdown: "{name}", Settings, "Sign out"
- "{org name} — Switch org"
- Mobile: Settings, Sign out

---

## 5. DemoBanner Text

- "Demo mode — You're viewing sample data. Data is not saved to a backend. Connect an API or sign in with a real account to use your own data."

---

## 6. SkipLink Text

- "Skip to main content"

---

## 7. API Messages (Toast & Validation)

From `api/messages.ts`:

**Copy:**
- "Copy generated."
- "Copy sent to CRM."
- "Copied to clipboard."

**Settings:**
- "Settings saved."

**Success:**
- leadCreated, leadUpdated, leadDeleted, leadConverted
- dealCreated, dealUpdated, dealDeleted, dealMoved
- companyCreated, companyUpdated, companyDeleted
- contactCreated, contactUpdated, contactDeleted
- taskCreated, taskUpdated, taskDeleted
- activityLogged, activityDeleted

**Errors:**
- "Something went wrong. Please try again."
- "Failed to load. Please try again."

**Validation:**
- "Name is required."
- "Name and email are required."
- "Name and value are required."
- "Title is required."
- "Subject or body are required."
- "Select at least one: Create contact or Create deal."

**Auth:**
- "Signed in."
- "Account created."
- "Demo mode"
- "Two-factor code required"

**2FA:**
- "2FA enabled."
- "2FA disabled."
- "Scan the secret in your authenticator, then confirm the code."

**Task:**
- "Task completed."
- "Task reopened."

---

## 8. EmptyState Usage

EmptyState is used with these titles/descriptions:

| Page | Title | Description | Action |
|------|-------|-------------|--------|
| Dashboard | Choose a copy type | Select one of the formats above... | Browse templates |
| Dashboard | No copy sent yet | Generate copy from the panel... | Browse templates |
| SendToCrm | No copy to send | Generate copy on the dashboard first... | Go to Dashboard |
| History | No copy history yet | Copy you send to CRM will appear here... | Create your first copy |
| History | No results found | Try a different search term. | — |
| Leads | No leads yet | Add your first lead to get started | Add lead |
| Companies | No companies yet | Add your first company | Add company |
| Contacts | No contacts yet | Add your first contact | Add contact |
| Activities | No activities yet | Log a call, meeting, or note | Log activity |
| Tasks | No tasks yet | Add your first task | Add task |
| Pipeline | No deals yet | Create your first deal | Add deal |

---

*End of report. For routing and API details, see FRONTEND_PAGES_REPORT.md.*
