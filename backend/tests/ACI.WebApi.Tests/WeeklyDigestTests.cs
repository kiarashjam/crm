using System.Net;
using System.Text.Json;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The weekly report digest: opt-in, for owners and managers, once a week from Monday
/// 07:00 in the recipient's time zone, through the app's email sender.
/// </summary>
public class WeeklyDigestTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
    // Monday 2 November 2026, 07:30 in Zurich (CET, UTC+1).
    private static readonly DateTime MondayMorning = new(2026, 11, 2, 6, 30, 0, DateTimeKind.Utc);

    private sealed class Outbox
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();
        public bool Accepts { get; set; } = true;

        public IEmailSender Sender()
        {
            var sender = new Mock<IEmailSender>();
            sender.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string to, string _, string subject, string body, CancellationToken _) =>
                {
                    if (Accepts) Sent.Add((to, subject, body));
                    return Task.FromResult(Accepts);
                });
            return sender.Object;
        }
    }

    private static WeeklyDigestService Digest(AppDbContext db, Outbox outbox, string smtpHost = "smtp.example.ch") =>
        new(db, new ReportingService(db), outbox.Sender(),
            Options.Create(new EmailSettings { SmtpHost = smtpHost, FrontendBaseUrl = "https://crm.example.ch" }),
            NullLogger<WeeklyDigestService>.Instance);

    private static async Task<int> SendDueAsync(IServiceProvider services, Outbox outbox, DateTime now, string smtpHost = "smtp.example.ch")
    {
        using var scope = services.CreateScope();
        return await Digest(scope.ServiceProvider.GetRequiredService<AppDbContext>(), outbox, smtpHost).SendDueAsync(now);
    }

    private static async Task OptInAsync(IServiceProvider services, Guid userId, bool enabled = true)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.UserSettings.FindAsync(userId);
        if (settings == null)
        {
            settings = new UserSettings { UserId = userId, BrandName = "x", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
            db.UserSettings.Add(settings);
        }
        settings.WeeklyDigestEnabled = enabled;
        settings.Timezone = "Europe/Zurich";
        await db.SaveChangesAsync();
    }

    [Theory]
    // Monday 06:59 in Zurich: last week's digest is still the latest one due.
    [InlineData("2026-11-02T05:59:00Z", "2026-10-26T06:00:00Z", "2026-10-19", "2026-10-25")]
    // Monday 07:00 in Zurich: this week's falls due, for Monday 26 October to Sunday 1 November.
    [InlineData("2026-11-02T06:00:00Z", "2026-11-02T06:00:00Z", "2026-10-26", "2026-11-01")]
    [InlineData("2026-11-05T12:00:00Z", "2026-11-02T06:00:00Z", "2026-10-26", "2026-11-01")]
    public void TheDigestIsDueFromSevenOnMondayInTheRecipientsTimeZone(string now, string due, string first, string last)
    {
        var week = WeeklyDigestService.WeekFor(DateTime.Parse(now).ToUniversalTime(), Zurich);
        week.DueUtc.Should().Be(DateTime.Parse(due).ToUniversalTime());
        week.FirstDay.Should().Be(DateOnly.Parse(first));
        week.LastDay.Should().Be(DateOnly.Parse(last));
        // The week runs from Zurich's Monday midnight, which on 26 October (after the
        // clocks went back) is 23:00 UTC the day before.
        if (first == "2026-10-26") week.WeekStartUtc.Should().Be(new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task AnOwnerWhoOptedInGetsLastWeeksFiguresOnceAWeek()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        await OptInAsync(factory.Services, ownerId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Deals.Add(new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Fondation Léman", Value = "45000", Currency = "CHF",
                IsWon = true, ClosedAtUtc = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc), CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            db.Deals.Add(new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Banque Léman", Value = "20000", Currency = "EUR",
                Probability = 50, ExpectedCloseDateUtc = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        }
        var outbox = new Outbox();

        (await SendDueAsync(factory.Services, outbox, MondayMorning)).Should().Be(1);

        var (to, subject, body) = outbox.Sent.Single();
        to.Should().Be("owner@example.ch");
        subject.Should().Contain("26 Oct").And.Contain("1 Nov 2026");
        body.Should().Contain("Pavillon46");
        body.Should().Contain("Won: 1 deal, CHF 45,000");
        body.Should().Contain("Expected to close in November: 1 deal, EUR 20,000 (weighted EUR 10,000)");
        body.Should().Contain("https://crm.example.ch/reports");

        (await SendDueAsync(factory.Services, outbox, MondayMorning.AddHours(5))).Should().Be(0, "this week's has gone");
        (await SendDueAsync(factory.Services, outbox, MondayMorning.AddDays(7))).Should().Be(1, "and next week's goes next Monday");
        outbox.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnlyOwnersAndManagersWhoTurnedItOnGetOne()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (_, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var (_, managerId) = await SignInAsync(factory, "manager@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);
        await OptInAsync(factory.Services, repId);
        await OptInAsync(factory.Services, managerId);
        var outbox = new Outbox();

        (await SendDueAsync(factory.Services, outbox, MondayMorning)).Should().Be(1);

        outbox.Sent.Select(s => s.To).Should().Equal("manager@example.ch");
    }

    [Fact]
    public async Task AWeekThatCouldNotBeSentIsTriedAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        await CreateAsync(factory, ownerId, "Pavillon46");
        await OptInAsync(factory.Services, ownerId);
        var outbox = new Outbox();

        (await SendDueAsync(factory.Services, outbox, MondayMorning, smtpHost: "")).Should().Be(0, "no mail server, nothing to send with");
        outbox.Accepts = false;
        (await SendDueAsync(factory.Services, outbox, MondayMorning)).Should().Be(0);
        outbox.Accepts = true;
        (await SendDueAsync(factory.Services, outbox, MondayMorning.AddHours(1))).Should().Be(1, "the week was given back when the send failed");
    }

    [Fact]
    public async Task TheSwitchIsSavedWithTheOtherSettingsAndOffUntilTurnedOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");

        var before = JsonDocument.Parse(await client.GetStringAsync("/api/settings")).RootElement;
        before.GetProperty("weeklyDigestEnabled").GetBoolean().Should().BeFalse();

        var put = await client.PutAsync("/api/settings", Body(new { weeklyDigestEnabled = true }));
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var after = JsonDocument.Parse(await client.GetStringAsync("/api/settings")).RootElement;
        after.GetProperty("weeklyDigestEnabled").GetBoolean().Should().BeTrue();

        // Another setting saved later leaves it on.
        (await client.PutAsync("/api/settings", Body(new { jobTitle = "Director" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.Services.Read(db => db.UserSettings.AsNoTracking().Single(s => s.UserId == userId).WeeklyDigestEnabled)).Should().BeTrue();
    }

    [SqlServerFact]
    public async Task OnSqlServerTheWeekIsClaimedOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            (await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }))).StatusCode.Should().Be(HttpStatusCode.Created);
            (await owner.PutAsync("/api/settings", Body(new { weeklyDigestEnabled = true, timezone = "Europe/Zurich" }))).StatusCode.Should().Be(HttpStatusCode.OK);
            var outbox = new Outbox();

            (await SendDueAsync(app.Services, outbox, MondayMorning)).Should().Be(1);
            (await SendDueAsync(app.Services, outbox, MondayMorning.AddMinutes(1))).Should().Be(0);
            outbox.Sent.Should().HaveCount(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [WeeklyDigestSentAtUtc] FROM [UserSettings] WHERE [UserId] = '{ownerId}'"))
                .Should().Be(MondayMorning);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
