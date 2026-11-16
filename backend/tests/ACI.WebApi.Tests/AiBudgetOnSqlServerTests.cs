using System.Net.Http.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The monthly allowance on a real SQL Server, where calls really race: the conditional UPDATE
/// that takes a call, and the unique index that keeps one row per workspace and month.
/// </summary>
public class AiBudgetOnSqlServerTests
{
    [SqlServerFact]
    public async Task CallsRacingForTheLastOfTheMonthTakeNoMoreThanItAllows()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await (await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }))).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
                .GetProperty("id").GetGuid();
            await using (var db = app.Services.EveryWorkspace())
            {
                db.AssistantSettings.Add(new AssistantSettings { Id = Guid.NewGuid(), OrganizationId = org, MonthlyLimit = 4, MorningBriefs = true, CreatedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            // Twelve calls at once, each in a scope (and so a connection) of its own, and the
            // month's row not made yet: the first ones also race to make it.
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IAiBudget>().TakeAsync(org, ownerId, personWaiting: false);
            }));

            results.Count(r => r.IsSuccess).Should().Be(4);
            results.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Error.Code == "Ai.MonthlyLimit");
            await using (var db = app.Services.EveryWorkspace())
            {
                var rows = await db.AiUsage.Where(u => u.OrganizationId == org).ToListAsync();
                rows.Should().ContainSingle("one row per workspace and month, however many raced to make it");
                rows[0].Calls.Should().Be(4);
                rows[0].Refused.Should().Be(8);
                rows[0].UserId.Should().BeNull();
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
