using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRunsAndConditions : Migration
    {
        /// <summary>
        /// Copies the trigger settings that name a condition into the rule's conditions; public so a
        /// test can run it a second time and see it change nothing.
        /// </summary>
        /// <remarks>
        /// A rule's trigger settings were stored and never read: a rule given
        /// {"stageId": "..."} through the API ran on every stage. The keys that say what the
        /// record must be are copied into the rule's conditions, which are evaluated, for
        /// the triggers where they mean something (a stage on a deal's trigger, a status or
        /// a source on a lead's). Keys are matched in any letter case, as the API kept them
        /// as sent. The settings themselves are left as they were, a rule with no such key
        /// keeps no conditions and runs as before, and a rule that already has conditions
        /// is not touched, so running this again changes nothing.
        /// </remarks>
        public const string CopyTriggerConditionsSql = @"
UPDATE r SET [ConditionsJson] = N'{""match"":""all"",""conditions"":' + x.[Conditions] + N'}'
FROM [AutomationRules] r
CROSS APPLY (
    SELECT (
        SELECT m.[Field] AS [field], N'is' AS [op], LTRIM(RTRIM(j.[value])) AS [value]
        FROM OPENJSON(r.[TriggerConfigJson]) j
        JOIN (VALUES
            (N'lead.created', N'status', N'status'), (N'lead.created', N'tostatus', N'status'),
            (N'lead.created', N'source', N'source'),
            (N'lead.created', N'ownerid', N'owner'), (N'lead.created', N'assigneeid', N'owner'),
            (N'lead.created', N'assignedtouserid', N'owner'),
            (N'lead.status_changed', N'status', N'status'), (N'lead.status_changed', N'tostatus', N'status'),
            (N'lead.status_changed', N'fromstatus', N'fromStatus'), (N'lead.status_changed', N'source', N'source'),
            (N'lead.status_changed', N'ownerid', N'owner'), (N'lead.status_changed', N'assigneeid', N'owner'),
            (N'lead.status_changed', N'assignedtouserid', N'owner'),
            (N'deal.stage_changed', N'stageid', N'stage'), (N'deal.stage_changed', N'tostageid', N'stage'),
            (N'deal.stage_changed', N'stage', N'stage'), (N'deal.stage_changed', N'tostage', N'stage'),
            (N'deal.stage_changed', N'fromstageid', N'fromStage'), (N'deal.stage_changed', N'fromstage', N'fromStage'),
            (N'deal.stage_changed', N'pipelineid', N'pipeline'),
            (N'deal.stage_changed', N'ownerid', N'owner'), (N'deal.stage_changed', N'assigneeid', N'owner'),
            (N'deal.won', N'pipelineid', N'pipeline'), (N'deal.won', N'ownerid', N'owner'), (N'deal.won', N'assigneeid', N'owner'),
            (N'deal.lost', N'pipelineid', N'pipeline'), (N'deal.lost', N'ownerid', N'owner'), (N'deal.lost', N'assigneeid', N'owner'),
            (N'task.overdue', N'assigneeid', N'assignee'), (N'task.overdue', N'ownerid', N'assignee')
        ) m([Trigger], [Key], [Field])
            ON m.[Trigger] = r.[Trigger] COLLATE DATABASE_DEFAULT
           AND m.[Key] = LOWER(j.[key]) COLLATE DATABASE_DEFAULT
        WHERE j.[type] = 1 AND LEN(LTRIM(RTRIM(j.[value]))) > 0
        FOR JSON PATH
    ) AS [Conditions]
) x
WHERE r.[ConditionsJson] IS NULL
  AND r.[TriggerConfigJson] IS NOT NULL
  AND ISJSON(r.[TriggerConfigJson]) = 1
  AND LEFT(LTRIM(r.[TriggerConfigJson]), 1) = N'{'
  AND x.[Conditions] IS NOT NULL;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionsJson",
                table: "AutomationRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendWindowJson",
                table: "AutomationRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "AutomationRules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(CopyTriggerConditionsSql);

            migrationBuilder.CreateTable(
                name: "AutomationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromImport = table.Column<bool>(type: "bit", nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EventJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPath = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StepStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StepAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimCount = table.Column<int>(type: "int", nullable: false),
                    WaitingFor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRuns_AutomationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRunSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StepType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    OutboundMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRunSteps_AutomationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AutomationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuns_NextAttemptAtUtc",
                table: "AutomationRuns",
                column: "NextAttemptAtUtc",
                filter: "[NextAttemptAtUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuns_OrganizationId_RuleId_StartedAtUtc",
                table: "AutomationRuns",
                columns: new[] { "OrganizationId", "RuleId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuns_OrganizationId_Status_StartedAtUtc",
                table: "AutomationRuns",
                columns: new[] { "OrganizationId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuns_RuleId",
                table: "AutomationRuns",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunSteps_RunId_AtUtc",
                table: "AutomationRunSteps",
                columns: new[] { "RunId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRunSteps");

            migrationBuilder.DropTable(
                name: "AutomationRuns");

            migrationBuilder.DropColumn(
                name: "ConditionsJson",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "SendWindowJson",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AutomationRules");
        }
    }
}
