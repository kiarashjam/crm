using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.PortalWorld;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What a signed-in member sees and does in the portal: their own profile, account, statements,
/// events, bookings, consent and points, by the club's rules, and nothing of anyone else's.
/// </summary>
public class MemberPortalDataTests
{
    private static DateTime Days(double days, double hourUtc = 18) => DateTime.UtcNow.Date.AddDays(days).AddHours(hourUtc);

    private static async Task<Guid> EventAsync(HttpClient staff, DateTime start, decimal memberPrice = 0, int capacity = 20, string status = "Published") =>
        (await Ok(await staff.PostAsync("/api/events", Body(new
        {
            name = "Wine night", location = "Library Bar", startAtUtc = start, endAtUtc = start.AddHours(3), capacity, memberPrice, status,
        })), "add an event")).GetProperty("id").GetGuid();

    private static async Task<Guid> ResourceAsync(HttpClient staff, string name, string memberBooking, string kind = "Court", int capacity = 4, decimal deposit = 0) =>
        (await Ok(await staff.PostAsync("/api/resources", Body(new { name, kind, capacity, memberBooking, depositAmount = deposit })), $"add {name}"))
            .GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> BookAsync(HttpClient member, Guid resourceId, DateTime start, DateTime end, int partySize = 1) =>
        member.PostAsync("/api/portal/reservations", Body(new { resourceId, startAtUtc = start, endAtUtc = end, partySize }));

    [Fact]
    public async Task AMemberSeesTheirOwnProfile_AndChangesOnlyTheirPhoneAndLanguage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@profile.ch");
        var anna = await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var me = await Ok(await member.GetAsync("/api/portal/me"), "read their profile");
        me.GetProperty("memberId").GetGuid().Should().Be(anna);
        me.GetProperty("clubName").GetString().Should().Be("Pavillon46");
        me.GetProperty("active").GetBoolean().Should().BeTrue();

        var changed = await Ok(await member.PatchAsync("/api/portal/me", Body(new { phone = "+41 79 123 45 67", preferredLanguage = "fr" })), "change phone and language");
        changed.GetProperty("phone").GetString().Should().Be("+41 79 123 45 67");
        changed.GetProperty("preferredLanguage").GetString().Should().Be("fr");
        await Refused(await member.PatchAsync("/api/portal/me", Body(new { preferredLanguage = "it" })), HttpStatusCode.BadRequest, "a language the club does not write in");
        await Refused(await member.PatchAsync("/api/portal/me", Body(new { phone = "<script>" })), HttpStatusCode.BadRequest, "something that is no phone number");

