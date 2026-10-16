# Cadence sales agent

The agent tells a salesperson the single next step for each open record. It reads what is already saved across the workspace and applies fixed rules. It does not call a language model, and it does not change records.

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
| Sequences | The due step, unless that person already has a stronger lead or contact step |
| Automations | Add an action when the rule is on and empty. A rule that is off is a choice. |
| Duplicates | Merge contact groups that share an email or a name, and company groups that share a domain or a name. Same scan as the Duplicates page. |
| Contracts | Read through the lead's contract, signature, and deposit phases. There is no second contract list. |
| Templates, copy writer, copy history, attach copy | Drafting tools. They do not owe a next step. |
| Reports, audit log | The reports page shows this list. The audit log is a record of what already happened. |
| Team, workspaces, settings, Claude, help | Workspace setup, not a sales step |

A first call that already has an email or a phone stays a call. Nobody is assigned does not replace the step.

Draft message fills the sales writer already on the dashboard: the copy type, the goal, the reason, and the person. The writer drafts the words. It does not send them. A step that is only data entry, such as recording a reason or adding a phone number, has no draft.

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
| Due later, or a low-priority task with no date | No suggestion | — |
| Completed or cancelled | No suggestion | — |

A later task stays off the list so it does not crowd out work that is actually due.

## Sequences

A sequence is an ordered set of email, call, task, and wait steps. The scheduler stores `nextActionAtUtc` on the enrollment. The agent speaks only when the enrollment is active and that date is today or earlier. An enrollment with no date produces nothing: there is not enough evidence to say the step is due. The sentence uses the current step: send the email, make the call, complete the task, or advance a finished wait. Priority is 71. Paused and completed enrollments produce nothing. The link goes to the lead or the contact who is enrolled. If that same person already has a lead step at a higher priority, the sequence row is dropped and the lead step remains.

## Where the code lives

- Calendar days, so a UTC midnight date stays on that day: `src/app/salesAgent/calendarDay.ts`
- Lead rules: `src/app/salesAgent/nextAction.ts`
- Deals, tasks, sequences, and the merged ranking: `src/app/salesAgent/workspace.ts`
- Tests: `src/app/salesAgent/nextAction.test.ts`, `src/app/salesAgent/workspace.test.ts`
- On screen: `src/app/salesAgent/NextActions.tsx`, mounted under the hero on the dashboard and the reports page

If one list cannot be loaded, the others still produce suggestions. The block stays hidden when every list fails, or when nothing is owed.
