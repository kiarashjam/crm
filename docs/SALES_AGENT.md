# Cadence sales agent

The agent tells a salesperson the single next step for each open record. It reads what is already saved across the workspace and applies fixed rules. It does not call a language model. A task, a note, a comment, an assignment, a conversion, an enrollment, or a send happens only after the person presses the button. A draft opens the sales writer and is not sent.

## What the last pass closed

The five rows are ordered by rule priority plus lead score, deal amount, probability, silence, a missing owner, and a date that is already late. Lead score of 70 or more adds weight. A deal of 40,000 or more can move ahead of a higher rule priority, and the row moves with it. A saved probability under 40 percent trims that amount. A row that already says nothing was recorded for 14 days gets a further silence weight. A second call, after a no-answer earlier today, sits lower before 15:00 and a little higher from 15:00 on. The first row quotes those weights. The spoken answer still names the rule-priority row when the two disagree.

The panel reads eight lists: leads, deals, tasks, sequence enrollments, sequences, contacts, companies, and automations. It does not read activities, contracts, invitations, or the club lists, so an answer that needs one of those says it is not loaded instead of reporting none. A list that fails is named in the answer: "could not be loaded" when there is no copy, "could not be refreshed, so an earlier copy is used" when an older copy is shown.

"That one" and the last twelve turns are saved on the signed-in user's settings, so they come back on another computer when the API is available. This browser keeps a copy as well, and signing out clears it. A conversation that comes back keeps its open and draft buttons only: whether a save button was already pressed is not stored, so it is not offered twice.

A confirmed button can send a message you typed when an email is saved, assign an unowned lead to the signed-in person, convert a lead into a contact and a deal, or enroll a lead in one of up to five sequences. Each duplicate group is its own step. Merge keeps the record that has an email, or the first company, and runs only after the button. A merge keeps "do not contact" when either record had it.

Asking to enroll offers a button for each saved sequence, up to five. Asking to merge offers one button per duplicate group, up to five. Asking to move the stage opens the deal: the panel does not load pipeline stages. Asking to book saves a meeting task due on the day you name, or after the default follow-up days. A club reservation still needs a room chosen on Reservations. Asking who should I call lists the loaded call steps and includes a saved phone. Asking for the overdue one opens the late row. On a list page, the next-step answer uses that page. On an open record, it uses that record when the record has a step. A saved email in the question selects that record. "Their other opportunities" reads the current company on the focused record and lists the other loaded deals. A tied name asks which record. A sentence that is not one of the set questions names the first loaded step, then the queue. Asking where the money is includes a saved probability. Counts and closed-deal patterns are labeled as calculated. Recorded deals and search hits are labeled as recorded. The conversation is stored per signed-in user and organization.

The reason on a lead can quote source, referrer, tags, lead score, and that a description is saved. A contact step quotes job title and preferred contact method when those are the reason the person cannot be reached.

A reminder date that is today or earlier puts the task on the list when the due date is missing or still in the future. Asking to comment on a task saves the comment only after the button. Asking about invitations points to the Team page, which lists them and can resend one.

## What stays outside this CRM

These are boundaries of the product, not unfinished steps inside Cadence.

| Boundary | What that means |
|----------|-----------------|
| No language model | Answers are the set questions, plus the loaded queue when a sentence does not match. Nothing is sent to a model. |
| Learning is this workspace | Won deals, lost deals, and drop-out reasons are counted from the records loaded here. Other companies are not part of that count. |
| Mail and meetings outside Cadence | Outside inboxes, outside calendars, and call recordings are not connected. A failed email already saved here is a step. A meeting the agent books is a dated task. |
| Other systems | Accounting, support, and marketing tools outside this CRM are not read. |

The rules follow the process stored in the app, plus the practice used for a next-best-action list: one step per record, chosen by the first rule that matches, with the evidence written beside it. A date that is merely soon raises that step. It does not replace it. A no-answer is not a loss. A missing reason on a lost record is finished before anything else. Nothing is owed when the evidence is too thin to name a step.