        // The fields a member may not touch are not even read: the body binds to what they may change.
        await Ok(await member.PatchAsync("/api/portal/me", Body(new { phone = (string?)null, email = "evil@example.ch", tier = "Platinum", status = "Active" })), "clear the phone");
        var stored = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna));
        stored.Phone.Should().BeNull("a phone sent as null is taken off");
        stored.Email.Should().Be("anna@example.ch");
        stored.Tier.Should().Be("Gold");
        stored.PreferredLanguage.Should().Be("fr", "the club now writes to them in French");

        // Staff see who changed it: the member, in the portal.
        var audit = await factory.Services.Read(db => db.AuditEvents.Where(e => e.EntityId == anna).OrderByDescending(e => e.CreatedAtUtc).First());
        audit.ActorName.Should().Be("Anna Muster (member portal)");
        audit.ActorUserId.Should().BeNull();
    }

    [Fact]
    public async Task TheAccount_AndStatements_AreTheMembersOwn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@account.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Dining", description = "Dinner for two", amount = 120 })), "charge Anna");
        await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Ben's whisky", amount = 45 })), "charge Ben");
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var account = await Ok(await member.GetAsync("/api/portal/account"), "read the account");
        account.GetProperty("owed").GetDecimal().Should().Be(120);
        var open = account.GetProperty("openCharges").EnumerateArray().ToList();
        open.Should().ContainSingle().Which.GetProperty("description").GetString().Should().Be("Dinner for two");

        var month = account.GetProperty("currentMonth").GetString()!;
        var statement = await Ok(await member.GetAsync($"/api/portal/statements/{month}"), "this month's statement");
        statement.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("description").GetString())
            .Should().Contain("Dinner for two").And.NotContain("Ben's whisky");
        statement.GetProperty("closingOwed").GetDecimal().Should().Be(120);
        statement.GetRawText().Should().NotContain("createdByName", "no staff names reach the member");

        var pdf = await member.GetAsync($"/api/portal/statements/{month}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        await Refused(await member.GetAsync("/api/portal/statements/2026-13"), HttpStatusCode.BadRequest, "a month that is no month");
        (await Refused(await member.GetAsync($"/api/portal/statements/{month}/household-pdf"), HttpStatusCode.Forbidden, "a household statement without a household"))
            .Should().Contain("Portal.NotAllowed");
    }

    [Fact]
    public async Task PayingAndSavingACard_WaitForTheClubToTakePaymentsOnline_AndNeverReachAnotherMembersCharge()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@paying.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        var bens = (await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Bar", amount = 45 })), "charge Ben"))
            .GetProperty("id").GetGuid();
        await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Dining", description = "Dinner", amount = 80 })), "charge Anna");
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var card = await Ok(await member.GetAsync("/api/portal/card"), "the saved card");
        card.GetProperty("paysOnline").GetBoolean().Should().BeFalse();
        (await Refused(await member.PostAsync("/api/portal/pay", Body(new { })), HttpStatusCode.BadRequest, "paying with no online payments"))
            .Should().Contain("Portal.PaymentsOff");
        await Refused(await member.PostAsync("/api/portal/card/setup", Body(new { autoPay = true })), HttpStatusCode.BadRequest, "saving a card with no online payments");

        // Ben's charge is not Anna's to pay, whatever the club's payment settings.
        await Refused(await member.PostAsync("/api/portal/pay", Body(new { chargeIds = new[] { bens } })), HttpStatusCode.BadRequest, "Ben's charge (payments off)");
    }

    [Fact]
    public async Task EventsAreSignedUpForAndCancelled_ByTheClubsRules()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@events.ch");
        var anna = await MemberAsync(staff, "Anna");
        await MemberAsync(staff, "Lena", status: "Lapsed");
        var soon = await EventAsync(staff, DateTime.UtcNow.AddHours(10), memberPrice: 60);
        var later = await EventAsync(staff, Days(10), memberPrice: 60);
        var draft = await EventAsync(staff, Days(12), status: "Draft");
        var key = await OpenPortalAsync(staff, cancelHours: 24);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var events = (await Ok(await member.GetAsync("/api/portal/events"), "the events")).EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        events.Should().Contain([soon, later]).And.NotContain(draft, "a draft is not offered");
        // Each refusal by the club's rules has a code of its own, so the portal can say it in the member's language.
        (await Refused(await member.PostAsync($"/api/portal/events/{draft}/register", null), HttpStatusCode.Forbidden, "signing up for a draft"))
            .Should().Contain("Portal.NotAllowed.EventNotOpen");

        var place = await Ok(await member.PostAsync($"/api/portal/events/{later}/register", null), "sign up");
        place.GetProperty("myStatus").GetString().Should().Be("Reserved");
        place.GetProperty("canCancel").GetBoolean().Should().BeTrue();
        await Refused(await member.PostAsync($"/api/portal/events/{later}/register", null), HttpStatusCode.Conflict, "signing up twice");
        var charges = await factory.Services.Read(db => db.ClubCharges.Where(c => c.MemberId == anna).Select(c => c.Amount).ToList());
        charges.Should().Contain(60, "the ticket is on their house account, by the event's rules");

        var cancelled = await Ok(await member.PostAsync($"/api/portal/registrations/{place.GetProperty("myRegistrationId").GetGuid()}/cancel", null), "cancel in time");
        cancelled.GetProperty("myStatus").GetString().Should().Be("Cancelled");

        var soonPlace = await Ok(await member.PostAsync($"/api/portal/events/{soon}/register", null), "sign up for tonight");
        soonPlace.GetProperty("canCancel").GetBoolean().Should().BeFalse("it starts within the club's 24 hours");
        (await Refused(await member.PostAsync($"/api/portal/registrations/{soonPlace.GetProperty("myRegistrationId").GetGuid()}/cancel", null), HttpStatusCode.Forbidden, "cancelling too late"))
            .Should().Contain("24 hours").And.Contain("Portal.NotAllowed.TooLate");

        var lena = await SignInMemberAsync(factory, key, "lena@example.ch");
        (await Refused(await lena.PostAsync($"/api/portal/events/{later}/register", null), HttpStatusCode.Forbidden, "a lapsed member signing up"))
            .Should().Contain("not active").And.Contain("Portal.NotAllowed.NotActive");
    }

    [Fact]
    public async Task BookingsFollowTheResourcesRules_AndAClashNamesNobody()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@bookings.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        var boardroom = await ResourceAsync(staff, "Boardroom", "off", kind: "Meeting");
        var court = await ResourceAsync(staff, "Court", "instant");
        var spa = await ResourceAsync(staff, "Spa suite", "request", kind: "Spa", capacity: 2);
        var key = await OpenPortalAsync(staff, cancelHours: 24);
        var annaSession = await SignInMemberAsync(factory, key, "anna@example.ch");
        var benSession = await SignInMemberAsync(factory, key, "ben@example.ch");

        var offered = (await Ok(await annaSession.GetAsync("/api/portal/resources"), "what can be booked")).EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
        offered.Should().BeEquivalentTo([court, spa], "the boardroom is not offered in the portal");
        await Refused(await BookAsync(annaSession, boardroom, Days(3, 9), Days(3, 10)), HttpStatusCode.NotFound, "booking what is not offered");

        var courtBooking = await Ok(await BookAsync(annaSession, court, Days(3, 9), Days(3, 10), 2), "book the court");
        courtBooking.GetProperty("status").GetString().Should().Be("Confirmed", "the court is booked at once");
        var spaBooking = await Ok(await BookAsync(annaSession, spa, Days(4, 9), Days(4, 10)), "ask for the spa");
        spaBooking.GetProperty("status").GetString().Should().Be("Pending", "the spa waits for the club to confirm");

        var clash = await Refused(await BookAsync(benSession, court, Days(3, 9.5), Days(3, 10.5)), HttpStatusCode.Conflict, "Ben on Anna's time");
        clash.Should().NotContain("Anna").And.NotContain("Muster").And.Contain("already booked");

        await Refused(await BookAsync(annaSession, court, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)), HttpStatusCode.BadRequest, "a time in the past");
        await Refused(await BookAsync(annaSession, court, Days(200, 9), Days(200, 10)), HttpStatusCode.BadRequest, "more than 180 days ahead");

        var cancelled = await Ok(await annaSession.PostAsync($"/api/portal/reservations/{courtBooking.GetProperty("id").GetGuid()}/cancel", null), "cancel in time");
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
        var tonight = await Ok(await BookAsync(annaSession, court, DateTime.UtcNow.AddHours(5), DateTime.UtcNow.AddHours(6)), "book for tonight");
        tonight.GetProperty("canCancel").GetBoolean().Should().BeFalse();
        await Refused(await annaSession.PostAsync($"/api/portal/reservations/{tonight.GetProperty("id").GetGuid()}/cancel", null), HttpStatusCode.Forbidden, "cancelling too late");

        var mine = (await Ok(await annaSession.GetAsync("/api/portal/reservations"), "Anna's bookings")).EnumerateArray().ToList();
        mine.Should().OnlyContain(b => b.GetProperty("mine").GetBoolean());
        var bens = (await Ok(await benSession.GetAsync("/api/portal/reservations"), "Ben's bookings")).EnumerateArray().ToList();
        bens.Should().BeEmpty("Ben sees none of Anna's bookings");
        (await factory.Services.Read(db => db.ClubReservations.Count(r => r.MemberId == ben))).Should().Be(0);
    }

    [Fact]
    public async Task ConsentIsChangedByTheMember_AndRecordedAsTheirs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@consent.ch");
        var anna = await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var before = await Ok(await member.GetAsync("/api/portal/consent"), "read consent");
        before.GetProperty("current").EnumerateArray().Single(c => c.GetProperty("channel").GetString() == "email" && c.GetProperty("purpose").GetString() == "marketing")
            .GetProperty("state").GetString().Should().Be("unknown");

        var after = await Ok(await member.PutAsync("/api/portal/consent", Body(new { channel = "email", purpose = "marketing", granted = true })), "agree to news by email");
        after.GetProperty("current").EnumerateArray().Single(c => c.GetProperty("channel").GetString() == "email" && c.GetProperty("purpose").GetString() == "marketing")
            .GetProperty("state").GetString().Should().Be("granted");
        await Refused(await member.PutAsync("/api/portal/consent", Body(new { channel = "pigeon", purpose = "marketing", granted = true })), HttpStatusCode.BadRequest, "a channel there is none of");

        var record = await factory.Services.Read(db => db.ConsentRecords.Single(c => c.SubjectId == anna && c.IsCurrent && c.Channel == "email" && c.Purpose == "marketing"));
        record.Source.Should().Be(ConsentSources.Member);
        record.RecordedByUserId.Should().BeNull("nobody on the club's side recorded it");
        record.Address.Should().Be("anna@example.ch");
        // The club's member page shows it as the member's own answer.
        var staffView = await Ok(await staff.GetAsync($"/api/members/{anna}/consent"), "staff read Anna's consent");
        staffView.GetProperty("history").EnumerateArray().First().GetProperty("source").GetString().Should().Be("member");
    }

    [Fact]
    public async Task PointsAreTheMembersOwn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@points.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.LoyaltyLedger.AddRange(
                new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna Muster", Points = 300, Reason = "Visit", CreatedAtUtc = DateTime.UtcNow.AddDays(-2) },
                new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna Muster", Points = -100, Kind = "Redeemed", Reason = "Bar", CreatedAtUtc = DateTime.UtcNow },
                new LoyaltyLedgerEntry { Id = Guid.NewGuid(), OrganizationId = org, MemberId = ben, MemberName = "Ben Muster", Points = 900, Reason = "Visit", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        var points = await Ok(await member.GetAsync("/api/portal/points"), "read points");
        points.GetProperty("balance").GetInt32().Should().Be(200);
        points.GetProperty("history").EnumerateArray().Select(e => e.GetProperty("points").GetInt32()).Should().BeEquivalentTo([300, -100]);
    }

    [Fact]
    public async Task EveryIdOfAnotherMember_AnotherHousehold_OrAnotherClub_IsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@isolation.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        var court = await ResourceAsync(staff, "Court", "instant");
        var ev = await EventAsync(staff, Days(10));
        var key = await OpenPortalAsync(staff);
        var annaSession = await SignInMemberAsync(factory, key, "anna@example.ch");
        var benSession = await SignInMemberAsync(factory, key, "ben@example.ch");

        // Another club, with its own member, event, resource and charge.
        var (other, _, _) = await ClubAsync(factory, "owner@isolation-other.ch", "Other club");
        var carla = await MemberAsync(other, "Carla");
        var otherEvent = await EventAsync(other, Days(10));
        var otherCourt = await ResourceAsync(other, "Their court", "instant");
        var carlasCharge = (await Ok(await other.PostAsync("/api/charges", Body(new { memberId = carla, kind = "Bar", description = "Bar", amount = 30 })), "charge Carla"))
            .GetProperty("id").GetGuid();
        var otherKey = await OpenPortalAsync(other);
        var carlaSession = await SignInMemberAsync(factory, otherKey, "carla@example.ch");
        var carlasPlace = (await Ok(await carlaSession.PostAsync($"/api/portal/events/{otherEvent}/register", null), "Carla signs up"))
            .GetProperty("myRegistrationId").GetGuid();
        var carlasBooking = (await Ok(await BookAsync(carlaSession, otherCourt, Days(5, 9), Days(5, 10)), "Carla books")).GetProperty("id").GetGuid();

        // Ben's own place and booking at Anna's club.
        var bensPlace = (await Ok(await benSession.PostAsync($"/api/portal/events/{ev}/register", null), "Ben signs up")).GetProperty("myRegistrationId").GetGuid();
        var bensBooking = (await Ok(await BookAsync(benSession, court, Days(6, 9), Days(6, 10)), "Ben books")).GetProperty("id").GetGuid();
        var bensCharge = (await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Bar", amount = 20 })), "charge Ben"))
            .GetProperty("id").GetGuid();

        await Refused(await annaSession.PostAsync($"/api/portal/registrations/{bensPlace}/cancel", null), HttpStatusCode.NotFound, "Ben's place");
        await Refused(await annaSession.PostAsync($"/api/portal/reservations/{bensBooking}/cancel", null), HttpStatusCode.NotFound, "Ben's booking");
        await Refused(await annaSession.PostAsync($"/api/portal/registrations/{carlasPlace}/cancel", null), HttpStatusCode.NotFound, "another club's place");
        await Refused(await annaSession.PostAsync($"/api/portal/reservations/{carlasBooking}/cancel", null), HttpStatusCode.NotFound, "another club's booking");
        await Refused(await annaSession.PostAsync($"/api/portal/events/{otherEvent}/register", null), HttpStatusCode.NotFound, "another club's event");
        await Refused(await BookAsync(annaSession, otherCourt, Days(7, 9), Days(7, 10)), HttpStatusCode.NotFound, "another club's resource");
        // Paying a charge that is not theirs is refused before anything about payments is asked.
        foreach (var charge in new[] { bensCharge, carlasCharge })
            await Refused(await annaSession.PostAsync("/api/portal/pay", Body(new { chargeIds = new[] { charge } })), HttpStatusCode.BadRequest, "someone else's charge");

        // Nothing of theirs in Anna's lists either.
        (await Ok(await annaSession.GetAsync("/api/portal/reservations"), "Anna's bookings")).EnumerateArray().Should().BeEmpty();
        var events = (await Ok(await annaSession.GetAsync("/api/portal/events"), "Anna's events")).EnumerateArray().ToList();
        events.Select(e => e.GetProperty("id").GetGuid()).Should().Equal(ev);
        events.Single().GetProperty("myStatus").GetString().Should().Be("none", "Ben's place is not Anna's");
        (await Ok(await annaSession.GetAsync("/api/portal/account"), "Anna's account")).GetProperty("openCharges").EnumerateArray().Should().BeEmpty();

        // And the places, bookings and charges of others are as they were.
        (await factory.Services.Read(db => db.ClubEventRegistrations.Count(r => r.Status == RegistrationStatuses.Cancelled))).Should().Be(0);
        (await factory.Services.Read(db => db.ClubReservations.Count(r => r.Status == ReservationStatuses.Cancelled))).Should().Be(0);
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).Email)).Should().Be("anna@example.ch");
    }

    [Fact]
    public async Task AHouseholdsPrimarySeesTheHousehold_ButActsOnlyForThemselves_AndTheSpouseSeesNoMoreThanTheirOwn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@household.ch");
        var anna = await MemberAsync(staff, "Anna");
        var ben = await MemberAsync(staff, "Ben");
        var court = await ResourceAsync(staff, "Court", "instant");
        var household = await Ok(await staff.PostAsync("/api/households", Body(new { name = "Muster", primaryMemberId = anna })), "make the household");
        var householdId = household.GetProperty("id").GetGuid();
        await Ok(await staff.PostAsync($"/api/households/{householdId}/members", Body(new { memberId = ben, role = "Spouse", rollUpBilling = true })), "add Ben, his bills to Anna");
        await Ok(await staff.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Ben's whisky", amount = 45 })), "Ben runs up a bill");
        var key = await OpenPortalAsync(staff);
        var annaSession = await SignInMemberAsync(factory, key, "anna@example.ch");
        var benSession = await SignInMemberAsync(factory, key, "ben@example.ch");
        var bensBooking = (await Ok(await BookAsync(benSession, court, Days(6, 9), Days(6, 10)), "Ben books")).GetProperty("id").GetGuid();

        var annaMe = await Ok(await annaSession.GetAsync("/api/portal/me"), "Anna's profile");
        annaMe.GetProperty("household").GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        annaMe.GetProperty("household").GetProperty("people").EnumerateArray().Should().HaveCount(2);
        var annaAccount = await Ok(await annaSession.GetAsync("/api/portal/account"), "Anna's account");
        annaAccount.GetProperty("openCharges").EnumerateArray().Single().GetProperty("incurredByName").GetString().Should().Be("Ben Muster",
            "Ben's bill rolls up to Anna's account, saying whose it was");
        var month = annaAccount.GetProperty("currentMonth").GetString();
        (await annaSession.GetAsync($"/api/portal/statements/{month}/household-pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        var annasView = (await Ok(await annaSession.GetAsync("/api/portal/reservations"), "Anna's bookings")).EnumerateArray().Single();
        annasView.GetProperty("mine").GetBoolean().Should().BeFalse();
        annasView.GetProperty("forName").GetString().Should().Be("Ben Muster");
        annasView.GetProperty("canCancel").GetBoolean().Should().BeFalse();
        await Refused(await annaSession.PostAsync($"/api/portal/reservations/{bensBooking}/cancel", null), HttpStatusCode.NotFound, "Anna cancelling Ben's booking");

        var benMe = await Ok(await benSession.GetAsync("/api/portal/me"), "Ben's profile");
        benMe.GetProperty("household").GetProperty("isPrimary").GetBoolean().Should().BeFalse();
        benMe.GetProperty("household").GetProperty("people").EnumerateArray().Should().OnlyContain(p => p.GetProperty("balance").ValueKind == JsonValueKind.Null,
            "balances are the primary's to see");
        var benAccount = await Ok(await benSession.GetAsync("/api/portal/account"), "Ben's account");
        benAccount.GetProperty("billsGoToPrimary").GetBoolean().Should().BeTrue();
        benAccount.GetProperty("openCharges").EnumerateArray().Should().BeEmpty("his bills are on Anna's account");
        await Refused(await benSession.GetAsync($"/api/portal/statements/{month}/household-pdf"), HttpStatusCode.Forbidden, "Ben asking for the household's statement");
    }

    [Fact]
    public async Task AMemberAddedByAHandoff_IsInvitedLikeAnyOther_AndSeesWhenTheirMembershipStarts()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@handoff-member.ch");
        var camille = await MemberAsync(staff, "Camille", status: "Pending");
        using (var scope = factory.Services.CreateScope())
        {
            // What a welcome hand-over leaves on the member it adds: the deal, and a start still to be decided.
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var row = await db.ClubMembers.SingleAsync(m => m.Id == camille);
            row.DealId = Guid.NewGuid();
            row.StartUndetermined = true;
            row.StartsOnUtc = null;
            await db.SaveChangesAsync();
        }
        var key = await OpenPortalAsync(staff);

        var use = await Ok(await staff.PostAsync($"/api/members/{camille}/portal/invite", null), "invite Camille");
        use.GetProperty("fromHandoff").GetBoolean().Should().BeTrue();
        use.GetProperty("invitedAtUtc").ValueKind.Should().Be(JsonValueKind.String);
        var token = await LinkTokenAsync(factory, "camille@example.ch");
        var session = MemberClient(factory, (await Ok(await RedeemAsync(factory, key, token), "use the invitation")).GetProperty("token").GetString()!);
        var me = await Ok(await session.GetAsync("/api/portal/me"), "Camille's profile");
        me.GetProperty("startUndetermined").GetBoolean().Should().BeTrue();
        me.GetProperty("active").GetBoolean().Should().BeFalse("a pending membership books and signs up for nothing yet");
    }
}
