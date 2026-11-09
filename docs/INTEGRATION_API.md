# Integration API (`/api/integration`)

Read-only JSON for another system that tracks one Cadence organization. Code:
`backend/src/ACI.WebApi/Controllers/IntegrationController.cs`. Tests: `IntegrationSummaryTests`,
`IntegrationDealsTests`.

Two systems use it today:

- **Atmosclear BMS** (`kiarashjam/ACI-PA`) shows a project's organization figures from the summary.
- **BonApp admin** (Azure DevOps `BonApp/bonapp-backend`, `Restaurant.Documentation/Cadence.md`) shows
  its sales pipeline from the summary, and links each restaurant to its deal with the deal routes.

## Key

Every route takes the organization's **MCP key** (**Settings → Claude**), as `X-Api-Key: <key>` or
`Authorization: Bearer <key>`. Prefer `X-Api-Key`: a Bearer value on these routes first goes through
the JWT handler, which fails to read it as a session token. The key opens exactly one organization,
read as its owner, the way the `/mcp` tools read it. The website-form lead key is refused (401).
Answers carry `Cache-Control: no-store`.

## Routes

| Route | Answer |
|---|---|
| `GET /api/integration/summary` | leads, tasks, deals and pipeline figures, pipeline value by stage, the 10 latest activities |
| `GET /api/integration/deals?search=&take=` | `{ organizationId, totalCount, items }`: deals by name, open and closed. `search` matches deal, company, contact, value or stage, case-insensitive. `take` is kept between 1 and 50, default 20 |
| `GET /api/integration/deals/{id}` | `{ deal, recentActivity, openTasks }`: the deal, its 10 latest activities, its open tasks (to do or in progress) by due date. 404 with `{ "error": … }` when the deal is not in the organization |

A deal: `id, name, stage, pipelineName, value, currency, companyName, contactName, assigneeName,
probability, expectedCloseDateUtc, status, closedAtUtc, lastActivityAtUtc, createdAtUtc,
updatedAtUtc`. `stage` is the pipeline stage's name, or the legacy stage text. `status` is `open`,
`won` or `lost`. `value` is null when the recorded value holds no readable number (the same rule as
the dashboard's unreadable count): never 0 for "unreadable".

What stays in Cadence: activity bodies, task descriptions and notes, contact emails and phone numbers.
