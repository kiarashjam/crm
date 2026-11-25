using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The recap emails on a real SQL Server: the settings saved under their unique index, each recap
/// taken once by two passes running at the same moment, a claim a dead server left taken over
/// once, and someone leaving taking their recaps with them.
/// </summary>
public class RecapEmailsOnSqlServerTests
{
    [SqlServerFact]
    public async Task TwoPassesAtOnceSendEachRecapOnce_AndTheSettingsSaveAndGoWithSomeoneWhoLeaves()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@pavillon46.ch");
            var (founder, founderId) = await SignInAsync(app, $"founder-{Guid.NewGuid():N}@pavillon46.ch");
            var (_, managerId) = await SignInAsync(app, $"manager-{Guid.NewGuid():N}@pavillon46.ch");
            var org = Guid.NewGuid();
            await HealthWorld.SaveAsync(app,
                new Organization { Id = org, Name = "Pavillon 46", OwnerUserId = ownerId, CreatedAtUtc = DateTime.UtcNow },
                new OrganizationMember { OrganizationId = org, UserId = ownerId, Role = OrgMemberRole.Owner, JoinedAtUtc = DateTime.UtcNow },
                new OrganizationMember { OrganizationId = org, UserId = founderId, Role = OrgMemberRole.Viewer, PermissionPreset = "read_only", JoinedAtUtc = DateTime.UtcNow },
                new OrganizationMember { OrganizationId = org, UserId = managerId, Role = OrgMemberRole.Manager, PermissionPreset = "manager", JoinedAtUtc = DateTime.UtcNow });
            owner.In(org);
            founder.In(org);

            // The settings, written through the API: the owner switches people on, the founder one off.
            (await owner.PutAsync($"/api/recaps/recipients/{founderId}", Body(new { daily = true, weekly = true }))).EnsureSuccessStatusCode();
            (await owner.PutAsync($"/api/recaps/recipients/{ownerId}", Body(new { daily = true }))).EnsureSuccessStatusCode();
            (await owner.PutAsync($"/api/recaps/recipients/{managerId}", Body(new { daily = true }))).EnsureSuccessStatusCode();
            (await founder.PutAsync("/api/settings/recaps", Body(new { weekly = false }))).EnsureSuccessStatusCode();
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecapSubscriptions] WHERE [OrganizationId] = '{org}'")).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecapSubscriptions] WHERE [UserId] = '{founderId}' AND [WeeklySinceUtc] IS NULL AND [DailySinceUtc] IS NOT NULL"))
                .Should().Be(1);

            // The manager leaves: their recaps go with them, in the same transaction as the hand-over.
            (await owner.DeleteAsync($"/api/organizations/{org}/members/{managerId}")).EnsureSuccessStatusCode();
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecapSubscriptions] WHERE [UserId] = '{managerId}'")).Should().Be(0);

            // The first daily recap after now: switched on just now, it is today's, due tomorrow at seven.
            var zone = RegionalSettings.ZoneOf("Europe/Zurich");
            var today = RecapPeriods.For(RecapFrequencies.Daily, RecapPeriods.LatestDue(RecapFrequencies.Daily, DateTime.UtcNow, zone).FirstDay.AddDays(1), zone);
            var at = today.DueUtc.AddMinutes(5);
            // A server that died while preparing the founder's left its claim twenty minutes ago.
            await HealthWorld.SaveAsync(app,
                new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Jean Dupont", Email = "jean@example.ch", Status = "New", Source = "pavillon46.ch", CreatedAtUtc = today.FromUtc.AddHours(1) },
                new RecapSend
                {
                    Id = Guid.NewGuid(), OrganizationId = org, UserId = founderId, Frequency = RecapFrequencies.Daily, PeriodStart = today.FirstDay,
                    Status = RecapSendStatuses.Preparing, CreatedAtUtc = at.AddMinutes(-20),
                });

            async Task<int> PassAsync()
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IRecapEmails>().SendDueAsync(at);
            }
            var passes = await Task.WhenAll(PassAsync(), PassAsync());

            passes.Sum().Should().Be(2, "the owner's and the founder's, each once");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecapSends] WHERE [OrganizationId] = '{org}' AND [Status] = 'sent'")).Should().Be(2);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecapSends] WHERE [OrganizationId] = '{org}'")).Should().Be(2, "the stale claim was taken over, not doubled");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [OrganizationId] = '{org}' AND [Source] = 'recap'")).Should().Be(2);
            (await PassAsync()).Should().Be(0);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
