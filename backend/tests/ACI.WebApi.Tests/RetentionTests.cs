using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace's retention settings, applied: by hand, or by the daily run.
/// </summary>
/// <remarks>
/// Nothing was ever deleted on a schedule. Every rule is off until the workspace sets a
/// period; a run removes only what its periods say, keeps the books, records what it did,
/// and runs once per workspace and day however many servers look.
/// </remarks>
public class RetentionTests
{
    private sealed record Seeded(
        Guid Org, Guid StaleLead, Guid StaleContact, Guid RecentLead, Guid MemberLead, Guid FormerMember, Guid OwingMember, Guid ActiveMember,
        Guid OldMessage, Guid OldActivity, Guid RecentMessage, Guid OldAudit, Guid RecentAudit, Guid OldHook);

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Seeded Seed)> ClubAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, owner) = await SignInAsync(factory, "olga@pavillon46.ch");
        var org = await CreateAsync(factory, owner, "Pavillon46");
        client.In(org);

        var now = DateTime.UtcNow;
        var old = now.AddYears(-3);
        var ancient = now.AddYears(-12);
        await using var db = factory.Services.EveryWorkspace();
        // Seeded as history: the audit trail would date everything today.
        db.RecordsAuditTrail = false;
        var staleLead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Old Prospect", Email = "old@example.ch", Status = "New", CreatedAtUtc = old };
        var staleContact = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Old Prospect", Email = "old@example.ch", ConvertedFromLeadId = staleLead.Id, CreatedAtUtc = old };
        staleLead.ConvertedToContactId = staleContact.Id;
        var recentLead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "New Prospect", Email = "new@example.ch", Status = "New", CreatedAtUtc = now.AddMonths(-1) };
        var memberLead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Joined Long Ago", Email = "joined@example.ch", Status = "New", CreatedAtUtc = old };
        ClubMember Member(string first, string status, decimal balance = 0, Guid? leadId = null) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = owner, FirstName = first, LastName = "Former", Email = $"{first.ToLowerInvariant()}@example.ch",
            Tier = "Gold", Status = status, JoinedAtUtc = ancient, RenewsAtUtc = ancient.AddYears(1), HouseAccountBalance = balance, LeadId = leadId,
        };
        var former = Member("Gone", "Cancelled");
        var owing = Member("Owing", "Lapsed", balance: -20);
        var active = Member("Still", "Active", leadId: memberLead.Id);
        db.AddRange(staleLead, staleContact, recentLead, memberLead, former, owing, active);
        db.ClubCharges.Add(new ClubCharge { Id = Guid.NewGuid(), OrganizationId = org, MemberId = former.Id, MemberName = "Gone Former", Description = "Dues", Amount = 100m, PostedAtUtc = ancient, CreatedAtUtc = ancient });

        var oldMessage = new OutboundMessage
        {
            Id = Guid.NewGuid(), OrganizationId = org, Status = OutboundStatuses.Sent, To = "someone@example.ch", Subject = "Your statement",
            Body = "Dear Someone, here it is.", TextBody = "Dear Someone, here it is.", HtmlBody = "<p>Dear Someone</p>", CreatedAtUtc = old, SentAtUtc = old,
        };
        var oldActivity = new Activity { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "email", Subject = "Your statement", Body = "Dear Someone, here it is.", LeadId = recentLead.Id, CreatedAtUtc = old, OccurredAtUtc = old };
        oldMessage.ActivityId = oldActivity.Id;
        var recentMessage = new OutboundMessage { Id = Guid.NewGuid(), OrganizationId = org, Status = OutboundStatuses.Sent, To = "x@example.ch", Subject = "Hello", Body = "Recent words.", CreatedAtUtc = now.AddDays(-3) };
        var hook = new OutboundMessage { Id = Guid.NewGuid(), OrganizationId = org, Channel = OutboundChannels.Webhook, Source = OutboundSources.Webhook, Status = OutboundStatuses.Sent, EventName = "lead.created", PayloadJson = "{\"Name\":\"Old Prospect\"}", CreatedAtUtc = old };
        db.AddRange(oldMessage, oldActivity, recentMessage, hook);
        db.OutboundAttachments.Add(new OutboundAttachment { Id = Guid.NewGuid(), OrganizationId = org, OutboundMessageId = oldMessage.Id, FileName = "statement.pdf", Content = [1, 2, 3], CreatedAtUtc = old });
        db.CopyHistoryItems.Add(new CopyHistoryItem { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "email", Copy = "Dear Someone", RecipientName = "Someone", RecipientId = "x", CreatedAtUtc = old });
        var oldAudit = new AuditEvent { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = owner, EntityType = "contact", EntityLabel = "Someone Old", Action = "updated", Summary = "Updated phone", ActorName = "Olga", CreatedAtUtc = old };
        var recentAudit = new AuditEvent { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = owner, EntityType = "contact", EntityLabel = "Someone New", Action = "updated", Summary = "Updated phone", ActorName = "Olga", CreatedAtUtc = now };
        db.AuditEvents.AddRange(oldAudit, recentAudit);
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Title = "Someone Old got in touch", CreatedAtUtc = old });
        await db.SaveChangesAsync();
        return (factory, client, new Seeded(org, staleLead.Id, staleContact.Id, recentLead.Id, memberLead.Id, former.Id, owing.Id, active.Id,
            oldMessage.Id, oldActivity.Id, recentMessage.Id, oldAudit.Id, recentAudit.Id, hook.Id));
    }

    private static object Periods(int? leads = null, int? members = null, int? mail = null, int? details = null) => new
    {
        marketingConsent = "optional", leadRetentionMonths = leads, memberRetentionYears = members, mailRetentionMonths = mail, detailRetentionMonths = details,
    };

    [Fact]
    public async Task NothingIsRemovedUntilTheWorkspaceSetsAPeriod()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        var preview = await Ok(await client.GetAsync("/api/privacy/retention/preview"), "preview");
        preview.GetProperty("anythingOn").GetBoolean().Should().BeFalse();
        preview.GetProperty("rules").EnumerateArray().Should().OnlyContain(r => r.GetProperty("count").GetInt32() == 0);
        var run = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");
        (run.GetProperty("leadsDeleted").GetInt32() + run.GetProperty("membersAnonymised").GetInt32() + run.GetProperty("messagesPurged").GetInt32()
            + run.GetProperty("detailsPurged").GetInt32()).Should().Be(0);
        (await factory.Services.Read(db => db.Leads.Count(l => l.OrganizationId == s.Org))).Should().Be(3);

        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IRetention>().RunScheduledAsync(DateTime.UtcNow)).Should().Be(0, "no workspace set a period");
    }

    [Fact]
    public async Task ARunRemovesWhatThePeriodsSay_KeepsTheBooksAndTheRest_AndRecordsWhatItDid()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(leads: 24, members: 10, mail: 12, details: 12))), "set the periods");

        var preview = await Ok(await client.GetAsync("/api/privacy/retention/preview"), "preview");
        var counts = preview.GetProperty("rules").EnumerateArray().ToDictionary(r => r.GetProperty("rule").GetString()!, r => r.GetProperty("count").GetInt32());
        counts["leads"].Should().Be(1, "only the old lead nobody worked on; the recent one and the one a member came from stay");
        counts["members"].Should().Be(1, "the one who left long ago with nothing open; not the one who still owes, nor the active member");
        counts["mail"].Should().Be(2, "the old email's text and the old generated copy");
        counts["details"].Should().Be(3, "the old audit line's name, the old webhook payload and the old notification");
        preview.GetProperty("kept").EnumerateArray().Select(k => k.GetString()).Should().Contain(k => k!.Contains("money is still open"));
        (await factory.Services.Read(db => db.Leads.Any(l => l.Id == s.StaleLead))).Should().BeTrue("a preview removes nothing");

        var run = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");
        (run.GetProperty("leadsDeleted").GetInt32(), run.GetProperty("membersAnonymised").GetInt32(), run.GetProperty("messagesPurged").GetInt32(), run.GetProperty("detailsPurged").GetInt32())
            .Should().Be((1, 1, 2, 3));
        run.GetProperty("status").GetString().Should().Be(RetentionRunStatuses.Succeeded);

        await using var db = factory.Services.EveryWorkspace();
        db.Leads.Any(l => l.Id == s.StaleLead).Should().BeFalse();
        db.Contacts.Any(c => c.Id == s.StaleContact).Should().BeFalse("the contact made for the lead went with it: nobody worked on it either");
        db.Leads.Any(l => l.Id == s.RecentLead).Should().BeTrue();
        db.Leads.Any(l => l.Id == s.MemberLead).Should().BeTrue();

        var former = db.ClubMembers.Single(m => m.Id == s.FormerMember);
        former.ErasedAtUtc.Should().NotBeNull();
        former.Email.Should().BeEmpty();
        $"{former.FirstName} {former.LastName}".Should().StartWith("Erased member");
        db.ClubCharges.Single(c => c.MemberId == s.FormerMember).Amount.Should().Be(100m, "the books keep the amount");
        db.ClubMembers.Single(m => m.Id == s.OwingMember).Email.Should().Be("owing@example.ch");
        db.ClubMembers.Single(m => m.Id == s.ActiveMember).Email.Should().Be("still@example.ch");
        db.EmailSuppressions.Any(x => x.OrganizationId == s.Org).Should().BeFalse("nobody asked to stop hearing from the club");

        var oldMessage = db.OutboundMessages.Single(m => m.Id == s.OldMessage);
        (oldMessage.TextBody, oldMessage.HtmlBody).Should().Be(((string?)null, (string?)null));
        oldMessage.Body.Should().Contain("removed");
        (oldMessage.To, oldMessage.Subject, oldMessage.Status).Should().Be(("someone@example.ch", "Your statement", OutboundStatuses.Sent), "who, what and whether it went stay");
        db.OutboundAttachments.Any(a => a.OutboundMessageId == s.OldMessage).Should().BeFalse();
        db.Activities.Single(a => a.Id == s.OldActivity).Body.Should().Contain("removed", "the timeline's copy of the email goes too");
        db.OutboundMessages.Single(m => m.Id == s.RecentMessage).Body.Should().Be("Recent words.");
        db.CopyHistoryItems.Any(c => c.OrganizationId == s.Org).Should().BeFalse();

        db.AuditEvents.Single(e => e.Id == s.OldAudit).Should().Match<AuditEvent>(e => e.EntityLabel == null && e.Summary == "Updated phone" && e.ActorName == "Olga");
        db.AuditEvents.Single(e => e.Id == s.RecentAudit).EntityLabel.Should().Be("Someone New");
        db.OutboundMessages.Single(m => m.Id == s.OldHook).PayloadJson.Should().BeNull();
        db.Notifications.Any(n => n.OrganizationId == s.Org).Should().BeFalse();

        var line = db.AuditEvents.Single(e => e.OrganizationId == s.Org && e.Action == "purged");
        line.Summary.Should().Contain("1 lead deleted").And.Contain("1 former member anonymised");
        db.PersonErasures.Where(e => e.OrganizationId == s.Org).Select(e => e.Trigger).Should().OnlyContain(t => t == PersonErasureTriggers.Retention);

        var again = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run again");
        (again.GetProperty("leadsDeleted").GetInt32() + again.GetProperty("membersAnonymised").GetInt32() + again.GetProperty("messagesPurged").GetInt32()
            + again.GetProperty("detailsPurged").GetInt32()).Should().Be(0, "what a run removes is gone for the next one");
        var runs = await Ok(await client.GetAsync("/api/privacy/retention/runs"), "the log");
        runs.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ALeadWhoseAddressHasABookingToComeIsNotStale_AndTheBookingIsLeftAlone()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        // The old prospect never became a member, but a member brings them to next week's gala as
        // a guest, and they booked the private dining room: a guest has only an address.
        var now = DateTime.UtcNow;
        var registrationId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        await using (var db = factory.Services.EveryWorkspace())
        {
            var gala = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = s.Org, Name = "Winter gala", StartAtUtc = now.AddDays(7), EndAtUtc = now.AddDays(7).AddHours(4), Capacity = 50, Status = ClubEventStatuses.Published, CreatedAtUtc = now, UpdatedAtUtc = now };
            var room = new ClubResource { Id = Guid.NewGuid(), OrganizationId = s.Org, Name = "Private dining", Capacity = 10, CreatedAtUtc = now, UpdatedAtUtc = now };
            db.AddRange(gala, room,
                new ClubEventRegistration
                {
                    Id = registrationId, OrganizationId = s.Org, EventId = gala.Id, Name = "Old Prospect", Email = "Old@Example.ch", TicketTier = TicketTiers.Guest,
                    HostMemberId = s.ActiveMember, HostName = "Still Former", Status = RegistrationStatuses.Reserved, RegisteredAtUtc = now.AddDays(-2), CreatedAtUtc = now, UpdatedAtUtc = now,
                },
                new ClubReservation
                {
                    Id = reservationId, OrganizationId = s.Org, ResourceId = room.Id, ResourceName = room.Name, MemberName = "Old Prospect", MemberEmail = "old@example.ch",
                    PartySize = 4, StartAtUtc = now.AddDays(5), EndAtUtc = now.AddDays(5).AddHours(2), Status = ReservationStatuses.Confirmed, CreatedAtUtc = now.AddDays(-1),
                });
            await db.SaveChangesAsync();
        }
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(leads: 24))), "set the period");

        var preview = await Ok(await client.GetAsync("/api/privacy/retention/preview"), "preview");
        preview.GetProperty("rules").EnumerateArray().Single(r => r.GetProperty("rule").GetString() == "leads").GetProperty("count").GetInt32()
            .Should().Be(0, "someone with a booking still to come is in touch with the club");
        var run = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");
        run.GetProperty("leadsDeleted").GetInt32().Should().Be(0);

        await using var check = factory.Services.EveryWorkspace();
        check.Leads.Any(l => l.Id == s.StaleLead).Should().BeTrue();
        check.ClubEventRegistrations.Single(r => r.Id == registrationId).Status.Should().Be(RegistrationStatuses.Reserved, "a retention run cancels nobody's place");
        check.ClubReservations.Single(r => r.Id == reservationId).Status.Should().Be(ReservationStatuses.Confirmed, "nor anybody's booking");
    }

    [Fact]
    public async Task AnUnsubscribeOutlivesTheLeadTheRetentionSettingsRemove_AsAFingerprint()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.EmailSuppressions.Add(new EmailSuppression
            {
                Id = Guid.NewGuid(), OrganizationId = s.Org, Email = "old@example.ch", Reason = EmailSuppressionReasons.Unsubscribed,
                Detail = "old@example.ch used the unsubscribe link", CreatedAtUtc = DateTime.UtcNow.AddYears(-3),
            });
            await db.SaveChangesAsync();
        }
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(leads: 24))), "set the period");
        var run = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");
        run.GetProperty("leadsDeleted").GetInt32().Should().Be(1);

        var suppressions = await factory.Services.Read(db => db.EmailSuppressions.Where(x => x.OrganizationId == s.Org).ToList());
        suppressions.Should().ContainSingle("the person asked to hear nothing more; that holds after their lead goes");
        suppressions[0].Email.Should().Be(ACI.Application.Common.ErasedAddresses.Key("old@example.ch"), "the address itself is not kept");
        suppressions[0].Reason.Should().Be(EmailSuppressionReasons.Unsubscribed);
        (suppressions[0].Detail ?? "").Should().NotContain("old@example.ch");

        using var scope = factory.Services.CreateScope();
        using var acting = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(s.Org, null);
        var refused = await scope.ServiceProvider.GetRequiredService<IOutbox>().QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = s.Org, Source = OutboundSources.Composer, To = ["old@example.ch"], Subject = "We have news", Body = "Come and see.",
        });
        refused.IsSuccess.Should().BeFalse("an imported list or a new lead with the address is still not mailed");
        refused.Error.Code.Should().Be("Email.Suppressed");
    }

    [Fact]
    public async Task TheDetailRuleAlsoTakesTheNameOffTheLineThatSaysSomeonesDataWasDownloaded()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        var exportLine = Guid.NewGuid();
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.RecordsAuditTrail = false;
            db.AuditEvents.Add(new AuditEvent
            {
                Id = exportLine, OrganizationId = s.Org, OwnerUserId = Guid.Empty, EntityType = "privacy", EntityId = Guid.NewGuid(), EntityLabel = "Old Prospect",
                Action = "exported", Summary = "Everything held about this person downloaded (12 items from 2 records)", ActorName = "Olga", CreatedAtUtc = DateTime.UtcNow.AddYears(-3),
            });
            await db.SaveChangesAsync();
        }
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(details: 12))), "set the period");
        await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");

        var line = await factory.Services.Read(db => db.AuditEvents.Single(e => e.Id == exportLine));
        line.EntityLabel.Should().BeNull("an export's line names the person it was about, like any other line");
        line.Summary.Should().Contain("downloaded", "what was done and when stay");
    }

    [Fact]
    public async Task OneRunAtATime_AndARunCutOffLongAgoNoLongerHoldsTheWorkspace()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(leads: 24))), "set the period");
        var at = DateTime.UtcNow;
        var going = new RetentionRun
        {
            Id = Guid.NewGuid(), OrganizationId = s.Org, Trigger = RetentionRunTriggers.Manual, Status = RetentionRunStatuses.Running, StartedAtUtc = at.AddMinutes(-2),
        };
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.RetentionRuns.Add(going);
            await db.SaveChangesAsync();
        }

        // Two runs at once would each anonymise the same former member, under two numbers.
        var refused = await client.PostAsync("/api/privacy/retention/run", null);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Privacy.Retention.Running.Conflict");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRetention>().RunScheduledAsync(at);
        (await factory.Services.Read(db => db.RetentionRuns.Count(r => r.OrganizationId == s.Org))).Should().Be(1, "the daily run leaves the day for its next look");
        (await factory.Services.Read(db => db.Leads.Any(l => l.Id == s.StaleLead))).Should().BeTrue("nothing ran");

        // A run still marked running hours later was cut off (its server stopped half way).
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.RetentionRuns.Single(r => r.Id == going.Id).StartedAtUtc = at.AddHours(-3);
            await db.SaveChangesAsync();
        }
        var ran = await Ok(await client.PostAsync("/api/privacy/retention/run", null), "run now");
        ran.GetProperty("leadsDeleted").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheDailyRunTakesEachWorkspacesDayOnce_AfterFourInTheMorningItsTime()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Periods(details: 12))), "set a period");

        // Zurich is UTC+1 in November: 02:30 UTC is 03:30 there, before the run; 05:00 UTC is 06:00.
        var night = new DateTime(2026, 11, 13, 2, 30, 0, DateTimeKind.Utc);
        var morning = new DateTime(2026, 11, 13, 5, 0, 0, DateTimeKind.Utc);
        int Run(DateTime at)
        {
            using var scope = factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IRetention>().RunScheduledAsync(at).GetAwaiter().GetResult();
        }

        Run(night).Should().Be(0, "the workspace's day has not reached 04:00");
        Run(morning).Should().Be(1);
        Run(morning.AddHours(1)).Should().Be(0, "the day is taken");
        Run(morning.AddDays(1)).Should().Be(1, "the next day runs again");

        var runs = await factory.Services.Read(db => db.RetentionRuns.Where(r => r.OrganizationId == s.Org).ToList());
        runs.Select(r => r.ScheduledFor).Should().BeEquivalentTo(new DateOnly?[] { new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 14) });
        runs.Should().OnlyContain(r => r.Trigger == RetentionRunTriggers.Schedule && r.Status == RetentionRunStatuses.Succeeded);
        runs.Sum(r => r.DetailsPurged).Should().Be(3);
    }
}
