# How the System Works & How Sales Will Use It

**Cadence Cloud CRM** — Presentation Report 1

---

## Contents

1. [At a glance](#at-a-glance)  
2. [What the system is](#1-what-the-system-is-in-plain-terms)  
3. [How the system works](#2-how-the-system-works-under-the-hood)  
4. [Copy types, goals, brand tone](#3-copy-types-goals-and-brand-tone)  
5. [How sales use it — day to day](#4-how-sales-will-use-it--day-to-day)  
6. [Example sales scenarios](#5-example-sales-scenarios)  
7. [Summary for sales](#6-summary-for-sales)

---

## At a glance

| | |
|---|--|
| **What it is** | **Cadence is the only CRM** — no copy from, import to, or sync with other CRMs. This system handles everything: leads, deals, contacts, tasks, activities, and copy. Runs in the cloud; access from anywhere; no installs. |
| **Main workflow** | Sign in → **create an organization** or **join an organization** → **open an organization** → work in that org’s CRM (one salesperson with full access to that org’s data). Optional: set company name and brand tone per org. Everything stays in Cadence. |
| **Who uses it** | Sales reps. You can be in several organizations; in each org you work as one salesperson with that org’s data. No external CRM. |
| **Cloud CRM** | Cadence runs in the cloud. Your data is stored in Cadence only. The system is **not** based on or connected to Salesforce, HubSpot, or any other CRM. |
| **Best for** | Teams that want one place in the cloud to generate copy and manage their pipeline, with access from any device. |

---

## 1. What the System Is (in plain terms)

Cadence is the **only CRM** you use for this workflow. It does **three things** for sales:

1. **Writes copy for you** — Emails, follow-ups, CRM notes, and deal messages. You pick the type and goal (e.g. “Schedule a meeting” or “Close the deal”); the system drafts text using your company name and the tone you chose (Professional, Friendly, or Persuasive).

2. **Holds your organization's sales data** — Leads, deals (in a pipeline with stages), contacts, companies, tasks, and a log of calls/meetings/emails (activities). You can convert a lead into a contact and/or a deal in one step. All of this lives **only in this system** — no copying to or importing from other CRMs.

3. **Attaches copy to a contact or deal** — You pick who the copy is for; the system saves it in **History** (in this app) so you can reuse it, edit it, or send it again. "Send to CRM" here means "save and attach to a contact or deal **in Cadence**."

> **Cloud CRM means:** Cadence runs in the cloud. You open the app in your browser and sign in to your cloud account. Your data and copy are stored securely in the cloud. You don't connect it to Salesforce, HubSpot, or any other CRM — Cadence is your CRM. Access from anywhere; no installs.

### 1.1 How the system should work (intended flow)

- **After login:** You **create an organization** or **join an organization** (e.g. accept an invite or have your join request accepted). When you **open an organization**, you go to the CRM part of that organization — you are one salesperson and can do all interactions with that org’s data (leads, deals, contacts, tasks, activities, copy).
- **Only this system:** There is **no** copy from another CRM, and **no** import or send to another CRM. Cadence is the only CRM; it handles everything. The system is not based on or connected to other CRMs.
- **Different organizations:** You can join different organizations and work in each. In each org you see only that org’s data; you switch by opening the org you want.
- **Owner:** Each organization has an **owner**. The owner can **accept or reject** requests to join the org, and can **invite** other sales agents by their email address.

**Today (current UI):** The frontend implements the full org flow: create or join an organization at `/organizations`, open an org, switch org in Settings or AppHeader; owners can invite by email and accept or reject join requests. Data is scoped by the current organization when you have an org open.

### 1.2 Cloud account and data

- **Access from anywhere:** Open the Cadence cloud app in any browser and sign in — your data is stored in the cloud. No installs.
- **No external CRM:** Cadence is your CRM; all data stays in Cadence. No Salesforce, HubSpot, or other CRM connection.

---

## 2. How the System Works (under the hood)

*This section is for readers who want to understand the technical building blocks. If you only care about using the app, you can skip to section 3 or 4.*

**In short:** Cadence runs in the cloud: a **web app** (what you see in the browser), a **cloud API** (login, data, Intelligent Sales Writer), and a **cloud database** (your data stored securely). When you’re signed in, you only see your own data.

### 2.1 Web app (frontend)

- **Pages:** Homepage, Login, Organizations, Onboarding, Dashboard, Generated Copy, Send to CRM, Leads, Pipeline (deals), Tasks, Activities, Contacts, Companies, Templates, History, Settings, Help, Privacy, Terms.
- **Navigation:** After sign-in, a sticky header shows links to Dashboard, Leads, Pipeline, Tasks, Activities, Contacts, Companies, Templates, History, Help; your profile menu has Settings and Sign out.
- **Try the app:** You can click “Try the app” (sandbox) to explore with sample data in the cloud; nothing is saved to your account.

### 2.2 Cloud API (backend)

- **Sign in:** Sign up or sign in with email and password. You can turn on 2FA (a 6-digit code from an app like Google Authenticator). The cloud API gives you a token that the web app sends with every request.
- **Your data only:** The cloud API ties every record to your user account and, when an organization context is sent (`X-Organization-Id` header), to that organization. Without the header, you see only your own (legacy) data. Only you see your data; org-scoped data is visible only to members of that org.
- **Intelligent Sales Writer:** The system uses built-in templates to generate text (no external AI service required). You can later swap this for an AI/LLM if you want.

### 2.3 Cloud database

- **Stored in the cloud:** Users, settings (company name, brand tone), organizations and memberships (target vision), companies, contacts, deals, leads, tasks, activities, templates, and copy history. All of this is stored securely in the cloud. No external CRM connection — data stays in Cadence.
- **Access from anywhere:** Your data syncs when you’re online; access from any browser. No local install.

### 2.4 Which page talks to what (for technical readers)

| Page | What it loads or does |
|------|------------------------|
| Login | Sign up, sign in, 2FA step; or Try the app (sandbox). |
| (No Connection page) | All data stays in Cadence — no external CRM. Connection removed. |
| Onboarding | Save company name and brand tone. |
| Dashboard | Load settings, templates, copy stats, CRM stats (leads, deals, pipeline value, won/lost), recent copy history; generate copy. |
| Send to CRM | Load contacts and deals; send copy to the chosen contact or deal. |
| Leads | Load leads and companies; create, edit, delete, or convert leads. |
| Pipeline | Load deals and companies; create, edit, delete deals; move deal stage. |
| Tasks | Load tasks (and leads/deals for dialogs); create, edit, complete tasks. |
| Activities | Load activities, contacts, deals; create activities; filter by contact or deal. |
| Contacts | Load contacts (and companies for dialogs); create, edit contacts. |
| Companies | Load companies; create, edit companies. |
| Templates | Load templates. |
| History | Load copy history. |
| Settings | Load/save settings (brand, 2FA), optional integrations, logout, delete account. |

---

## 3. Copy Types, Goals, and Brand Tone

*When you generate copy, you choose **what** you’re writing (type), **why** you’re writing it (goal), and **how** it should sound (brand tone).*

### 3.1 Copy types (what you’re writing)

| Type | What it’s for |
|------|----------------|
| **Sales Email** | Outreach and first contact. |
| **Follow-up** | Keeping the conversation going after a call or demo. |
| **CRM Note** | Notes you log in the CRM (calls, meetings, emails). |
| **Deal Message** | Updates to stakeholders on a deal. |
| **Workflow** | Automated or sequence-style messages. |

### 3.2 Message goals (why you’re writing)

You pick one goal so the copy is focused:

- Schedule a meeting  
- Follow up after demo  
- Request feedback  
- Share resources  
- Check in on progress  
- Close the deal  

### 3.3 Brand tone (how it should sound)

You set this once in Onboarding or Settings. It’s used for all generated copy:

- **Professional** — Formal and concise.  
- **Friendly** — Warm and approachable.  
- **Persuasive** — Confident and action-oriented.  

Your **company name** is also used in the copy (e.g. “how [Your Company] can help…”).

### 3.4 Templates (quick start)

Templates are pre-set combinations of type + goal. Examples: “First contact email”, “Follow-up after meeting”, “Demo reminder”, “Closing deal message”, “Re-engagement email”. When you click **Use template** on the Templates page, the Dashboard opens with that type and goal already selected so you can generate right away.

---

## 4. How Sales Will Use It — Day to Day

### 4.1 First-time setup (one-time)

> **In short:** Open the Cadence cloud app → **Sign in** or **Sign up** → **Organizations** (create or join org, then open org) → set your **Company name** and **Brand tone** (Onboarding) → you land on the **Dashboard** in the cloud.

| Step | What you do |
|------|-------------|
| 1 | Open the **Cadence cloud app** in your browser and click **Sign In** or **Get started**. |
| 2 | **Create account** (name, email, password) or **Sign in**. If 2FA is on, enter the 6-digit code. Or click **Try the app** (sandbox) if you’re to explore with sample data. |
| 3 | **Organizations:** Create an organization or accept an invite, then **Open** the org you want. |
| 4 | **Onboarding:** Enter your **Company name** and choose **Brand tone** (Professional / Friendly / Persuasive). Click **Save & Continue**.
| 5 | You land on the **Dashboard** in the cloud (stats, "Create your content"). Your data is stored in the cloud — access from any browser. |

### 4.2 Generating and sending copy (the main workflow)

> **In short:** Pick type and goal on the Dashboard → generate → edit if needed on Generated Copy → send to a contact or deal → it’s saved in History.

| Step | What you do |
|------|-------------|
| 1 | On the **Dashboard**, select a **copy type** (Sales Email, Follow-up, CRM Note, Deal Message, or Workflow). |
| 2 | Choose a **Message goal** from the dropdown (e.g. “Schedule a meeting”, “Close the deal”). |
| 3 | Optionally add **context** (e.g. prospect name or deal name) so the copy is more specific. |
| 4 | Choose **length**: Short, Medium, or Long. |
| 5 | Click **Generate copy**. You’re taken to the **Generated Copy** page with the draft. |
| 6 | On **Generated Copy**: read and edit the text. You can **Copy** to clipboard, or use **Adjust Copy** (Make shorter, friendlier, or more persuasive), or **Regenerate** to start over with the same context. |
| 7 | When you’re happy, click **Send to CRM**. |
| 8 | On **Send to CRM**: choose **Contact** or **Deal**, search and select the person or deal, then click **Confirm & Send**. The copy is saved in History. |
| 9 | After sending: **Create Another** (back to Dashboard) or **View History**. |

> **Tip:** Use **Templates** to skip steps 1–2. Go to Templates → pick one (e.g. “First contact email”) → **Use template** → the Dashboard opens with type and goal already set → generate as above.

---

### 4.3 Leads

> **In short:** You see a list of leads (name, email, company, status, source). You can search, add, edit, delete, or **convert** a lead into a contact and/or a deal.

| What you do | How |
|-------------|-----|
| **View leads** | Open Leads. You see name, email, company, status, source. Use the search box to filter by name or email. |
| **Add a lead** | Click **Add lead**. Enter name, email, phone (optional), company (dropdown), source (e.g. website, referral), status (e.g. New, Contacted). Save. |
| **Edit or delete** | Click **Edit** or **Delete** on a row. Delete asks for confirmation. |
| **Convert a lead** | Click **Convert** on a lead. Choose “Create contact” and/or “Create deal”. If you create a deal, enter deal name, value, and stage (e.g. Qualification). Click **Convert**. The lead’s name, email, phone, and company are used for the new contact and/or deal. |

*Status examples:* New, Contacted, Qualified, Lost. *Source examples:* website, referral, events, ads, manual.

---

### 4.4 Pipeline (deals)

> **In short:** You see deals in columns by stage (Qualification → Proposal → Negotiation → Closed Won / Closed Lost). You can move deals between stages, add new deals, edit, or delete.

| What you do | How |
|-------------|-----|
| **View pipeline** | Open Pipeline (Deals). Columns: Qualification, Proposal, Negotiation, Closed Won, Closed Lost. Each deal card shows name, value, and a stage dropdown. |
| **Move a deal** | Use the stage dropdown on the card. If you move to Closed Won or Closed Lost, the deal is marked won or lost. |
| **New deal** | Click **New deal**. Enter name, value, stage, expected close date (optional), company. Save. |
| **Edit or delete** | Click **Edit** or **Delete** on a card. Delete asks for confirmation. |

*Pipeline value and won/lost counts appear on the Dashboard.*

---

### 4.5 Tasks

> **In short:** You see tasks with due dates. You can filter by All, Pending, or Overdue; add or edit tasks; and mark them complete. Tasks can be linked to a lead or deal.

| What you do | How |
|-------------|-----|
| **View tasks** | Open Tasks. Filter: All / Pending / Overdue. Each row has a checkbox (complete), title, due date, and Edit. |
| **Add a task** | Click **Add task**. Enter title, description, due date; optionally link to a lead or deal. Save. |
| **Edit or complete** | Click **Edit** to change details, or use the checkbox to mark complete. |

### 4.6 Activities

**In short:** You log calls, meetings, emails, and notes. You can filter by contact or by deal to see a timeline of activity.

| What you do | How |
|-------------|-----|
| **View activities** | Open Activities. Filter: All, By contact, or By deal (then pick a contact or deal). You see type, subject, body, and date. |
| **Log activity** | Click **Log activity**. Choose type (call, meeting, email, note), enter subject and body; optionally link to a contact or deal. Save. |

---

### 4.7 Contacts and companies

> **In short:** **Contacts** are people (name, email, phone, company). You use them when sending copy to CRM and when logging activities. **Companies** are accounts you sell to; you pick a company when creating leads and deals.

| What you do | How |
|-------------|-----|
| **Contacts** | Open Contacts. List and search by name, email, or phone. **Add contact** or **Edit** on a row. Contacts appear when you send copy to CRM and when you filter or link activities. |
| **Companies** | Open Companies. List and search. **Add company** or **Edit**. Companies appear in the company dropdown when you create or edit leads and deals. |

### 4.8 Dashboard stats and recent activity

**In short:** The Dashboard shows how much copy you’ve generated and sent, plus CRM numbers (leads, deals, pipeline value, won/lost) and the last 5 items you sent to CRM.

| What you see | Meaning |
|--------------|---------|
| Generated this week | How many pieces of copy you generated this week. |
| Templates | Number of templates available. |
| Sent to CRM | How many times you’ve sent copy to a contact or deal. |
| Active leads | Number of leads (not deleted). |
| Active deals | Number of deals (not closed). |
| Pipeline value | Sum of deal values in the pipeline. |
| Deals won / lost | Count of closed-won and closed-lost deals. |
| Recent activity | Last 5 items sent to CRM (type, recipient, date). “View full history” opens History. |

### 4.9 History

**In short:** History lists every piece of copy you’ve sent to a contact or deal. You can search, copy to clipboard, **Regenerate** (back to Dashboard with context), or **Send again** (back to Send to CRM with that copy).

| What you do | How |
|-------------|-----|
| **View history** | Open History. You see type, copy snippet, recipient name, recipient type, and date. |
| **Search** | Use the search box to filter by copy text, type, or recipient name. |
| **Copy** | Click **Copy** on a row to copy the full text to your clipboard. |
| **Regenerate** | Click **Regenerate** to go to the Dashboard with that copy’s context so you can generate a new version. |
| **Send again** | Click **Send again** to go to Send to CRM with that copy so you can send it to another contact or deal. |

---

### 4.10 Settings and account

> **In short:** Settings lets you change your company name and brand tone, switch organization, (for owner) invite members or accept/reject join requests, turn 2FA on or off, log out, or delete your account.

| What you do | How |
|-------------|-----|
| **Brand** | Change company name and brand tone (Professional / Friendly / Persuasive). Click **Save Changes**. |
| **Organization** | Switch current org; link to Manage organizations. For owner: invite by email, accept/reject join requests. (No Connection page — Cadence is the only CRM.) |
| **2FA** | Enable: follow the setup (scan QR or enter secret), enter 6-digit code, **Confirm & enable 2FA**. Disable: enter password and 6-digit code, **Disable 2FA**. |
| **Logout / Delete account** | **Logout** takes you to Login. **Delete account** requires two clicks to confirm, then you’re redirected to the Homepage. |

---

## 5. Example sales scenarios

*How a rep might use the app in real situations.*

| Scenario | What you do |
|----------|-------------|
| **First thing Monday** | Open Dashboard → check CRM stats and recent activity → open Tasks (filter Pending/Overdue) → complete or reschedule → open Leads or Pipeline and update statuses → use Templates to generate a follow-up or deal message → send to contact or deal. |
| **After a demo** | Dashboard → pick Follow-up + “Follow up after demo” → add context (prospect name) → generate → edit if needed → Send to CRM → pick the deal → confirm. Then log a meeting in Activities linked to that deal. |
| **Closing a deal** | Pipeline → move deal to Negotiation or final stage → generate Deal Message, goal “Close the deal” → send to deal → after close, move the deal to Closed Won. |
| **New lead from website** | Leads → Add lead (name, email, company, source: website, status: New). Later: **Convert** → Create contact + Create deal (name, value, stage) → contact and deal appear in Contacts and Pipeline. |
| **Re-engagement** | Templates → “Re-engagement email” → Use template → add context → generate → send to contact or deal. Use History → “Send again” if you want to reuse the same copy for someone else. |

---

## 6. Summary for sales

> **In one sentence:** Cadence is the only CRM: after login you create or join an organization, open an org, then work in that org’s CRM (generate copy, manage leads, deals, contacts, tasks, activities); everything stays in Cadence, access from any browser. No copy from or import to other CRMs.

**Key points:**

- **Only this system:** The system is **not** based on or connected to other CRMs. No copy from, import to, or sync with Salesforce, HubSpot, or any other CRM. Cadence handles everything.
- **Organizations:** After login you **create an organization** or **join an organization**. When you **open an organization**, you work in that org's CRM as one salesperson with full access to that org's data. You can be in several orgs and switch by opening the org you want.
- **Owner:** Each org has an **owner** who can **accept or reject** requests to join and **invite** other sales agents by email.
- **Cloud CRM:** Cadence runs in the cloud. You sign in from any browser; your data is stored in Cadence only.
- **Copy:** One place to generate (Dashboard + Templates), edit (Generated Copy), and attach to a contact or deal (Send to CRM = save in Cadence). History keeps everything so you can copy, regenerate, or send again.
- **Sales data:** Leads (with convert to contact/deal), Pipeline (stages, value, won/lost), Tasks, Activities, Contacts and Companies. All data lives only in Cadence, scoped per organization when you have an org open.
- **Security:** Login with email and password; optional 2FA. The frontend implements the full org flow (create/join, open org, switcher in Settings and AppHeader); backend supports it.

For more technical detail (flows, backend, database), see [USER_FLOWS_REPORT.md](../src/app/reports/USER_FLOWS_REPORT.md), [FLOWS_BACKEND_DATABASE_VERIFICATION.md](../src/app/reports/FLOWS_BACKEND_DATABASE_VERIFICATION.md), and [PROJECT_ASPECTS.md](../PROJECT_ASPECTS.md).

---

*Presentation report 1. Last updated: February 2026.*
