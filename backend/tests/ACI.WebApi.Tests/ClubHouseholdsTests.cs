using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// One household model: who belongs together, one primary each, roles and roll-up billing that
/// change in place, bills that really go to the primary, the household's statement, and the
/// couples a contact's page records, which are households too.
/// </summary>
/// <remarks>
/// Roll-up billing was a label nothing read, so a spouse's spa bill stayed on the spouse's
/// account while the club invoiced the primary. Each test here posts or reads money and checks
/// whose account it landed on.
/// </remarks>
public class ClubHouseholdsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string last = "Meier") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = last, email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>The Meier household: Peter its primary, Anna his spouse with her bills rolling up, Kit their child billed on his own.</summary>
    private static async Task<(Guid Household, Guid Peter, Guid Anna, Guid Kit, JsonElement State)> MeiersAsync(HttpClient client)
    {
        var peter = await MemberAsync(client, "Peter");
        var anna = await MemberAsync(client, "Anna");
        var kit = await MemberAsync(client, "Kit");
        var household = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier", primaryMemberId = peter })), "household")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/households/{household}/members", Body(new { memberId = anna, role = "Spouse", rollUpBilling = true })), "add Anna");
        var state = await Json(await client.PostAsync($"/api/households/{household}/members", Body(new { memberId = kit, role = "Child" })), "add Kit");
        return (household, peter, anna, kit, state);
    }

    private static JsonElement Person(JsonElement household, Guid memberId) =>
        household.GetProperty("people").EnumerateArray().Single(p => p.TryGetProperty("memberId", out var m) && m.GetGuid() == memberId);

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync($"/api/members/{memberId}/balance"), "balance")).GetProperty("balance").GetDecimal();

    // ── the household ────────────────────────────────────────────────────────

    [Fact]
    public async Task RolesAndRollUpChangeInPlace_ThePrimaryCanChange_AndAHouseholdHasOne()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (household, peter, anna, kit, state) = await MeiersAsync(client);
        state.GetProperty("people").GetArrayLength().Should().Be(3);
        Person(state, peter).GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        var kitLink = Person(state, kit).GetProperty("linkId").GetGuid();
        var annaLink = Person(state, anna).GetProperty("linkId").GetGuid();

        var changed = await Json(await client.PutAsync($"/api/households/{household}/members/{kitLink}", Body(new { role = "Dependent", rollUpBilling = true })), "change Kit");
        (Person(changed, kit).GetProperty("role").GetString(), Person(changed, kit).GetProperty("rollUpBilling").GetBoolean()).Should().Be(("Dependent", true));
        // A second spouse is refused: one couple per household.
        await Refused(await client.PutAsync($"/api/households/{household}/members/{kitLink}", Body(new { role = "Partner" })), HttpStatusCode.Conflict, "a second partner");

        // Anna becomes the primary: Peter steps down as her spouse, and her bills stop rolling up.
        var moved = await Json(await client.PostAsync($"/api/households/{household}/primary", Body(new { linkId = annaLink })), "Anna becomes primary");
        var primaries = moved.GetProperty("people").EnumerateArray().Where(p => p.GetProperty("isPrimary").GetBoolean()).ToList();
        primaries.Should().ContainSingle().Which.GetProperty("memberId").GetGuid().Should().Be(anna);
        (Person(moved, peter).GetProperty("role").GetString(), Person(moved, anna).GetProperty("rollUpBilling").GetBoolean()).Should().Be(("Spouse", false));
        moved.GetProperty("primaryMemberId").GetGuid().Should().Be(anna);

        // The primary cannot leave, and a person is in one household.
        await Refused(await client.DeleteAsync($"/api/households/{household}/members/{annaLink}"), HttpStatusCode.BadRequest, "the primary leaves");
        var other = (await Json(await client.PostAsync("/api/households", Body(new { name = "Other", primaryMemberId = await MemberAsync(client, "Zoe", "Other") })), "another"))
            .GetProperty("id").GetGuid();
        (await Refused(await client.PostAsync($"/api/households/{other}/members", Body(new { memberId = kit, role = "Child" })), HttpStatusCode.Conflict, "Kit in two"))
            .Should().Contain("already in the Meier household");
        // Nor can the primary of a household with others in it be deleted.
        await Refused(await client.DeleteAsync($"/api/members/{anna}"), HttpStatusCode.Conflict, "delete the primary");
    }

    [Fact]
    public async Task AStaleEditIsRefused_AndSwitchingRollUpNeedsMoneyManage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        var (household, _, anna, kit, state) = await MeiersAsync(owner);
        var version = state.GetProperty("version").GetGuid();
        var kitLink = Person(state, kit).GetProperty("linkId").GetGuid();
        var annaLink = Person(state, anna).GetProperty("linkId").GetGuid();

        await Json(await director.PutAsync($"/api/households/{household}/members/{kitLink}", Body(new { role = "Dependent" })), "the director changes a role");
        await Refused(await owner.PutAsync($"/api/households/{household}/members/{kitLink}", Body(new { role = "Child", version })), HttpStatusCode.Conflict,
            "a change from the version before the director's");
        await Refused(await director.PutAsync($"/api/households/{household}/members/{annaLink}", Body(new { rollUpBilling = false })), HttpStatusCode.Forbidden,
            "the director switches roll-up");
    }

    [Fact]
    public async Task WhoseAccountBillsGoOn_IsChangedOnlyBySomeoneWhoHandlesMoney_HoweverTheHouseholdIsEdited()
    {
        // Switching a member's roll-up needs money.manage. Taking them out of the household, or
        // making them (or anyone else) the primary while bills roll up, changes the same thing:
        // whose account their charges go on. Without the check, a reviewer who may not switch
        // roll-up off did it by taking the spouse out, or by making her the primary.
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        var (household, peter, anna, kit, state) = await MeiersAsync(owner);
        var annaLink = Person(state, anna).GetProperty("linkId").GetGuid();
        var kitLink = Person(state, kit).GetProperty("linkId").GetGuid();

        await Refused(await director.DeleteAsync($"/api/households/{household}/members/{annaLink}"), HttpStatusCode.Forbidden, "the director takes Anna out");
        await Refused(await director.PostAsync($"/api/households/{household}/primary", Body(new { linkId = kitLink })), HttpStatusCode.Forbidden,
            "the director makes Kit the primary, so Anna's bills would go to him");
        (await factory.Services.Read(db => db.ClubHouseholdMembers.Single(l => l.MemberId == anna).RollUpBilling)).Should().BeTrue();
        (await factory.Services.Read(db => db.ClubHouseholdMembers.Single(l => l.IsPrimary).MemberId)).Should().Be(peter);

        // Kit's bills are his own: the director may take him out, and once nobody's bills roll
        // up, the primary is the household's to change.
        await Json(await director.DeleteAsync($"/api/households/{household}/members/{kitLink}"), "the director takes Kit out");
        await Json(await owner.PutAsync($"/api/households/{household}/members/{annaLink}", Body(new { rollUpBilling = false })), "the owner switches Anna's roll-up off");
        await Json(await director.PostAsync($"/api/households/{household}/primary", Body(new { linkId = annaLink })), "the director makes Anna the primary");
    }

    [Fact]
    public async Task MakingSomeoneWhoIsNotAMemberThePrimary_StopsTheRollUp_RatherThanSayingBillsGoWhereTheyDoNot()
    {
        // Roll-up bills the primary only when the primary is a member: with a contact as primary
        // the ledger bills each member on their own. Left switched on, the page went on saying
        // Anna's bills went to someone who never saw them.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (household, _, anna, _, _) = await MeiersAsync(client);
        var carla = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Carla Meier", email = "carla@example.ch" })), "Carla"))
            .GetProperty("id").GetGuid();
        var withCarla = await Json(await client.PostAsync($"/api/households/{household}/members", Body(new { contactId = carla, role = "Sibling" })), "Carla joins");
        var carlaLink = withCarla.GetProperty("people").EnumerateArray().Single(p => p.TryGetProperty("contactId", out var c) && c.GetGuid() == carla)
            .GetProperty("linkId").GetGuid();

        var moved = await Json(await client.PostAsync($"/api/households/{household}/primary", Body(new { linkId = carlaLink })), "Carla becomes the primary");
        moved.GetProperty("people").EnumerateArray().Count(p => p.GetProperty("rollUpBilling").GetBoolean()).Should().Be(0,
            "nobody's bills can go to a primary who is not a member");
        var spa = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Massage", amount = 90 })), "Anna's spa");
        spa.GetProperty("memberId").GetGuid().Should().Be(anna);
    }

    [Fact]
    public async Task AMemberMergeThatLeavesAContactHeadingTheHousehold_StopsTheRollUpToo()
    {
        // Peter heads the Meier household; Carla (a contact, not a member) joined it first, then
        // Anna, whose bills roll up. Peter is merged into Zoe, who heads another household, so
        // the Meier household passes to the first to have joined it: Carla, with no account.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var peter = await MemberAsync(client, "Peter");
        var anna = await MemberAsync(client, "Anna");
        var zoe = await MemberAsync(client, "Zoe", "Other");
        var meier = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier", primaryMemberId = peter })), "Meier")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync("/api/households", Body(new { name = "Other", primaryMemberId = zoe })), "Other");
        var carla = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Carla Meier", email = "carla@example.ch" })), "Carla")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/households/{meier}/members", Body(new { contactId = carla, role = "Sibling" })), "Carla joins");
        await Json(await client.PostAsync($"/api/households/{meier}/members", Body(new { memberId = anna, role = "Spouse", rollUpBilling = true })), "Anna joins");

        (await client.PostAsync($"/api/members/{zoe}/merge", Body(new { otherMemberId = peter }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await Json(await client.GetAsync($"/api/households/{meier}"), "the Meier household after");
        after.GetProperty("primaryName").GetString().Should().Be("Carla Meier");
        Person(after, anna).GetProperty("rollUpBilling").GetBoolean().Should().BeFalse("nobody's bills can go to a primary who is not a member");
    }

    [Fact]
    public async Task DuplicateHouseholdsMergeOnTheServer_EachPersonOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (household, _, _, _, _) = await MeiersAsync(client);
        var lena = await MemberAsync(client, "Lena", "Meier");
        var duplicate = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier family", primaryMemberId = lena, notes = "Gate code 1234" })), "duplicate"))
            .GetProperty("id").GetGuid();

        var merged = await Json(await client.PostAsync($"/api/households/{household}/merge", Body(new { duplicateIds = new[] { duplicate } })), "merge");
        merged.GetProperty("people").GetArrayLength().Should().Be(4);
        Person(merged, lena).GetProperty("role").GetString().Should().Be("Other", "the duplicate's primary joins as Other");
        merged.GetProperty("notes").GetString().Should().Contain("Gate code 1234");
        (await Json(await client.GetAsync("/api/households"), "list")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task AnotherWorkspacesHouseholdIsInvisible_AndCannotBeChanged()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var (theirs, _, _) = await ClubAsync(factory, "other@club.ch");
        var (household, _, _, kit, state) = await MeiersAsync(theirs);
        var kitLink = Person(state, kit).GetProperty("linkId").GetGuid();
        var myMember = await MemberAsync(mine, "Jean", "Dupont");

        await Refused(await mine.GetAsync($"/api/households/{household}"), HttpStatusCode.NotFound, "read theirs");
        await Refused(await mine.PutAsync($"/api/households/{household}/members/{kitLink}", Body(new { role = "Other" })), HttpStatusCode.NotFound, "change theirs");
        await Refused(await mine.PostAsync($"/api/households/{household}/members", Body(new { memberId = myMember, role = "Other" })), HttpStatusCode.NotFound, "join theirs");
        await Refused(await mine.PostAsync("/api/households", Body(new { name = "Stolen", primaryMemberId = kit })), HttpStatusCode.NotFound, "head one with their member");
        (await Json(await mine.GetAsync("/api/households"), "my list")).GetArrayLength().Should().Be(0);
    }

    // ── roll-up billing ──────────────────────────────────────────────────────

    [Fact]
    public async Task AChargeForAMemberWhoseBillsRollUp_GoesOnThePrimarysAccount_SayingWhoRanItUp()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (_, peter, anna, kit, _) = await MeiersAsync(client);

        var spa = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Massage", amount = 180 })), "Anna's spa");
        spa.GetProperty("memberId").GetGuid().Should().Be(peter, "her bills roll up to Peter");
        (spa.GetProperty("incurredByMemberId").GetGuid(), spa.GetProperty("incurredByName").GetString()).Should().Be((anna, "Anna Meier"));
        var lunch = await Json(await client.PostAsync("/api/charges", Body(new { memberId = kit, kind = "Dining", description = "Lunch", amount = 40 })), "Kit's lunch");
        lunch.GetProperty("memberId").GetGuid().Should().Be(kit, "Kit is billed on his own");
        lunch.TryGetProperty("incurredByMemberId", out _).Should().BeFalse();

        (await BalanceAsync(client, peter)).Should().Be(-180m);
        (await BalanceAsync(client, anna)).Should().Be(0m);
        (await BalanceAsync(client, kit)).Should().Be(-40m);

        // Peter pays it: the payment pays the charge on his account.
        var chargeId = spa.GetProperty("id").GetGuid();
        await Json(await client.PostAsync("/api/payments", Body(new { chargeId, amount = 180, method = "Cash" })), "Peter pays");
        (await BalanceAsync(client, peter)).Should().Be(0m);

        // The statement's line says whose it was.
        var from = DateTime.UtcNow.Date.AddDays(-1).ToString("o");
        var to = DateTime.UtcNow.Date.AddDays(2).ToString("o");
        var statement = await Json(await client.GetAsync($"/api/members/{peter}/statement?from={from}&to={to}"), "Peter's statement");
        statement.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("side").GetString() == "charge")
            .GetProperty("incurredByName").GetString().Should().Be("Anna Meier");
    }

    [Fact]
    public async Task ATabAndTheDuesOfAMemberWhoseBillsRollUp_AreBilledToThePrimary_AndTheirPointsStayTheirs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (_, peter, anna, _, _) = await MeiersAsync(client);

        var tab = await Json(await client.PostAsync("/api/tabs", Body(new { memberId = anna, venue = "Bar" })), "open Anna's tab");
        var tabId = tab.GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/tabs/{tabId}/items", Body(new { name = "Negroni", quantity = 2, unitPrice = 18 })), "a round");
        var closed = await Json(await client.PostAsync($"/api/tabs/{tabId}/close", null), "close the tab");
        var charge = await Read(factory, closed.GetProperty("chargeId").GetGuid());
        (charge.MemberId, charge.IncurredByMemberId, charge.Amount).Should().Be((peter, anna, 36m));
        (await factory.Services.Read(db => db.LoyaltyLedger.Single().MemberId)).Should().Be(anna, "the points are hers: she spent it");

        var sub = await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId = anna, name = "Locker", amount = 25, cadence = "Monthly" })), "Anna's locker");
        await Json(await client.PostAsync($"/api/subscriptions/{sub.GetProperty("id").GetGuid()}/bill", Body(new { })), "bill it");
        var dues = await factory.Services.Read(db => db.ClubCharges.Single(c => c.SubscriptionId == sub.GetProperty("id").GetGuid()));
        (dues.MemberId, dues.IncurredByMemberId).Should().Be((peter, anna));
        (await factory.Services.Read(db => db.ClubSubscriptions.Single(s => s.Id == sub.GetProperty("id").GetGuid()).MemberId)).Should().Be(anna,
            "the subscription stays hers; only the bill goes to Peter");
    }

    [Fact]
    public async Task SwitchingRollUpOff_BillsTheMemberAgainFromThen_AndWhatWasPostedStaysWhereItWas()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (household, peter, anna, _, state) = await MeiersAsync(client);
        await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Before", amount = 100 })), "before");
        await Json(await client.PutAsync($"/api/households/{household}/members/{Person(state, anna).GetProperty("linkId").GetGuid()}", Body(new { rollUpBilling = false })), "switch off");
        await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "After", amount = 30 })), "after");

        (await BalanceAsync(client, peter)).Should().Be(-100m, "what was posted stays on his account");
        (await BalanceAsync(client, anna)).Should().Be(-30m);
    }

    [Fact]
    public async Task ABillLeftUnpaidLapsesAndRemindsTheAccountThatOwesIt_ThePrimary_NotTheSpouseWhoRanItUp()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var (_, peter, anna, kit, _) = await MeiersAsync(client);
        await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId = anna, kind = "Spa", description = "Massage", amount = 180, dueAtUtc = DateTime.UtcNow.AddDays(-45).ToString("o"),
        })), "Anna's spa, long overdue");
        await Json(await client.PutAsync("/api/hospitality/settings", Body(new
        {
            billing = new { remindersEnabled = true, reminderDays = new[] { 3 }, lapseEnabled = true, gracePeriodDays = 30 },
        })), "turn on reminders and lapsing");

        using (var scope = factory.Services.CreateScope())
        {
            // The daily run, as the background job does it, after 03:00 in the club's time zone.
            var zone = ACI.Application.Common.RegionalSettings.ZoneOf(ACI.Application.Common.RegionalSettings.DefaultTimeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            var at = local.TimeOfDay >= TimeSpan.FromHours(3) ? DateTime.UtcNow : DateTime.UtcNow.Add(TimeSpan.FromHours(3) - local.TimeOfDay + TimeSpan.FromMinutes(1));
            await scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubBilling>().RunScheduledAsync(at);
        }

        var members = await Json(await client.GetAsync("/api/members"), "members");
        string StatusOf(Guid id) => members.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id).GetProperty("status").GetString()!;
        (StatusOf(peter), StatusOf(anna), StatusOf(kit)).Should().Be(("Lapsed", "Active", "Active"),
            "the bill is on Peter's account: he owes it, so he lapses, and the spouse who ran it up is not locked out for it");
        var reminders = await factory.Services.Read(db => db.OutboundMessages.Where(m => m.OrganizationId == org && m.Source == OutboundSources.PaymentReminder).Select(m => m.To).ToList());
        reminders.Should().Equal("peter@example.ch");
    }

    [Fact]
    public async Task TheHouseholdStatement_IsThePrimarysAccountAsAPdf()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var (household, _, anna, _, _) = await MeiersAsync(client);
        await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Massage", amount = 180 })), "Anna's spa");
        var from = DateTime.UtcNow.Date.AddDays(-1).ToString("o");
        var to = DateTime.UtcNow.Date.AddDays(2).ToString("o");

        var pdf = await client.GetAsync($"/api/households/{household}/statement.pdf?from={from}&to={to}");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        pdf.Content.Headers.ContentDisposition!.ToString().Should().Contain("household");
    }

    // ── the contact pages ────────────────────────────────────────────────────

    [Fact]
    public async Task ASpouseRecordedOnAContactsPage_IsTheCoupleOfAHousehold_SeenOnBothPagesAndTheClubs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var claire = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Claire Martin", email = "claire@example.ch" })), "Claire")).GetProperty("id").GetGuid();
        var marc = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Marc Martin", email = "marc@example.ch" })), "Marc")).GetProperty("id").GetGuid();

        var added = await Json(await client.PostAsync($"/api/contacts/{claire}/relationships", Body(new { relatedContactId = marc, type = "spouse" })), "record the spouse");
        added.GetProperty("type").GetString().Should().Be("spouse");
        added.GetProperty("householdName").GetString().Should().Be("Martin", "a couple is a household");
        (await factory.Services.Read(db => db.ContactRelationships.Count())).Should().Be(0, "recorded once: in the household, not as a row too");

        var households = await Json(await client.GetAsync("/api/households"), "the club sees it");
        var martin = households.EnumerateArray().Single();
        martin.GetProperty("people").EnumerateArray().Select(p => (p.GetProperty("name").GetString(), p.GetProperty("role").GetString()))
            .Should().BeEquivalentTo([("Claire Martin", "Primary"), ("Marc Martin", "Spouse")]);
        var fromMarc = await Json(await client.GetAsync($"/api/contacts/{marc}/relationships"), "Marc's page");
        fromMarc.GetProperty("items").EnumerateArray().Single().GetProperty("type").GetString().Should().Be("spouse");

        // A child recorded from Claire's page joins their household as her child; Marc sees his.
        var kid = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Lou Martin", email = "lou@example.ch" })), "Lou")).GetProperty("id").GetGuid();
        var spouseId = added.GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/households/{martin.GetProperty("id").GetGuid()}/members", Body(new { contactId = kid, role = "Child" })), "Lou joins");
        (await Json(await client.GetAsync($"/api/contacts/{marc}/relationships"), "Marc again")).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("relatedContactId").GetGuid() == kid).GetProperty("type").GetString().Should().Be("child");

        // Ended from Marc's page: he leaves Claire's household.
        (await client.DeleteAsync($"/api/contacts/{marc}/relationships/{spouseId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Json(await client.GetAsync($"/api/contacts/{claire}/relationships"), "Claire after")).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("relatedContactName").GetString()).Should().Equal("Lou Martin");
    }

    [Fact]
    public async Task TheLeadsListShowsASpouseKeptInAHousehold()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var claire = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Claire Martin", email = "claire@example.ch", status = "New" })), "Claire")).GetProperty("id").GetGuid();
        var marc = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Marc Martin", email = "marc@example.ch" })), "Marc")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/leads/{claire}/relationships", Body(new { relatedContactId = marc, type = "partner" })), "record the partner");

        var leads = await Json(await client.GetAsync("/api/leads?pageSize=50"), "the leads list");
        var items = leads.ValueKind == JsonValueKind.Array ? leads : leads.GetProperty("items");
        var row = items.EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == claire);
        row.GetProperty("relationships").EnumerateArray().Single().GetProperty("relatedContactName").GetString().Should().Be("Marc Martin");
    }

    private static Task<ClubCharge> Read(CustomWebApplicationFactory factory, Guid chargeId) =>
        factory.Services.Read(db => db.ClubCharges.Single(c => c.Id == chargeId));
}