Sources: [what next best action is](https://pipeit.ai/blog-next-best-action), [one action per open deal](https://resources.rework.com/libraries/ai-for-sales-operations/next-best-action-for-each-open-deal), [work the due queue before cold calls](https://dialedsales.com/blog/never-miss-a-sales-follow-up), [a no-show is not a loss](https://usepinpoint.ai/resources/workflows/why-appointments-no-show/), [retry at a different time of day](https://dring.ai/blog-outbound-retry-policy).

## How a suggestion is chosen

The rules are checked in order. The first one that matches is the sentence. Later rules can only raise its priority or add a sentence to the reason.

1. A lost lead or deal with no reason. Recording the reason comes before any selling.
2. A meeting dated today. Holding it comes before repairing a close date, because the meeting cannot be done tomorrow.
3. A close date that has already passed, or a task that is overdue.
4. The stage's own next step: deposit, signature, contract, meeting outcome, proposal, qualification, first call.
5. A sequence step whose date is today or earlier, when that person does not already have a stronger step.
6. A contact with no email and no phone, duplicate contacts or companies, an automation that is on and empty, and a company with nobody on file.
7. No suggestion. Won deals, lost records with a reason, converted leads, future sequence steps, tasks due later, enrollments with no date, archived contacts, do-not-contact, and automations that are switched off all stop here.

A close date inside the next seven days, or a week with no activity, lifts the stage step up the list and is quoted in the reason. The sentence stays the stage step. Two suggestions that open the same lead or contact collapse to the stronger one. The screen shows five. The rest wait, and the next one moves up when a row is cleared.

## What it looks at

| Place in the CRM | What the agent does |
|------------------|---------------------|
| Leads, including import and the webhook | The five-phase pipeline. A call with no phone and no email becomes "add a way to reach them". A missing owner is written on the reason. |
| Deals | The stage step, a passed close date, or a missing loss reason. A missing owner is written on the reason. |
| Tasks | Overdue, due today, or high priority with no date |
| Activities | History. A deal with no activity for seven days is told to log a touch. An activity row is not its own task. |
| Contacts | Add an email or phone when both are missing. Archived and do-not-contact stay quiet. |
| Companies | Add a contact when the company has no people |
| Sequences | The due step, unless that person already has a stronger lead or contact step. Enroll offers one button per sequence, up to five. |
| Automations | Add an action when the rule is on and empty. A rule that is off is a choice. |
| Duplicates | Same scan as the Duplicates page. Each group is a step, up to five. Merge keeps the record with an email, or the first company, after a button. |
| Contracts | Read through the lead's contract, signature, and deposit phases. There is no second contract list. |
| Templates, copy writer, copy history, attach copy | Drafting tools. They do not owe a next step. |
| Reports, audit log | The reports page shows this list. The audit log is a record of what already happened. |
| Team, workspaces, settings, Claude, help | Workspace setup, not a sales step. The workspace launcher shows the agent when a workspace is already selected. |

A first call that already has an email or a phone stays a call. Nobody is assigned does not replace the step.

Draft message fills the sales writer already on the dashboard. Asking to send offers a button only for words you typed, after a colon or in quotes, to a saved email that is not marked do not contact. The saved step is an instruction to you, so it is never sent as the message. The message goes out only after that button.

## Leads

The lead process lives in `src/app/pages/leads/leadPipeline.ts`. Five phases, stored as JSON on `pipelineState`.

| Phase | Name | Done when |
|-------|------|-----------|
| 1 | Outreach | Contacted, a meeting is scheduled, and the meeting has a date |
| 2 | Meeting | They attended and are still interested |
| 3 | Contract | Contract is yes, and the send date is saved |
| 4 | Signature | Signed, and the signature date is saved |
| 5 | Deposit | Paid, and the payment date is saved |

A no-show is not a loss. The lead is lost only when a failure is recorded: not interested, profile rejected, contract declined, or deposit never paid. A lost lead still needs a reason from the dropout or rejection list. "Other" needs the written note.

| Situation | Suggestion | Priority |
|-----------|------------|----------|
| Failure recorded, reason missing | Record why | 100 |
| Deposit not paid | Ask for the deposit | 88 |
| Signature missing or pending | Ask them to sign, or follow up | 86 |
| Contract to be sent | Send the contract | 84 |
| Signed, no signature date | Record the date | 83 |
| Contract yes, no send date | Record the send date | 82 |
| Contract empty | Record the contract decision | 80 |
| Meeting dated today | Hold it, then log whether they came | 96 |
| Meeting date passed, attendance blank | Log whether they came | 78 |
| No-show, interest blank or still interested | Reschedule, or book another meeting | 77 |
| Meeting scheduled, no date | Add the date | 76 |
| Attended, interest blank | Record whether they are still interested | 76 |
| Follow-up, no meeting | Book the meeting | 74 |
| Contacted, no outcome | Record meeting, follow-up, or not interested | 72 |
| No outreach yet | Make the first call and log the answer | 70 |
| Attempted, no answer, on an earlier day | Call again at a different time of day | 68 |
| Meeting tomorrow | Prepare for it | 66 |
| No answer already logged today | Try once more later today, below work that is due | 55 |
| Meeting after tomorrow | Prepare for that date | 40 |
| All five phases complete | Convert to a deal | 90 |
| Lost, reason saved | No suggestion | — |
| Lead already converted | No suggestion | — |

## Deals

Deal stages are configured per pipeline. The default names are Qualification, Proposal, Negotiation, Contract Sent, Contract Signed, Closed Won, and Closed Lost. A deal is won when `isWon` is true or the stage is Closed Won. It is lost when `isWon` is false or the stage is Closed Lost. The pipeline page already treats a passed `expectedCloseDateUtc` on an open deal as overdue.

| Situation | Suggestion | Priority |
|-----------|------------|----------|
| Lost, no close reason | Record why it was lost | 99 |
| Open, close date in the past | Update the date, or close the deal | 94 |
| Contract Signed | Mark Closed Won once payment is in | 88 |
| Contract Sent | Follow up on the signature | 86 |
| Negotiation | Agree the terms and send the contract | 82 |
| Proposal | Send the proposal, or follow up | 72 |
| Qualification | Confirm the fit, then move to Proposal | 70 |
| Custom stage, quiet for seven days, date already set | Log a touch | 67 |
| Custom stage, no close date | Add an expected close date | 65 |
| Any other open stage | Move it on, or record why it is waiting | 64 |
| Won, or lost with a reason | No suggestion | — |

A passed close date replaces the stage step, because the forecast date is already wrong. A close date inside seven days does not. It keeps the stage sentence and raises that step to at least 80, still under Negotiation. A week of silence adds eight to the priority, and never pushes an early stage above 85, so a signature chase stays ahead of a quiet qualification. Both facts are written into the reason.

## Tasks

Grouping matches `src/app/pages/tasks/utils.ts`: completed and cancelled are finished; a due date before today is overdue; the same calendar day is due today.

| Situation | Suggestion | Priority |
|-----------|------------|----------|
| Open and overdue | Complete it | 93 |
| Due today | Do it today | 73 |
| High priority or in progress, no due date | Set a due date | 58 |
| Reminder date today or earlier, due date missing or still ahead | The reminder is due | 60 |
| Due later, or a low-priority task with no date and no reminder | No suggestion | — |
| Completed or cancelled | No suggestion | — |

A later task stays off the list so it does not crowd out work that is actually due.

## Sequences

A sequence is an ordered set of email, call, task, and wait steps. The scheduler stores `nextActionAtUtc` on the enrollment. The agent speaks only when the enrollment is active and that date is today or earlier. An enrollment with no date produces nothing: there is not enough evidence to say the step is due. The sentence uses the current step: send the email, make the call, complete the task, or advance a finished wait. Priority is 71. Paused and completed enrollments produce nothing. The link goes to the lead or the contact who is enrolled. If that same person already has a lead step at a higher priority, the sequence row is dropped and the lead step remains.

## Where the code lives

- Calendar days, so a UTC midnight date stays on that day: `src/app/salesAgent/calendarDay.ts`
- Lead rules: `src/app/salesAgent/nextAction.ts`
- Deals, tasks, sequences, and the merged ranking: `src/app/salesAgent/workspace.ts`
- Tests: `src/app/salesAgent/nextAction.test.ts`, `src/app/salesAgent/workspace.test.ts`
- Questions, playbooks, memory, and the amount-and-silence judgment: `src/app/salesAgent/brain/`
- Briefings, activity, neglected records, pipeline math, and proposed creates: `src/app/salesAgent/brain/workflows.ts`
- Shared list cache, one copy per workspace, refreshed after a save: `src/app/salesAgent/agentData.ts`
- The ask box: `src/app/salesAgent/AgentDesk.tsx`. The last record and the last turns are saved on the user settings (`AgentMemoryJson`) and in this browser, under a key that includes the user and the organization. Send, assign, convert, enroll, merge, a task, a note, a comment, a contact, and a deal change run only from a button. A button saves nothing when another tab has switched the workspace or the account since the answer was given.
- On screen: `src/app/salesAgent/NextActions.tsx`, in the slot after the page title, and on the workspace launcher when a workspace is already selected. The ask box sits on that same panel.
- Reusable checks: `src/app/salesAgent/agentEvaluation.test.tsx`. Organization isolation for the external tools: `backend/tests/ACI.WebApi.Tests/McpEndpointTests.cs`.

If one list cannot be loaded, the others still produce suggestions. The block stays hidden when every list fails, or when nothing is owed.

## What a question can do

Answers use the records already loaded for this user and organization. A follow-up such as "their email" or "their other opportunities" uses the last record, then reads the current loaded rows. It does not reuse an old sentence as if it were a new fact. A change that names nobody goes to the record on screen, or to the last record (asked for first with "that one"), read again by its id. When neither is loaded the agent asks. It never puts the change on the top row of the queue instead.

A full name selects that person, as whole words: "Ann" is not found in "planning", and "the" or "call" is never the start of a name. Two loaded people who share a first name stay a question. A lead and the contact made from it share an email and count as one person. A name that is not loaded stays unanswered. Names are read from the part of the question before a colon or a quote, so the words of a note or a message do not change which record it is for. A company briefing lists the open deals, tasks, and activity already loaded for that company. Closed won and closed lost stay out of the open total. A blank last-contact date is not treated as neglect. Neglect means 14 days since a saved contact or activity date, a close date before today, or a task due before today.

Search, counts, and the pipeline total say when the list failed or when more loaded rows were not printed. Totals are kept per currency and never added across currencies. Amounts are read the way the rest of the app reads them, so "1.234,56" is one thousand two hundred and thirty-four. A value that is not a number, or a negative amount, is left out of the total and named.

Create contact needs a name and an email, and it will not propose a second contact for an email already loaded. Create deal needs a name and an amount after "worth", "amount", "value", "for", or next to a currency, such as "worth CHF 12'500" or "for 15k". A year or a date is not read as the amount. It will not propose a second open deal with the same name. A task takes the words you typed as its title and the day you name (tomorrow, Friday, in 3 days, 2026-11-02) as its due date. A note or a comment needs the words you typed: the agent does not write the saved step into the record as if it happened. Changing an amount or a close date shows the recorded value and the proposed value, and it needs a deal already in view. The button is the only save. A second click while that save is running does not send it again. After the API returns the saved record, the button is removed. If the API returns an error, the button stays and the error is shown. A read-only workspace shows no save button and does not call the API.

Text inside a note, an activity, or any other record is data. It does not change the draft's saved step, the pipeline total, or which buttons appear. Delete, wipe, and erase are refused. Duplicate review opens the review page and does not merge until the person asks to merge and then presses the button.

## Cache

The lists live in `agentData.ts`, outside the panel, so navigating does not load them again. The key is the organization plus the signed-in session. A copy younger than 60 seconds is kept. A save, or returning to the tab, refreshes behind the rows already on screen. Switching organization drops the previous lists. One failed list does not wipe the others. A write is not retried by the loader.

## Example requests

- "What do we have on Ada Lovelace?"
- "Brief Pavilion."
- "What about their other opportunities?"
- "What is their email?"
- "Who went quiet?"
- "Pipeline value."
- "How many open deals?"
- "Search for Pavilion."
- "Create a contact named Rita Book rita@example.com."
- "Create a deal called Harbor for 15000."
- "Set the amount to 15000."
- "Draft Pavilion renewal."
- "Any duplicates?"

A draft opens the sales writer. Nothing is sent, created, or changed until the button for that action is pressed.

## Checking these answers

The evaluation file states the expected text and buttons for each scenario. The checks do not ask the agent to grade itself.

```text
npx vitest run src/app/salesAgent
```

`agentLoads.test.tsx` checks that a second page reads none of the lists again.

Organization isolation for the external read-only tools:

```text
dotnet test backend/tests/ACI.WebApi.Tests/ACI.WebApi.Tests.csproj --filter McpEndpointTests
```

## Limits that are still true

The `AgentMemoryJson` column is added by the API startup script when it is missing. There is no separate migration to run. The list cache lives in the browser tab. Production deployment is a separate step.

The in-app agent does not call a model. The sales writer does, when an API key is configured, and an empty key keeps the template replies. Record text is placed between marker lines that change on every request, so a record cannot close the block early. A model can still ignore the label. This CRM does not read mail or calendars outside Cadence, and it does not open attachments. Global search still reads its four lists one after another. A question that is not one of the set questions falls back to the loaded queue and says so. Answers use the loaded lists, which can be up to 60 seconds old, rather than a new query for every question. The ask box does not yet read activities, contracts, invitations, pipeline stages, or the club lists, so those questions say so and point to the page that has them. The external tools are read-only and were not given a write path.
