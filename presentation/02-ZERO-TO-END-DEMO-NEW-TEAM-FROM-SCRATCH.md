# Zero to End Demo — New Team From Scratch

**Cadence Cloud CRM** — Presentation Report 2

*Latest: Demo includes Organizations (Demo Organization, Acme Corp, TechStart Inc) → Open → Onboarding → Dashboard. Button label: "Try demo (no backend)".*

---

## Contents

1. [At a glance](#at-a-glance)  
2. [What “From scratch” means (cloud)](#1-what-from-scratch-means-in-the-cloud)  
3. [Option A — Try the demo (sandbox)](#2-option-a--try-the-demo-sandbox-in-the-cloud)  
4. [Option B — Sign up (cloud account)](#3-option-b--sign-up-and-use-your-cloud-account)  
5. [Zero to end — Using the system](#4-zero-to-end--using-the-system-as-a-new-user)  
6. [“Team” from scratch](#5-team-from-scratch--current-reality)  
7. [Troubleshooting](#6-troubleshooting)  
8. [Quick reference](#7-quick-reference)

---

## At a glance

| | |
|---|--|
| **How it works** | Cadence runs **in the cloud**. You open the app in your browser, sign in to your cloud account, and your data is stored securely in the cloud. **Access from anywhere** — no installs. |
| **Two ways to start** | **(1) Try the demo** — click "Try demo (no backend)" for a sandbox with sample data (no sign-up). **(2) Sign up** — create your cloud account and use the full CRM with your own data. |
| **After you’re in** | **Demo:** Organizations (pick one or create) → Open → Onboarding → Dashboard. **Account:** Set company name and brand tone (Onboarding) → add companies, leads, deals (and optionally contacts, tasks, activities) → generate copy → send to a contact or deal. You can convert leads to contacts/deals in one step. All in the cloud. |
| **Today** | Create or join an organization; open an org to work in that org's CRM. Owner can invite by email and accept/reject join requests. Data is scoped by organization; all members of an org see that org's data. |

---

## 1. What “From Scratch” Means (in the cloud)

> **In short:** You can start using Cadence in the cloud from day one. No servers to run, no database to install — everything runs in the cloud.

| Option | What you get | What you do |
|--------|--------------|-------------|
| **Try the demo** | The cloud app with sample data. No sign-in required. Nothing saved to your account. | Open the Cadence cloud app → click “Try demo (no backend)” → Organizations → pick an org → Open → Onboarding → Dashboard. |
| **Sign up (cloud account)** | Your own cloud account. Real sign-in, saved leads, deals, contacts, tasks, activities, copy history. Everything stored in the cloud. | Open the Cadence cloud app → Create account (name, email, password) → use the full CRM. |

Use **Try the demo** to explore. Use **Sign up** when you’re ready to use the cloud CRM with your own data.

---

## 2. Option A — Try the Demo (Sandbox in the Cloud)

**Goal:** See the cloud CRM in under 2 minutes. No account required.

### Steps

1. Open the **Cadence cloud app** in your browser (any device).
2. On the login page, click **“Try demo (no backend)”** (sandbox).
3. You land on **Organizations** with three options: **Demo Organization**, **Acme Corp**, and **TechStart Inc**. Click **Open** on any one (or **Create** a new organization), then you go to Onboarding.
4. Complete **Onboarding**: enter your company name and brand tone (Professional / Friendly / Persuasive), then **Save & Continue**.
5. You land on the **Dashboard** with sample data (Leads, Deals, Tasks, Activities, Contacts, Companies, Templates, History, Send to CRM, Settings).

> **In short:** No sign-up. Sample data isn’t saved to your account; it’s just for exploring. When you’re ready, sign up for your own data.

### What you can try

- **Generate copy:** Dashboard → pick type and goal → Generate copy → edit on Generated Copy → Send to CRM (pick a contact or deal from the sample list).
- **Leads:** List, search, add, edit, delete, or **convert** a lead to a contact and/or deal.
- **Pipeline:** View deals by stage, move stages, add a new deal, edit, delete.
- **Tasks:** List (All / Pending / Overdue), add, edit, mark complete.
- **Activities:** List, filter by contact or deal, log a call/meeting/email/note.
- **Contacts and Companies:** List, search, add, edit.
- **Templates:** Click “Use template” on a card → Dashboard opens with type and goal already set.
- **History:** Search, Copy to clipboard, Regenerate, Send again.
- **Settings:** Change company name and tone, optional integrations, turn 2FA on/off, Logout, Delete account.

---

## 3. Option B — Sign Up and Use Your Cloud Account

**Goal:** Use the full cloud CRM with your own data, stored securely in the cloud.

### Steps

1. Open the **Cadence cloud app** in your browser.
2. Click **Get started** or **Sign In**.
3. **Create account** — enter name, email, password. (Or sign in if you already have an account.)
4. Optional: 2FA — turn on for extra security.
5. **Organizations:** Create a new organization or accept an invite, then **Open** an org. (Required after login when using the real API.)
6. **Onboarding:** Enter your **Company name** and **Brand tone** (Professional / Friendly / Persuasive). Click **Save & Continue**.
7. You land on the **Dashboard**. Your data is stored in the cloud — access from any browser.

> **In short:** Sign up → optional Connection → Onboarding (company + tone) → Dashboard. Everything runs in the cloud; no installs.

---

## 4. Zero to End — Using the System as a New User

*Once you’re in (Try demo (no backend) or your cloud account), this is the path from first login to full usage.*

### First-time setup (one-time)

> **In short:** Open the Cadence cloud app → Sign in or Sign up → Organizations (create/join/open org) → set your company name and brand tone → you land on the Dashboard.

1. **Sign in or register** (or use Try demo (no backend) to explore). Create account (Name, Email, Password) or Sign in. If 2FA is on, enter the 6-digit code.
2. **Organizations:** Create a new organization or accept a pending invite, then **Open** the org you want to work in.
3. **Onboarding:** Enter your **Company name** (e.g. “Acme Inc”) and choose **Brand tone** (Professional / Friendly / Persuasive). Click **Save & Continue**.
4. You land on the **Dashboard** in the cloud. You’ll see stats and “Create your content”.

**Note:** Frontend supports organizations: create org, accept invite, open org; org switcher in Settings and AppHeader. Owner can invite by email and accept/reject join requests. Data is scoped by organization when X-Organization-Id is set.

### Build your base data (recommended order)

Do this so you have companies, leads, and deals (and optionally contacts, tasks, activities) before generating and sending copy.

| Order | What to do | Example |
|-------|------------|---------|
| 1 | **Companies** — Add the companies you sell to. (Companies → **Add company**) | “Acme Corp”, “TechStart Inc” |
| 2 | **Leads** — Add leads: name, email, phone (optional), company, source (e.g. website), status (e.g. New). (Leads → **Add lead**) | “Alex Turner”, alex@example.com, Acme Corp, website, New |
| 3 | **Pipeline** — Create deals: name, value, stage, expected close date (optional), company. (Pipeline → **New deal**) | “Acme Corp - Enterprise Plan”, $50,000, Qualification |
| 4 | **Contacts** — Add contacts or **convert a lead** to contact (and optionally deal) from Leads → **Convert**. | Convert “Alex Turner” → Create contact + Create deal |
| 5 | **Tasks** — Add tasks with due date; optionally link to lead or deal. (Tasks → **Add task**) | “Follow up with Acme”, due tomorrow, link to deal |
| 6 | **Activities** — Log calls, meetings, emails, notes; optionally link to contact or deal. (Activities → **Log activity**) | Call, “Intro call”, “Discussed pricing”, link to deal |

### Core workflow — Copy and send

1. **Dashboard** → Pick copy type and goal, add optional context, choose length → **Generate copy**.
2. **Generated Copy** → Edit if needed, **Copy** to clipboard, or **Adjust Copy** (shorter, friendlier, persuasive, or **Regenerate**) → **Send to CRM**.
3. **Send to CRM** → Choose Contact or Deal, search and select one → **Confirm & Send**.
4. **History** → See everything you’ve sent; use **Copy**, **Regenerate**, or **Send again**.

> **Tip:** Use **Templates** → pick a card → **Use template** so the Dashboard opens with type and goal already set.

### Ongoing use

- **Dashboard:** Check stats (copy and CRM) and recent activity; generate more copy.
- **Leads, Pipeline, Tasks, Activities, Contacts, Companies:** Update daily (new leads, move deals, complete tasks, log activities).
- **Settings:** Change brand, optional integrations, 2FA, logout, or delete account. All in the cloud.

---

## 5. “Team” From Scratch — Current Reality

> **In short:** Frontend has organizations: create/join org, org switcher, X-Organization-Id. Owner can invite by email and accept/reject join requests. Data is scoped by organization.

| Question | Answer |
|----------|--------|
| Can I invite teammates? | Yes. In Settings, Organization section: owner can invite by email; pending invites and join requests with Accept/Reject. |
| Can we share the same leads/deals? | Yes. Create or join an organization, then open that org. All CRM data is scoped by the current organization. |
| How do we “start as a team”? | Today: each person creates an account and has their own data. Target: create or join an organization, then work in that org's CRM. |
| Will there be roles (admin/manager/rep)? | Owner can invite and accept/reject join requests. Owner, Member (Salesperson), and Manager roles; pipeline/stage/lead-source/lead-status config restricted to Owner/Manager. All org members share the same CRM data. |

So “new team from scratch” today means: **each member signs up and uses the cloud CRM for their own pipeline**. Frontend has full org flow (create/join, switcher, X-Organization-Id).

---

## 6. Troubleshooting

| Problem | What to try |
|---------|-------------|
| **I want my own data but the app only shows “Try demo (no backend)”** | You need to **Sign up** (Create account) to use the cloud CRM with your own data. “Try demo (no backend)” is a sandbox with sample data only. |
| **I get “401 Unauthorized” when using the app** | You must be signed in with your cloud account. Don’t use “Try demo (no backend)” (sandbox) when you want your own data. If your session expired, sign in again. |
| **I can’t access the app** | Make sure you’re using the correct Cadence cloud app URL and that you have an internet connection. The cloud CRM requires network access. |
| **I forgot my password** | Use the “Forgot password” or “Reset password” link on the login page (if available). Otherwise contact your administrator. |
| **I want to use the app on another device** | Open the Cadence cloud app in your browser on that device and sign in with the same cloud account. Your data is in the cloud — access from anywhere. |

---

## 7. Quick reference

| Goal | What to do |
|------|------------|
| **See the app in 1–2 minutes** | Open the **Cadence cloud app** in your browser → click **"Try demo (no backend)"** (when no API is configured) → **Organizations** (Open or Create) → **Onboarding** (company + tone) → **Dashboard**. No sign-up; sample data is not saved to your account. (Self-host: see RUN_FROM_SCRATCH.md.) |
| **Use the CRM with your own data** | Open the Cadence cloud app → **Create account** (or Sign in) → Organizations (create/join/open org) → Onboarding (company + tone) → Dashboard. Your data is stored in the cloud — access from any browser. |
| **First-time user path** | Sign up → Organizations (create/join/open org) → Onboarding (company + tone) → Dashboard → add Companies, Leads, Deals (and optionally convert leads, add Tasks, Activities) → generate copy → send to CRM. |
| **Convert a lead** | Leads → **Convert** on a lead → choose Create contact and/or Create deal (with deal name, value, stage) → **Convert**. |
| **Run from scratch (advanced)** | For self-host or local development, see [RUN_FROM_SCRATCH.md](../RUN_FROM_SCRATCH.md), [LOCAL_DEV.md](../LOCAL_DEV.md). |

---

*Presentation report 2. Last updated: February 2026.*
