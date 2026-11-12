using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A person's data rights: finding them across the workspace, exporting everything held about
/// them, and erasing them.
/// </summary>
/// <remarks>
/// Before, staff could delete one row and leave the person everywhere else (the contact made
/// for their lead, activities, sent mail, enrolments, the audit trail, club records), and
/// could not hand anyone the data held about them at all.
/// </remarks>
public class PersonDataTests
{
    private const string AnnaEmail = "anna.keller@example.ch";

    /// <summary>One person, Anna, held as a lead, its contact, a member and an application, with something in every area.</summary>
    internal sealed record Seeded(
        Guid Org, Guid Owner, Guid LeadId, Guid ContactId, Guid MemberId, Guid ApplicationId,
        Guid BobLeadId, Guid BobMemberId, Guid OtherOrgMemberId, Guid FutureReservationId, Guid PastReservationId,
        Guid FutureRegistrationId, Guid SentMessageId, Guid QueuedMessageId, Guid BobMessageId, Guid BobActivityId,
        Guid ChargeId, Guid PaymentId, Guid SubscriptionId);

    internal static async Task<Seeded> SeedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, Guid owner, Guid org)
    {
        var other = Guid.NewGuid();
        await using var db = factory.Services.EveryWorkspace();
        db.Organizations.Add(new Organization { Id = other, Name = "Another club", OwnerUserId = owner, CreatedAtUtc = DateTime.UtcNow });

        var now = DateTime.UtcNow;
        var lead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Anna Keller", Email = AnnaEmail, Phone = "079 123 45 67", Status = "Qualified", CreatedAtUtc = now.AddDays(-30) };
        var contact = new Contact { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Anna Keller", Email = AnnaEmail, Phone = "079 123 45 67", ConvertedFromLeadId = lead.Id, CreatedAtUtc = now.AddDays(-30) };
        lead.ConvertedToContactId = contact.Id;
        var member = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = owner, FirstName = "Anna", LastName = "Keller", Email = AnnaEmail, Phone = "079 123 45 67",
            Tier = "Gold", Status = "Active", JoinedAtUtc = now.AddYears(-1), RenewsAtUtc = now.AddMonths(2), DuesAmount = 299, HouseAccountBalance = -30,
            Notes = "Prefers the window table", ContactId = contact.Id, LeadId = lead.Id,
        };
        var application = new ClubApplication
        {
            Id = Guid.NewGuid(), OrganizationId = org, FirstName = "Anna", LastName = "Keller", Email = AnnaEmail, RequestedTier = "Gold",
            Status = ApplicationStatuses.Approved, MemberId = member.Id, LeadId = lead.Id, ReasonForJoining = "Friends are members", SubmittedAtUtc = now.AddYears(-1), CreatedAtUtc = now.AddYears(-1),
        };
        var bobLead = new Lead { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Bob Brown", Email = "bob@example.ch", Status = "New", CreatedAtUtc = now };
        var bobMember = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = owner, FirstName = "Bob", LastName = "Brown", Email = "bob@example.ch",
            Tier = "Gold", Status = "Active", JoinedAtUtc = now.AddYears(-2), RenewsAtUtc = now.AddMonths(3),
        };
        var otherOrgMember = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = other, OwnerUserId = owner, FirstName = "Anna", LastName = "Keller", Email = AnnaEmail,
            Tier = "Gold", Status = "Active", JoinedAtUtc = now.AddYears(-1), RenewsAtUtc = now.AddMonths(2),
        };
        db.AddRange(lead, contact, member, application, bobLead, bobMember, otherOrgMember);

        var bobActivity = new Activity { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "note", Subject = "Referral", Body = "Anna Keller sent him to us.", LeadId = bobLead.Id, CreatedAtUtc = now };
        db.Activities.AddRange(
            new Activity { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "note", Subject = "Website form", Body = $"Name: Anna Keller\nEmail: {AnnaEmail}\nPhone: 079 123 45 67", LeadId = lead.Id, CreatedAtUtc = now.AddDays(-30) },
            new Activity { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "call", Subject = "Called about Gold", ContactId = contact.Id, CreatedAtUtc = now.AddDays(-20) },
            bobActivity);
        db.TaskItems.Add(new TaskItem { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Title = "Send Anna the brochure", ContactId = contact.Id, CreatedAtUtc = now });
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Type = "lead", Title = "A lead got in touch again", Message = "Anna Keller sent a new inquiry.", Link = $"/leads/{lead.Id}", CreatedAtUtc = now });

        var sent = new OutboundMessage
        {
            Id = Guid.NewGuid(), OrganizationId = org, Source = OutboundSources.Composer, Status = OutboundStatuses.Sent, To = AnnaEmail, RecipientName = "Anna Keller",
            Subject = "Welcome, Anna Keller", Body = "Dear Anna, welcome to the club.", TextBody = "Dear Anna, welcome to the club.", HtmlBody = "<p>Dear Anna</p>",
            LeadId = lead.Id, CreatedAtUtc = now.AddDays(-10), SentAtUtc = now.AddDays(-10),
        };
        var queued = new OutboundMessage
        {
            Id = Guid.NewGuid(), OrganizationId = org, Source = OutboundSources.Member, Status = OutboundStatuses.Queued, To = AnnaEmail, RecipientName = "Anna Keller",
            Subject = "Your statement", Body = "Your statement is attached.", MemberId = member.Id, CreatedAtUtc = now, NextAttemptAtUtc = now.AddMinutes(5),
        };
        var bobMessage = new OutboundMessage
        {
            Id = Guid.NewGuid(), OrganizationId = org, Source = OutboundSources.Composer, Status = OutboundStatuses.Sent, To = "bob@example.ch", RecipientName = "Bob Brown",
            Subject = "Thank you", Body = $"Thanks for coming with Anna Keller ({AnnaEmail}).", LeadId = bobLead.Id, CreatedAtUtc = now, SentAtUtc = now,
        };
        db.OutboundMessages.AddRange(sent, queued, bobMessage);
        db.InboundEmails.Add(new InboundEmail { Id = Guid.NewGuid(), OrganizationId = org, OutboundMessageId = sent.Id, FromAddress = AnnaEmail, Subject = "Re: Welcome", MatchedBy = "token", ReceivedAtUtc = now });
        db.ConsentRecords.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(), OrganizationId = org, SubjectType = ConsentSubjects.Member, SubjectId = member.Id, Channel = ConsentChannels.Email,
            Purpose = ConsentPurposes.Marketing, Granted = true, Source = ConsentSources.Form, Address = AnnaEmail, RecordedAtUtc = now, IsCurrent = true,
        });
        db.EmailSuppressions.Add(new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = org, Email = AnnaEmail, Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = now });

        var charge = new ClubCharge { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = "Anna Keller", Kind = "Dining", Description = "Dinner", Amount = 50m, Currency = "CHF", VatRate = 8.1m, PostedAtUtc = now.AddDays(-3), CreatedAtUtc = now.AddDays(-3) };
        var payment = new ClubPayment { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = "Anna Keller", Method = "Cash", Amount = 20m, Currency = "CHF", ReceivedAtUtc = now.AddDays(-2), Notes = "Anna paid at the bar", CreatedAtUtc = now.AddDays(-2) };
        var subscription = new ClubSubscription { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = "Anna Keller", Name = "Gold dues", Amount = 299m, Currency = "CHF", Cadence = "Monthly", Status = "Active", StartedAtUtc = now.AddYears(-1), NextChargeAtUtc = now.AddDays(10), CreatedAtUtc = now.AddYears(-1) };
        db.AddRange(charge, payment, subscription);
        db.ClubHouseAccounts.Add(new ClubHouseAccount { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, Currency = "CHF", OpenedAtUtc = now.AddYears(-1) });
        db.ClubVisits.Add(new ClubVisit { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = "Anna Keller", MemberTier = "Gold", Venue = "Bar", VisitedAtUtc = now.AddDays(-5), PointsAwarded = 50, Notes = "With her sister" });
        db.LoyaltyLedger.Add(new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = member.Id, MemberName = "Anna Keller", Points = 50, CreatedAtUtc = now.AddDays(-5) });

        var resource = new ClubResource { Id = Guid.NewGuid(), OrganizationId = org, Name = "Private dining", Capacity = 10, CreatedAtUtc = now, UpdatedAtUtc = now };
        var future = new ClubReservation { Id = Guid.NewGuid(), OrganizationId = org, ResourceId = resource.Id, ResourceName = resource.Name, MemberId = member.Id, MemberName = "Anna Keller", MemberEmail = AnnaEmail, PartySize = 2, StartAtUtc = now.AddDays(7), EndAtUtc = now.AddDays(7).AddHours(2), Status = ReservationStatuses.Confirmed, Notes = "Anniversary", CreatedAtUtc = now };
        var past = new ClubReservation { Id = Guid.NewGuid(), OrganizationId = org, ResourceId = resource.Id, ResourceName = resource.Name, MemberId = member.Id, MemberName = "Anna Keller", MemberEmail = AnnaEmail, PartySize = 2, StartAtUtc = now.AddDays(-7), EndAtUtc = now.AddDays(-7).AddHours(2), Status = ReservationStatuses.Completed, CreatedAtUtc = now.AddDays(-14) };
        var gala = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = org, Name = "Winter gala", StartAtUtc = now.AddDays(20), EndAtUtc = now.AddDays(20).AddHours(4), Capacity = 50, Status = ClubEventStatuses.Published, CreatedAtUtc = now, UpdatedAtUtc = now };
        var registration = new ClubEventRegistration { Id = Guid.NewGuid(), OrganizationId = org, EventId = gala.Id, MemberId = member.Id, Name = "Anna Keller", Email = AnnaEmail, Status = RegistrationStatuses.Reserved, RegisteredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.AddRange(resource, future, past, gala, registration);
        await db.SaveChangesAsync();

        return new Seeded(org, owner, lead.Id, contact.Id, member.Id, application.Id, bobLead.Id, bobMember.Id, otherOrgMember.Id,
            future.Id, past.Id, registration.Id, sent.Id, queued.Id, bobMessage.Id, bobActivity.Id, charge.Id, payment.Id, subscription.Id);
    }

    internal static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Seeded Seed)> ClubAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, owner) = await SignInAsync(factory, "olga@pavillon46.ch");
        var org = await CreateAsync(factory, owner, "Pavillon46");
        client.In(org);
        return (factory, client, await SeedAsync(factory, owner, org));
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static object AnnaSelection(Seeded s) => new
    {
        leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId },
        addresses = new[] { AnnaEmail },
    };

    [Fact]
    public async Task FindGathersTheLeadItsContactTheMemberAndTheApplication_AndNobodyElse()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        var byEmail = await Ok(await client.PostAsync("/api/privacy/find", Body(new { email = "Anna.Keller@Example.ch " })), "find by email");
        var records = byEmail.GetProperty("records").EnumerateArray().ToList();
        records.Select(r => (r.GetProperty("kind").GetString(), r.GetProperty("id").GetGuid())).Should().BeEquivalentTo(new[]
        {
            ("lead", s.LeadId), ("contact", s.ContactId), ("member", s.MemberId), ("application", s.ApplicationId),
        }, "the same address in any case, in this workspace only: not Bob, not another workspace's member");
        records.Should().OnlyContain(r => r.GetProperty("foundBy").GetString()!.Length > 0, "each says why it was found");

        var fromLead = await Ok(await client.PostAsync("/api/privacy/find", Body(new { leadId = s.LeadId })), "find from the lead");
        fromLead.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("id").GetGuid())
            .Should().BeEquivalentTo(new[] { s.LeadId, s.ContactId, s.MemberId, s.ApplicationId }, "the links lead to the same person");
        fromLead.GetProperty("records").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == s.LeadId)
            .GetProperty("foundBy").GetString().Should().Be("The record you started from");
    }

    [Fact]
    public async Task AnotherWorkspacesRecordIsNotFound_AndCannotBeExportedOrErased()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        (await client.PostAsync("/api/privacy/find", Body(new { memberId = s.OtherOrgMemberId }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/api/privacy/export", Body(new { memberIds = new[] { s.OtherOrgMemberId } }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { s.OtherOrgMemberId }, confirm = "ERASE" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var untouched = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == s.OtherOrgMemberId));
        untouched.FirstName.Should().Be("Anna");
        untouched.Email.Should().Be(AnnaEmail);
    }

    [Fact]
    public async Task OnlyWhoeverHandlesPrivacyRequestsMayFindExportOrErase()
    {
        var (factory, _, s) = await ClubAsync();
        using var __ = factory;
        var (rep, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, s.Org, repId, OrgMemberRole.Member);
        rep.In(s.Org);

        var res = await rep.PostAsync("/api/privacy/find", Body(new { email = AnnaEmail }));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, "an old Member keeps what Members could do, and that never included this");
        (await res.Content.ReadAsStringAsync()).Should().Contain("Permission.Required");
        (await rep.GetAsync("/api/privacy/erasures")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the log is the privacy handlers' too");
    }

    [Fact]
    public async Task TheExportHoldsEverythingAboutThePerson_WithAReadableSummary_AndIsAudited()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        var res = await client.PostAsync("/api/privacy/export", Body(AnnaSelection(s)));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        using var zip = new ZipArchive(await res.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        string Read(string path)
        {
            var entry = zip.GetEntry(path);
            entry.Should().NotBeNull($"{path} should be in the export");
            using var reader = new StreamReader(entry!.Open());
            return reader.ReadToEnd();
        }

        var summary = Read("summary.txt");
        summary.Should().Contain("Personal data held by Pavillon46").And.Contain("Anna Keller").And.Contain("records/leads.json");
        Read("records/leads.json").Should().Contain(AnnaEmail).And.Contain("079 123 45 67");
        Read("records/members.json").Should().Contain("Prefers the window table");
        Read("records/applications.json").Should().Contain("Friends are members");
        Read("timeline/activities.json").Should().Contain("Website form").And.Contain("Called about Gold");
        Read("timeline/notes-mentioning-them.json").Should().Contain("Anna Keller sent him to us");
        Read("timeline/tasks.json").Should().Contain("Send Anna the brochure");
        Read("mail/sent.json").Should().Contain("Dear Anna, welcome to the club.");
        Read("mail/received.json").Should().Contain("Re: Welcome");
        Read("consent/consent-records.json").Should().Contain("marketing");
        Read("club/house-account.json").Should().Contain("Dinner").And.Contain("Anna paid at the bar");
        Read("club/subscriptions.json").Should().Contain("Gold dues");
        Read("club/reservations.json").Should().Contain("Anniversary");
        Read("club/events.json").Should().Contain("Winter gala");
        Read("club/visits.json").Should().Contain("With her sister");
        Read("audit/audit-trail.json").Should().Contain("Lead created");

        var everything = string.Join("\n", zip.Entries.Select(e => { using var r = new StreamReader(e.Open()); return r.ReadToEnd(); }));
        everything.Should().NotContain("bob@example.ch", "Bob is someone else: his records and the email to him stay out");
        everything.Should().NotContain("Another club");

        var line = await factory.Services.Read(db => db.AuditEvents.Single(e => e.EntityType == "privacy" && e.Action == "exported"));
        line.OrganizationId.Should().Be(s.Org);
        line.EntityId.Should().Be(s.MemberId);
        line.ActorName.Should().Be("olga@pavillon46.ch");
    }

    [Fact]
    public async Task ThePreviewSaysWhatWouldHappen_AndChangesNothing()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        var plan = await Ok(await client.PostAsync("/api/privacy/erasure/preview", Body(AnnaSelection(s))), "preview");
        var lines = plan.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("area").GetString()!, l => (l.GetProperty("action").GetString(), l.GetProperty("count").GetInt32()));
        lines["Leads"].Should().Be(("delete", 1));
        lines["Contacts"].Should().Be(("delete", 1));
        lines["Club member records"].Should().Be(("anonymise", 1));
        lines["Club money records"].Should().Be(("anonymise", 2));
        lines["Subscriptions"].Should().Be(("cancel", 1));
        lines["Bookings still to come"].Should().Be(("cancel", 1));
        lines["Event places still to come"].Should().Be(("cancel", 1));
        lines["Emails waiting to go to them"].Should().Be(("cancel", 1));
        lines["Notes on other records that mention them"].Should().Be(("anonymise", 1));
        plan.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("owes CHF 30.00"));
        plan.GetProperty("addressesSuppressed").EnumerateArray().Select(a => a.GetString()).Should().Equal(AnnaEmail);

        (await factory.Services.Read(db => db.Leads.Any(l => l.Id == s.LeadId))).Should().BeTrue("a preview changes nothing");
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == s.MemberId).Email)).Should().Be(AnnaEmail);
        (await factory.Services.Read(db => db.ClubReservations.Single(r => r.Id == s.FutureReservationId).Status)).Should().Be(ReservationStatuses.Confirmed);
    }

    [Fact]
    public async Task AnErasureMustBeConfirmed()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        var res = await client.PostAsync("/api/privacy/erasure", Body(AnnaSelection(s)));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Privacy.ConfirmationMissing");
        (await factory.Services.Read(db => db.Leads.Any(l => l.Id == s.LeadId))).Should().BeTrue();
    }

    [Fact]
    public async Task AnErasureDeletesWhatCanGo_AnonymisesWhatTheBooksKeep_AndCancelsWhatIsToCome()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;

        var result = await Ok(await client.PostAsync("/api/privacy/erasure", Body(new
        {
            leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId },
            addresses = new[] { AnnaEmail }, confirm = "ERASE", reference = "Letter of 12 November",
        })), "erase");
        result.GetProperty("number").GetInt32().Should().Be(1);
        result.GetProperty("pseudonym").GetString().Should().Be("Erased member 1");

        await using var db = factory.Services.EveryWorkspace();
        // Deleted.
        (await db.Leads.AnyAsync(l => l.Id == s.LeadId)).Should().BeFalse();
        (await db.Contacts.AnyAsync(c => c.Id == s.ContactId)).Should().BeFalse();
        (await db.ClubApplications.AnyAsync(a => a.Id == s.ApplicationId)).Should().BeFalse();
        (await db.Activities.AnyAsync(a => a.LeadId == s.LeadId || a.ContactId == s.ContactId)).Should().BeFalse();
        (await db.Activities.AnyAsync(a => a.Body != null && a.Body.Contains(AnnaEmail))).Should().BeFalse("the website form's summary went with the lead");
        (await db.TaskItems.AnyAsync(t => t.Title.Contains("Anna"))).Should().BeFalse();
        (await db.Notifications.AnyAsync(n => n.Message != null && n.Message.Contains("Anna Keller"))).Should().BeFalse();
        (await db.ConsentRecords.AnyAsync(c => c.SubjectId == s.MemberId)).Should().BeFalse();

        // The member stays for the books, under the pseudonym, with nothing personal left.
        var member = await db.ClubMembers.SingleAsync(m => m.Id == s.MemberId);
        $"{member.FirstName} {member.LastName}".Should().Be("Erased member 1");
        member.Email.Should().BeEmpty();
        member.Phone.Should().BeNull();
        member.Notes.Should().BeNull();
        member.ContactId.Should().BeNull();
        member.Status.Should().Be("Cancelled");
        member.ErasedAtUtc.Should().NotBeNull();

        // Money: every amount, date and VAT rate stays, under the pseudonym.
        var charge = await db.ClubCharges.SingleAsync(c => c.Id == s.ChargeId);
        (charge.Amount, charge.VatRate, charge.MemberName).Should().Be((50m, 8.1m, "Erased member 1"));
        var payment = await db.ClubPayments.SingleAsync(p => p.Id == s.PaymentId);
        (payment.Amount, payment.MemberName, payment.Notes).Should().Be((20m, "Erased member 1", (string?)null));
        var subscription = await db.ClubSubscriptions.SingleAsync(x => x.Id == s.SubscriptionId);
        subscription.Status.Should().Be("Cancelled", "no more dues are billed to someone erased");
        subscription.NextChargeAtUtc.Should().BeNull();
        (await db.ClubVisits.SingleAsync(v => v.MemberId == s.MemberId)).Should().Match<ClubVisit>(v => v.MemberName == "Erased member 1" && v.Notes == null && v.PointsAwarded == 50);

        // What was still to come is cancelled; what happened stays, anonymised.
        (await db.ClubReservations.SingleAsync(r => r.Id == s.FutureReservationId)).Status.Should().Be(ReservationStatuses.Cancelled);
        var past = await db.ClubReservations.SingleAsync(r => r.Id == s.PastReservationId);
        (past.Status, past.MemberName, past.MemberEmail).Should().Be((ReservationStatuses.Completed, "Erased member 1", (string?)null));
        var place = await db.ClubEventRegistrations.SingleAsync(r => r.Id == s.FutureRegistrationId);
        (place.Status, place.Name, place.Email).Should().Be((RegistrationStatuses.Cancelled, "Erased member 1", (string?)null));

        // Mail: the text to her goes, a queued email is cancelled, and mail to others loses the mention.
        var sent = await db.OutboundMessages.SingleAsync(m => m.Id == s.SentMessageId);
        sent.To.Should().Be("Erased member 1");
        sent.TextBody.Should().BeNull();
        sent.HtmlBody.Should().BeNull();
        sent.Subject.Should().Be("Welcome, Erased member 1");
        sent.Status.Should().Be(OutboundStatuses.Sent, "when it went and whether it arrived stay");
        (await db.OutboundMessages.SingleAsync(m => m.Id == s.QueuedMessageId)).Status.Should().Be(OutboundStatuses.Cancelled);
        (await db.OutboundMessages.SingleAsync(m => m.Id == s.BobMessageId)).Body.Should().Be("Thanks for coming with Erased member 1 (Erased member 1).");
        (await db.Activities.SingleAsync(a => a.Id == s.BobActivityId)).Body.Should().Be("Erased member 1 sent him to us.");
        (await db.InboundEmails.SingleAsync(i => i.OutboundMessageId == s.SentMessageId)).FromAddress.Should().Be("Erased member 1");

        // The suppression list holds a fingerprint of her address, not the address.
        var suppressions = await db.EmailSuppressions.Where(x => x.OrganizationId == s.Org).ToListAsync();
        suppressions.Should().ContainSingle();
        suppressions[0].Email.Should().Be(ErasedAddresses.Key(AnnaEmail));
        suppressions[0].Reason.Should().Be(EmailSuppressionReasons.Erased);

        // The audit trail keeps its lines about her records, under the pseudonym; the erasure's own
        // line names nobody.
        var lines = await db.AuditEvents.Where(e => e.OrganizationId == s.Org).ToListAsync();
        lines.Should().NotContain(e => (e.EntityLabel ?? "").Contains("Anna") || e.Summary.Contains("Anna") || e.Summary.Contains(AnnaEmail));
        lines.Should().Contain(e => e.EntityId == s.LeadId && e.EntityLabel == "Erased member 1");
        var erased = lines.Single(e => e.Action == "erased");
        erased.EntityLabel.Should().Be("Erased member 1");
        erased.Summary.Should().Contain("Letter of 12 November").And.NotContain("Keller");
        var log = await db.PersonErasures.SingleAsync(e => e.OrganizationId == s.Org);
        (log.Number, log.Trigger, log.Reference).Should().Be((1, PersonErasureTriggers.Request, "Letter of 12 November"));
        log.SummaryJson.Should().NotContain("Anna").And.NotContain(AnnaEmail);

        // Nobody else is touched.
        (await db.Leads.SingleAsync(l => l.Id == s.BobLeadId)).Name.Should().Be("Bob Brown");
        (await db.ClubMembers.SingleAsync(m => m.Id == s.BobMemberId)).Email.Should().Be("bob@example.ch");
        (await db.ClubMembers.SingleAsync(m => m.Id == s.OtherOrgMemberId)).Email.Should().Be(AnnaEmail);
    }

    [Fact]
    public async Task AnErasedAddressIsNotEmailedAgain_AndTheNextErasureTakesTheNextNumber()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await Ok(await client.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { s.MemberId }, leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, applicationIds = new[] { s.ApplicationId }, confirm = "ERASE" })), "erase Anna");

        using (var scope = factory.Services.CreateScope())
        {
            using var acting = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(s.Org, s.Owner);
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            var refused = await outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = s.Org, Source = OutboundSources.Composer, To = ["Anna.Keller@example.ch"], Subject = "Come back", Body = "We miss you.",
            });
            refused.IsSuccess.Should().BeFalse("her address is on the suppression list as a fingerprint");
            refused.Error.Code.Should().Be("Email.Suppressed");
            refused.Error.Description.Should().Contain("Anna.Keller@example.ch").And.Contain("erased at their request");
        }

        var second = await Ok(await client.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { s.BobMemberId }, confirm = "ERASE" })), "erase Bob");
        second.GetProperty("pseudonym").GetString().Should().Be("Erased member 2");
        var log = await Ok(await client.GetAsync("/api/privacy/erasures"), "the log");
        log.EnumerateArray().Select(e => e.GetProperty("number").GetInt32()).Should().Equal(2, 1);
    }

    [Fact]
    public async Task AnErasedPersonIsNotBroughtBackByAnImport_OrAddedBackAsAMember_WithoutAnyoneNoticing()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        await Ok(await client.PostAsync("/api/privacy/erasure", Body(new
        {
            leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId }, confirm = "ERASE",
        })), "erase Anna");

        var csv = "Name,Email\nAnna Keller,ANNA.KELLER@example.ch\nCarla Rossi,carla@example.ch\n";
        string?[] columns = ["name", "email"];
        var preview = await ImportFiles.OkJson(await ImportFiles.Preview(client, "lead", csv, columns), "preview the import");
        ImportFiles.Action(preview, 2).Should().Be("skip", "the dry run says what the import would do");
        ImportFiles.Message(preview, 2).Should().Contain("erased at their request");
        var imported = await ImportFiles.OkJson(await ImportFiles.Import(client, "lead", csv, columns), "import");
        ImportFiles.Action(imported, 2).Should().Be("skip");
        ImportFiles.Action(imported, 3).Should().Be("create", "everyone else is imported as before");
        (await factory.Services.Read(db => db.Leads.Count(l => l.OrganizationId == s.Org && l.Email.ToLower() == AnnaEmail))).Should().Be(0);

        var member = await client.PostAsync("/api/members", Body(new { firstName = "Anna", lastName = "Keller", email = AnnaEmail, tier = "Gold", status = "Active" }));
        member.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await member.Content.ReadAsStringAsync()).Should().Contain("lift the address from the suppression list");
    }

    [Fact]
    public async Task AReferenceThatNamesThePersonIsRefused()
    {
        var (factory, client, s) = await ClubAsync();
        using var _ = factory;
        var res = await client.PostAsync("/api/privacy/erasure", Body(new { leadIds = new[] { s.LeadId }, confirm = "ERASE", reference = "Request from Anna Keller" }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Privacy.ReferenceHasPersonalData");
        (await factory.Services.Read(db => db.Leads.Any(l => l.Id == s.LeadId))).Should().BeTrue();
    }
}
