using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// System health on a real SQL Server: the page's queries, the alerts' readings across
/// workspaces and the digest's claim, as SQL Server runs them.
/// </summary>
public class SystemHealthOnSqlServerTests
{
    [SqlServerFact]
    public async Task The_page_the_readings_and_the_digest_run_on_sql_server()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (alpine, alpineOwner) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@alpine.example");
            var (_, berninaOwner) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@bernina.example");
            Guid alpineOrg = Guid.NewGuid(), berninaOrg = Guid.NewGuid();
            await HealthWorld.SaveAsync(app,
                new Organization { Id = alpineOrg, Name = "Alpine", OwnerUserId = alpineOwner, CreatedAtUtc = DateTime.UtcNow },
                new Organization { Id = berninaOrg, Name = "Bernina", OwnerUserId = berninaOwner, CreatedAtUtc = DateTime.UtcNow },
                new OrganizationMember { OrganizationId = alpineOrg, UserId = alpineOwner, Role = OrgMemberRole.Owner, JoinedAtUtc = DateTime.UtcNow },
                new OrganizationMember { OrganizationId = berninaOrg, UserId = berninaOwner, Role = OrgMemberRole.Owner, JoinedAtUtc = DateTime.UtcNow });
            var now = DateTime.UtcNow;
            var a = await HealthWorld.SeedTroubleAsync(app, alpineOrg, alpineOwner, "alpine", now);
            await HealthWorld.SeedTroubleAsync(app, berninaOrg, berninaOwner, "bernina", now);

            var health = await HealthWorld.Ok(await alpine.In(alpineOrg).GetAsync("/api/system-health"));
            var counts = health.GetProperty("overall").GetProperty("counts");
            counts.GetProperty("handoffs").GetInt32().Should().Be(2);
            counts.GetProperty("outbox").GetInt32().Should().Be(4);
            counts.GetProperty("webhooks").GetInt32().Should().Be(1);
            counts.GetProperty("payments").GetInt32().Should().Be(1);
            counts.GetProperty("automations").GetInt32().Should().Be(1);
            counts.GetProperty("email").GetInt32().Should().Be(2);
            health.GetProperty("handoffs").GetProperty("items").EnumerateArray().Select(h => h.GetProperty("id").GetGuid())
                .Should().BeEquivalentTo(new[] { a.FailedHandoff, a.LateHandoff });

            using (var scope = app.Services.CreateScope())
            {
                var readings = await scope.ServiceProvider.GetRequiredService<IMonitoringReadings>().ReadAsync(now);
                readings.LateEmails.Should().Be(2, "one in each workspace");
                readings.OldestLateEmailMinutes.Should().BeInRange(119, 121);
                readings.LateHandoffs.Should().Be(2);
                readings.LateHandoffWorkspaces.Should().Be(2);
                readings.FailedMessageIds.Values.Should().BeEquivalentTo(new[] { "email", "webhook", "email", "webhook" });
                readings.FailedStripeEventIds.Values.Should().BeEquivalentTo("evt_alpine_failed", "evt_bernina_failed");
            }

            // The digest, claimed by its notification under SQL Server's unique key: once a day.
            var morning = HealthDigestWindowNow(now);
            async Task<int> Sent(DateTime at)
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IHealthDigest>().SendDueAsync(at);
            }
            (await Sent(morning)).Should().Be(2);
            (await Sent(morning.AddMinutes(15))).Should().Be(0);
            using (var scope = app.Services.CreateScope())
            {
                await using var db = scope.ServiceProvider.EveryWorkspace();
                db.OutboundMessages.Count(m => m.Source == OutboundSources.HealthDigest).Should().Be(2);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    /// <summary>08:30 Zurich on the morning whose digest covers the failed run seeded an hour before <paramref name="now"/>.</summary>
    private static DateTime HealthDigestWindowNow(DateTime now)
    {
        var zone = ACI.Application.Common.RegionalSettings.ZoneOf("Europe/Zurich");
        var run = TimeZoneInfo.ConvertTimeFromUtc(now.AddHours(-1), zone);
        var morning = run.AddHours(-8).Date.AddDays(1).AddHours(8.5);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(morning, DateTimeKind.Unspecified), zone);
    }
}
