using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Automations on a real SQL Server: the migration that copies old trigger settings into
/// conditions, and two runners claiming one run with the real conditional UPDATE.
/// </summary>
public class AutomationsOnSqlServerTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<object?> Value(string database, string sql) => SqlServerApp.ScalarAsync(database, sql);

    [SqlServerFact]
    public async Task OldTriggerSettingsThatNameAConditionBecomeConditions_WhereTheyMeanSomething_AndASecondRunChangesNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var ours = migrations.Single(m => m.EndsWith("_AddAutomationRunsAndConditions", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(ours) - 1]);

            var org = Guid.NewGuid();
            var owner = Guid.NewGuid();
            var stageId = Guid.NewGuid();
            var stageRule = Guid.NewGuid();
            var statusRule = Guid.NewGuid();
            var stageOnALead = Guid.NewGuid();
            var plain = Guid.NewGuid();
            var broken = Guid.NewGuid();
            var pipelineRule = Guid.NewGuid();
            async Task Rule(Guid id, string trigger, string? config)
            {
                var configSql = config == null ? "NULL" : $"N'{config.Replace("'", "''")}'";
                await SqlServerApp.ExecuteAsync(database, $@"
                    INSERT INTO [AutomationRules] ([Id], [OrganizationId], [OwnerUserId], [Name], [Enabled], [Trigger], [TriggerConfigJson], [ActionsJson], [RunCount], [CreatedAtUtc])
                    VALUES ('{id}', '{org}', '{owner}', N'Rule', 1, N'{trigger}', {configSql}, N'[{{""type"":""notify"",""config"":{{}}}}]', 3, SYSUTCDATETIME())");
            }
            await Rule(stageRule, "deal.stage_changed", $$"""{"stageId":"{{stageId}}","note":"kept as it was"}""");
            await Rule(statusRule, "lead.created", """{"Status":"Qualified","source":" Website "}""");
            await Rule(stageOnALead, "lead.created", $$"""{"stageId":"{{stageId}}"}""");
            await Rule(plain, "lead.created", null);
            await Rule(broken, "lead.created", "not json at all");
            await Rule(pipelineRule, "deal.won", """{"pipelineId":"p-1"}""");

            // The migration production runs at start-up, after the ones before it.
            await SqlServerSchema.MigrateAloneAsync(database);

            async Task<List<(string Field, string Op, string Value)>?> ConditionsOf(Guid id)
            {
                var json = await Value(database, $"SELECT [ConditionsJson] FROM [AutomationRules] WHERE [Id] = '{id}'") as string;
                if (json == null) return null;
                var group = AutomationConditions.Parse(json);
                group.Should().NotBeNull($"the conditions of {id} should read: {json}");
                group!.Match.Should().Be("all");
                return group.Conditions.Select(c => (c.Field, c.Op, c.Value ?? "")).ToList();
            }
            (await ConditionsOf(stageRule)).Should().Equal(("stage", "is", stageId.ToString()));
            (await ConditionsOf(statusRule)).Should().BeEquivalentTo(new[] { ("status", "is", "Qualified"), ("source", "is", "Website") },
                "keys are matched in any letter case and values are trimmed");
            (await ConditionsOf(stageOnALead)).Should().BeNull("a lead has no stage: the key means nothing for its trigger");
            (await ConditionsOf(plain)).Should().BeNull("a rule with no settings keeps running for every record");
            (await ConditionsOf(broken)).Should().BeNull();
            (await ConditionsOf(pipelineRule)).Should().Equal(("pipeline", "is", "p-1"));
            (await Value(database, $"SELECT [TriggerConfigJson] FROM [AutomationRules] WHERE [Id] = '{stageRule}'") as string)
                .Should().Contain("kept as it was", "the settings are copied, not moved");
            (await Value(database, $"SELECT COUNT(*) FROM [AutomationRules] WHERE [Version] = '00000000-0000-0000-0000-000000000000'")).Should().Be(6);

            // A second run, as an idempotent migration script would make, changes nothing.
            var before = await Value(database, "SELECT STRING_AGG(CONVERT(nvarchar(36), [Id]) + N'=' + ISNULL([ConditionsJson], N'-'), N';') WITHIN GROUP (ORDER BY [Id]) FROM [AutomationRules]");
            await SqlServerApp.ExecuteAsync(database, AddAutomationRunsAndConditions.CopyTriggerConditionsSql);
            (await Value(database, "SELECT STRING_AGG(CONVERT(nvarchar(36), [Id]) + N'=' + ISNULL([ConditionsJson], N'-'), N';') WITHIN GROUP (ORDER BY [Id]) FROM [AutomationRules]"))
                .Should().Be(before);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoRunnersClaimingOneDueRunAtOnceRunItsStepOnce_AndTheRunCountMovesPastTheRulesVersion()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ILedgerReadObserver>(gate));
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Ok(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            var rule = await Ok(await client.PostAsync("/api/automations", Body(new
            {
                name = "Later", trigger = "lead.created",
                actions = new object[]
                {
                    new { type = "wait", config = new { amount = "10", unit = "minutes" } },
                    new { type = "create_task", config = new { title = "Call them" } },
                },
            })), "add the rule");
            var ruleId = rule.GetProperty("id").GetGuid();
            var version = rule.GetProperty("version").GetGuid();
            var leadId = (await Ok(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "add a lead"))
                .GetProperty("id").GetGuid();

            // The run counted on the rule, past its version: an editor's copy is not made stale by a run.
            (await Value(database, $"SELECT [RunCount] FROM [AutomationRules] WHERE [Id] = '{ruleId}'")).Should().Be(1);
            (await Value(database, $"SELECT [Version] FROM [AutomationRules] WHERE [Id] = '{ruleId}'")).Should().Be(version);

            // Ten minutes pass.
            await SqlServerApp.ExecuteAsync(database,
                $"UPDATE [AutomationRuns] SET [StepStartedAtUtc] = DATEADD(minute, -11, [StepStartedAtUtc]), [NextAttemptAtUtc] = DATEADD(minute, -11, [NextAttemptAtUtc]) WHERE [RuleId] = '{ruleId}'");
            var runId = (Guid)(await Value(database, $"SELECT [Id] FROM [AutomationRuns] WHERE [RuleId] = '{ruleId}'"))!;
            var claimCount = (int)(await Value(database, $"SELECT [ClaimCount] FROM [AutomationRuns] WHERE [Id] = '{runId}'"))!;

            gate.Arm("automations", readers: 2);
            using var first = app.Services.CreateScope();
            using var second = app.Services.CreateScope();
            var due = new DueAutomationRun(runId, claimCount);
            await Task.WhenAll(
                first.ServiceProvider.GetRequiredService<IAutomationEngine>().ResumeAsync(due),
                second.ServiceProvider.GetRequiredService<IAutomationEngine>().ResumeAsync(due));

            (await Value(database, $"SELECT COUNT(*) FROM [TaskItems] WHERE [LeadId] = '{leadId}'")).Should().Be(1, "one runner's conditional UPDATE moved the claim");
            (await Value(database, $"SELECT [Status] FROM [AutomationRuns] WHERE [Id] = '{runId}'")).Should().Be(AutomationRunStatuses.Completed);
            (await Value(database, $"SELECT COUNT(*) FROM [AutomationRunSteps] WHERE [RunId] = '{runId}' AND [StepType] = N'create_task'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
