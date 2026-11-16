# Privacy

**Route:** `/privacy` (and `/privacy?form=<token>` from a club's public form)  
**File:** `Privacy.tsx`

## Purpose

The privacy notice, under the Swiss nFADP and, for people in the EU and EEA, the GDPR. Its facts
come from the server, never from the page: who runs the app, who answers for a club's data, the
services the data goes to, where it is kept, how long, and how to use each right.

## Behavior

- Custom header: “Back to Home” (→ `/`), Cadence logo (→ `/`). No AppHeader.
- Opened from a club's capture or application form (`?form=`), it names that club as the
  controller (its privacy settings, or the workspace's name), the operator who runs the app for
  it, the club's retention periods and the consent wording its forms show. Opened on its own, it
  names the operator and says each club sets its own periods.
- Lists only the processors actually used: Microsoft Azure always; SendGrid when it sends the
  mail; OpenAI (United States, with what is and is not sent) when the AI writer is configured;
  Stripe and Lightspeed when the club connected them; and, by kind rather than address, what the
  club connected itself (its webhook or member portal, and systems holding its API key).
- The consent wording printed is the one the form shows: the workspace's for the enquiry form,
  the application form's own for an application.
- Says what the browser keeps: a bearer sign-in token in local storage for the session length
  the server states, the person's name and email, the workspace per tab, preferences and a few
  working notes; no advertising or tracking cookies.
- The rights (access and a portable copy, correction, erasure, objection, withdrawing consent),
  where to write, the 30-day answer, the 10-year bookkeeping exception (OR art. 958f) under a
  pseudonym, and the FDPIC or the EU authority for complaints.
- Loading and failure states with a retry; on demo data it says the live app fills it in.
- Uses `MAIN_CONTENT_ID` for skip link.

## API / Data

`GET /api/public/privacy?form=<token>` (anonymous, rate limited per form token). The operator
comes from the server settings `Privacy:OperatorName`, `OperatorAddress`, `OperatorEmail`,
`DpoContact` and `Hosting`; a club's details from its privacy settings (Privacy requests →
Settings).
