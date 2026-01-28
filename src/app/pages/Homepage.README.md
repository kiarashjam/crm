# Homepage

**Route:** `/`  
**File:** `Homepage.tsx`

## Purpose

Public landing page: hero, value proposition, feature highlights, and CTAs to sign in or help.

## Behavior

- Sticky header with ACI logo and “Sign In” link to `/login`.
- Hero: trust badge (“AI copy for your CRM”), headline (“Write sales copy in seconds, not hours”), subtext, “Get started” (→ `/login`) and “See how it works” (→ `/help`).
- Stats row: “5” copy types, “Templates” ready-to-use, “1-click” send to CRM.
- “How It Works”: 3 steps (Connect your CRM, Generate copy, Send to CRM).
- “What You Can Do”: 10 feature cards (Sales email, Follow-up, CRM note, Deal message, Workflow, Templates, History, Send to CRM, Connection, Settings).
- “Why Choose ACI”: Save time, Simple setup, Your brand voice.
- CTA strip: “Start writing in minutes”, “Get Started Free” (→ `/login`), “No credit card required • Free forever plan”.
- Footer: ACI blurb, Product nav (How it works, Templates, Dashboard, History, Settings, Connect CRM), Legal (Privacy, Terms, Help Center), © 2026, social (Twitter, LinkedIn, GitHub).
- Uses `MAIN_CONTENT_ID` for skip link; no auth required.

## API / Data

None. Static content and navigation only.
