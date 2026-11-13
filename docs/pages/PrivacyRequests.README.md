# Privacy requests

**Route:** `/privacy-requests` (`?lead=`, `?contact=`, `?member=` or `?email=` to start from a person; `?tab=erasures|retention|settings`)  
**File:** `PrivacyRequests.tsx`, panels in `components/privacy/`

## Purpose

A person asks what is held about them, or to be erased; and how long the workspace keeps
personal data. Only for someone holding `privacy.manage` ("Handle privacy requests": the owner,
managers, or a member given it on the Team page), because a download hands over everything about
someone at once and an erasure cannot be undone. The menu offers it only to them, in a workspace.

## Behavior

- **Requests**: find a person by email, or open a lead's, contact's or member's page and choose
  *Privacy*. The server finds their records by the address and the links between records (never a
  name alone), and every record and address found starts ticked; staff untick what is someone
  else's (an address on a ticked record stays ticked: it always goes with the record). *Download
  everything held* gives one zip (each area as JSON, a readable summary, the
  files on their records), written to the audit log. *Erase this person* shows the server's dry
  run (what is deleted, anonymised and kept in the books under a pseudonym, cancelled, or kept and
  why, with warnings such as money owed or a card saved with Stripe), then erases on the typed word
  ERASE with an optional reference of staff's own (never the person's name or email).
- **Erasures**: the log, which names nobody: number, when, by whom or by the retention settings,
  the reference, and how much went where.
- **Retention**: each rule with its period (all off until set), what a run would remove now and
  what it keeps although the period is over, *Run now* on a second click, and the runs.
- **Settings**: who answers for the data (printed on the privacy notice), marketing consent on the
  public forms (off, optional or required, and the enquiry form's words; the application form has
  its own), and the retention periods. Saved
  against the version read; a stale save is refused and the settings are read again.

## API / Data

`/api/privacy/*`: `POST find`, `POST export`, `POST erasure/preview`, `POST erasure`,
`GET erasures`, `GET|PUT settings` (ETag / If-Match), `GET retention/preview`,
`POST retention/run`, `GET retention/runs`. Lookups and previews are POSTs because they carry an
email address. Cached under the `privacy` kind; an erasure or a run refreshes every list the
person was on.
