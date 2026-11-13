using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Private events: a company's or a private client's hire of the club's spaces, sold through a
/// deal. Its spaces are held in the bookings diary for its time, its menu and services come from
/// the catalogue, its deposit and final invoice go on the client's house account, its contract is
/// drafted from the private event template, and its guest list is checked in at the door.
/// </summary>
public class ClubPrivateEventsTests
{
    private static readonly DateTime Evening = DateTime.UtcNow.Date.AddDays(30).AddHours(17);

    private static string At(DateTime time) => time.ToString("O");

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

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch", string name = "Pavillon46")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, name);
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

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    private static Guid Version(JsonElement e) => e.GetProperty("version").GetGuid();

    private static async Task<Guid> CompanyAsync(HttpClient client, string name = "Acme AG") =>
        Id(await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}"));

    private static async Task<Guid> ContactAsync(HttpClient client, string name, string? email = null) =>
        Id(await Json(await client.PostAsync("/api/contacts", Body(new { name, email = email ?? $"{name.Split(' ')[0].ToLowerInvariant()}@example.ch" })), $"add {name}"));

    private static async Task<Guid> MemberAsync(HttpClient client, string first, Guid? contactId = null) =>
        Id(await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@keller.ch", tier = "Gold", status = "Active", contactId,
        })), $"add {first}"));

    private static async Task<Guid> ResourceAsync(HttpClient client, string name, int capacity) =>
        Id(await Json(await client.PostAsync("/api/resources", Body(new { name, kind = "Dining", capacity })), $"add {name}"));

    private static async Task<JsonElement> EventAsync(HttpClient client, object request) =>
        await Json(await client.PostAsync("/api/private-events", Body(request)), "make the private event");

    private static object Party(Guid company, params Guid[] spaces) => new
    {
        title = "Acme summer party", companyId = company, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(5)), guestCount = 20, resourceIds = spaces,
    };

    private static async Task<JsonElement> AccountAsync(HttpClient client, string kind, Guid id) =>
        await Json(await client.GetAsync($"/api/club-accounts/{kind}/{id}"), $"the {kind} account");

    // ── spaces ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheEventHoldsItsSpaces_AMemberCannotBookOverThem_NorTheEventOverABooking_AndCancellingFreesThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var dining = await ResourceAsync(client, "Private dining room", 24);
        var terrace = await ResourceAsync(client, "Terrace", 60);
        var anna = await MemberAsync(client, "Anna");
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = terrace, memberId = anna, partySize = 4, startAtUtc = At(Evening.AddHours(2)), endAtUtc = At(Evening.AddHours(3)),
        })), "Anna books the terrace");

        // Both rooms asked for, and the terrace is Anna's then: nothing is made.
        var clash = await Refused(await client.PostAsync("/api/private-events", Body(Party(acme, dining, terrace))), HttpStatusCode.Conflict, "the party over Anna's booking");
        clash.Should().Contain("Terrace");
        (await Json(await client.GetAsync("/api/private-events"), "the events")).GetArrayLength().Should().Be(0, "an event refused for its spaces is not left half made");

        var party = await EventAsync(client, Party(acme, dining));
        var space = party.GetProperty("spaces").EnumerateArray().Single();
        space.GetProperty("resourceName").GetString().Should().Be("Private dining room");
        space.GetProperty("status").GetString().Should().Be("Pending", "an enquiry pencils the room in");
        party.GetProperty("companyName").GetString().Should().Be("Acme AG");

        // Anna cannot book the dining room under the party, and the diary can neither change nor delete its hold.
        await Refused(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = dining, memberId = anna, partySize = 2, startAtUtc = At(Evening.AddHours(1)), endAtUtc = At(Evening.AddHours(2)),
        })), HttpStatusCode.Conflict, "Anna over the party");
        var hold = space.GetProperty("reservationId").GetGuid();
        var listed = (await Json(await client.GetAsync("/api/reservations"), "the diary")).EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == hold);
        listed.GetProperty("privateEventId").GetGuid().Should().Be(Id(party));
        (await Refused(await client.PutAsync($"/api/reservations/{hold}", Body(new { status = "Cancelled" })), HttpStatusCode.Conflict, "cancelling the hold in the diary"))
            .Should().Contain("private event");
        await Refused(await client.DeleteAsync($"/api/reservations/{hold}"), HttpStatusCode.Conflict, "deleting the hold");

        // An hour later: the hold moves with it, and the hour before is free.
        var moved = await Json(await client.PutAsync($"/api/private-events/{Id(party)}", Body(new
        {
            startAtUtc = At(Evening.AddHours(1)), endAtUtc = At(Evening.AddHours(6)), version = Version(party),
        })), "move the party");
        var heldNow = await factory.Services.Read(db => db.ClubReservations.Single(r => r.Id == hold));
        (heldNow.StartAtUtc, heldNow.EndAtUtc, heldNow.PartySize).Should().Be((Evening.AddHours(1), Evening.AddHours(6), 24), "a party has the room to itself");
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = dining, memberId = anna, partySize = 2, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(1)),
        })), "Anna, before the party now");

        // A screen still showing the old version is told so.
        await Refused(await client.PutAsync($"/api/private-events/{Id(party)}", Body(new { guestCount = 30, version = Version(party) })),
            HttpStatusCode.Conflict, "an edit from the old version");

        var cancelled = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Acme postponed" })), "cancel");
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
        cancelled.GetProperty("spaces").GetArrayLength().Should().Be(0);
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = dining, memberId = anna, partySize = 2, startAtUtc = At(Evening.AddHours(2)), endAtUtc = At(Evening.AddHours(3)),
        })), "Anna, where the party was");
        await Refused(await client.PutAsync($"/api/private-events/{Id(party)}", Body(new { guestCount = 30, version = Version(moved) })),
            HttpStatusCode.Conflict, "editing a cancelled event");
    }

    // ── money ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheMenuComesFromTheCatalogue_TheDepositGoesOnTheCompanysAccount_AndTheInvoiceSetsItAgainstTheLinesPerVatRate()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var dinner = Id(await Json(await client.PostAsync("/api/catalog", Body(new { kind = "MenuItem", name = "Chef's dinner", price = 120m, vatRate = 8.1m })), "add the dinner"));
        var party = await EventAsync(client, new
        {
            title = "Acme board dinner", companyId = acme, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(4)), guestCount = 30,
            lines = new object[]
            {
                new { catalogItemId = dinner, quantity = 30, unitPrice = 1m },
                new { name = "Room hire", unitPrice = 800m, vatRate = 8.1m },
            },
        });
        var lines = party.GetProperty("lines").EnumerateArray().ToList();
        lines[0].GetProperty("name").GetString().Should().Be("Chef's dinner");
        lines[0].GetProperty("unitPrice").GetDecimal().Should().Be(120m, "a catalogue line takes the catalogue's price");
        lines[0].GetProperty("total").GetDecimal().Should().Be(3600m);
        var flowers = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/lines", Body(new { name = "Flowers", unitPrice = 250m, vatRate = 2.6m })), "add the flowers");
        flowers.GetProperty("money").GetProperty("linesTotal").GetDecimal().Should().Be(4650m);
        flowers.GetProperty("money").GetProperty("billToKind").GetString().Should().Be("company");

        // Its quantity is the event's to change; a catalogue line's price is not.
        var changed = await Json(await client.PutAsync($"/api/private-events/{Id(party)}/lines/{Id(lines[0])}", Body(new { quantity = 32, unitPrice = 99m })), "32 dinners");
        changed.GetProperty("lines").EnumerateArray().First().GetProperty("unitPrice").GetDecimal().Should().Be(120m);
        changed.GetProperty("money").GetProperty("linesTotal").GetDecimal().Should().Be(4890m);

        var asked = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/deposit", Body(new { amount = 1000m })), "ask the deposit");
        asked.GetProperty("money").GetProperty("depositState").GetString().Should().Be("charged");
        var deposit = asked.GetProperty("money").GetProperty("depositChargeId").GetGuid();
        (await AccountAsync(client, "companies", acme)).GetProperty("owed").GetDecimal().Should().Be(1000m, "the deposit is on Acme's own account");
        await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/deposit", Body(new { amount = 500m })), HttpStatusCode.Conflict, "a second deposit");

        await Json(await client.PostAsync("/api/payments", Body(new { chargeId = deposit, amount = 1000m, method = "Wire" })), "Acme pays the deposit");
        (await Json(await client.GetAsync($"/api/private-events/{Id(party)}"), "the event")).GetProperty("money").GetProperty("depositState").GetString().Should().Be("paid");

        // One charge per VAT rate, and what the deposit paid pays them.
        var invoiced = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/invoice", null), "invoice");
        var money = invoiced.GetProperty("money");
        money.GetProperty("invoiced").GetBoolean().Should().BeTrue();
        money.GetProperty("depositState").GetString().Should().Be("set_against_invoice");
        money.GetProperty("invoiceTotal").GetDecimal().Should().Be(4890m);
        money.GetProperty("invoicePaid").GetDecimal().Should().Be(1000m);
        var invoiceCharges = money.GetProperty("invoiceChargeIds").EnumerateArray().Select(c => c.GetGuid()).ToList();
        var charges = (await Json(await client.GetAsync($"/api/charges?memberId={acme}"), "Acme's charges")).EnumerateArray().ToList();
        charges.Where(c => invoiceCharges.Contains(Id(c))).Select(c => (c.GetProperty("amount").GetDecimal(), c.GetProperty("vatRate").GetDecimal()))
            .Should().BeEquivalentTo([(4640m, 8.1m), (250m, 2.6m)]);
        var account = await AccountAsync(client, "companies", acme);
        account.GetProperty("owed").GetDecimal().Should().Be(3890m, "the deposit paid counts once, against the invoice");
        account.GetProperty("balance").GetDecimal().Should().Be(-3890m);

        // Invoiced: the menu and the invoice stay as they were posted.
        await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/invoice", null), HttpStatusCode.Conflict, "invoicing twice");
        await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/lines", Body(new { name = "Late extra", unitPrice = 10m })), HttpStatusCode.Conflict, "a line after the invoice");
        await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Too late", deposit = "release" })), HttpStatusCode.Conflict, "cancelling an invoiced event");
    }

    [Fact]
    public async Task APrivateClientPaysFromTheirMemberAccount_OrTheirOwnWhenNotAMember_AndTheClientIsFixedOnceMoneyIsOnIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var lena = await ContactAsync(client, "Lena Graf");
        var wedding = await EventAsync(client, new { title = "Graf wedding", contactId = lena, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(8)), guestCount = 80 });
        wedding.GetProperty("money").GetProperty("billToKind").GetString().Should().Be("contact");

        var asked = await Json(await client.PostAsync($"/api/private-events/{Id(wedding)}/deposit", Body(new { amount = 2000m })), "ask Lena's deposit");
        asked.GetProperty("money").GetProperty("billToAccountId").GetGuid().Should().Be(lena);
        var charge = (await Json(await client.GetAsync($"/api/charges?memberId={lena}"), "Lena's charges")).EnumerateArray().Single();
        charge.GetProperty("accountType").GetString().Should().Be("contact");
        charge.GetProperty("memberName").GetString().Should().Be("Lena Graf");
        (await AccountAsync(client, "contacts", lena)).GetProperty("owed").GetDecimal().Should().Be(2000m);

        // Who pays cannot change once money is on their account.
        var acme = await CompanyAsync(client);
        (await Refused(await client.PutAsync($"/api/private-events/{Id(wedding)}", Body(new { companyId = acme, version = Version(asked) })), HttpStatusCode.Conflict, "moving the wedding to Acme"))
            .Should().Contain("account");

        // A contact who is a member pays from their member account.
        var marc = await ContactAsync(client, "Marc Weber");
        var marcMember = await MemberAsync(client, "Marc", contactId: marc);
        var birthday = await EventAsync(client, new { title = "Marc's 50th", contactId = marc, startAtUtc = At(Evening.AddDays(1)), endAtUtc = At(Evening.AddDays(1).AddHours(4)), guestCount = 25 });
        birthday.GetProperty("money").GetProperty("billToKind").GetString().Should().Be("member");
        birthday.GetProperty("money").GetProperty("billToAccountId").GetGuid().Should().Be(marcMember);
        await Json(await client.PostAsync($"/api/private-events/{Id(birthday)}/deposit", Body(new { amount = 300m })), "ask Marc's deposit");
        (await Json(await client.GetAsync($"/api/charges?memberId={marcMember}"), "Marc's charges")).EnumerateArray()
            .Should().ContainSingle(c => c.GetProperty("amount").GetDecimal() == 300m && c.GetProperty("description").GetString()!.StartsWith("Deposit: Marc's 50th"));
    }

    [Fact]
    public async Task CancellingAsksWhatBecomesOfTheDeposit_KeptOrGivenBackAsCredit_AndMoneyTakesMoneyManage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        var acme = await CompanyAsync(owner);

        // The membership director arranges the event, but posting money is the finance side's.
        var kept = await EventAsync(director, Party(acme));
        await Refused(await director.PostAsync($"/api/private-events/{Id(kept)}/deposit", Body(new { amount = 500m })), HttpStatusCode.Forbidden, "a deposit without money.manage");
        var keptDeposit = (await Json(await owner.PostAsync($"/api/private-events/{Id(kept)}/deposit", Body(new { amount = 500m })), "ask the deposit"))
            .GetProperty("money").GetProperty("depositChargeId").GetGuid();
        await Json(await owner.PostAsync("/api/payments", Body(new { chargeId = keptDeposit, amount = 500m, method = "Wire" })), "Acme pays");

        await Refused(await director.PostAsync($"/api/private-events/{Id(kept)}/cancel", Body(new { reason = "Called off" })), HttpStatusCode.BadRequest, "cancelling without saying what becomes of the deposit");
        await Refused(await director.PostAsync($"/api/private-events/{Id(kept)}/cancel", Body(new { reason = "Called off", deposit = "release" })), HttpStatusCode.Forbidden, "giving it back without money.manage");
        var cancelled = await Json(await director.PostAsync($"/api/private-events/{Id(kept)}/cancel", Body(new { reason = "Called off late", deposit = "keep" })), "cancel, keeping the deposit");
        cancelled.GetProperty("money").GetProperty("depositState").GetString().Should().Be("kept");
        (await AccountAsync(owner, "companies", acme)).GetProperty("balance").GetDecimal().Should().Be(0m, "the deposit kept stays paid");

        // Given back: the deposit's charge is taken back, and what was paid on it is Acme's credit.
        var released = await EventAsync(owner, new { title = "Acme offsite", companyId = acme, startAtUtc = At(Evening.AddDays(2)), endAtUtc = At(Evening.AddDays(2).AddHours(3)) });
        var releasedDeposit = (await Json(await owner.PostAsync($"/api/private-events/{Id(released)}/deposit", Body(new { amount = 400m })), "ask the deposit"))
            .GetProperty("money").GetProperty("depositChargeId").GetGuid();
        await Json(await owner.PostAsync("/api/payments", Body(new { chargeId = releasedDeposit, amount = 400m, method = "Wire" })), "Acme pays");
        var givenBack = await Json(await owner.PostAsync($"/api/private-events/{Id(released)}/cancel", Body(new { reason = "Acme moved it", deposit = "release" })), "cancel, giving it back");
        givenBack.GetProperty("money").GetProperty("depositState").GetString().Should().Be("released");
        var account = await AccountAsync(owner, "companies", acme);
        account.GetProperty("credit").GetDecimal().Should().Be(400m);
        account.GetProperty("owed").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task APrivateClientWhoIsACompanysNominee_PaysForTheirOwnParty_TheCompanyOnlyForWhatTheyRunUpInTheClub()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var marc = await ContactAsync(client, "Marc Weber");
        var marcMember = await MemberAsync(client, "Marc", contactId: marc);
        // Acme pays what its nominees run up in the club, and Marc holds one of its seats.
        var corporate = Id(await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = acme, name = "Acme corporate", seats = 2, nomineeCharges = "company", startsAtUtc = DateTime.UtcNow.Date.ToString("O"),
        })), "make Acme's corporate membership"));
        await Json(await client.PostAsync($"/api/corporate-memberships/{corporate}/nominees", Body(new { memberId = marcMember })), "seat Marc");
        (await Json(await client.PostAsync("/api/charges", Body(new { memberId = marcMember, kind = "Bar", description = "Negroni", amount = 18m })), "Marc's drink"))
            .GetProperty("memberId").GetGuid().Should().Be(acme, "a drink at the bar is what the company agreed to pay");

        // Marc hires the club for his own birthday, as himself: the party is his to pay, not Acme's.
        var birthday = await EventAsync(client, new
        {
            title = "Marc's 50th", contactId = marc, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(4)), guestCount = 25,
            lines = new[] { new { name = "Dinner", quantity = 25m, unitPrice = 80m, vatRate = 8.1m } },
        });
        var asked = await Json(await client.PostAsync($"/api/private-events/{Id(birthday)}/deposit", Body(new { amount = 500m })), "ask Marc's deposit");
        asked.GetProperty("money").GetProperty("billToAccountId").GetGuid().Should().Be(marcMember);
        asked.GetProperty("money").GetProperty("billToKind").GetString().Should().Be("member");
        var invoiced = await Json(await client.PostAsync($"/api/private-events/{Id(birthday)}/invoice", null), "invoice Marc's party");
        var invoiceCharge = invoiced.GetProperty("money").GetProperty("invoiceChargeIds").EnumerateArray().Single().GetGuid();

        (await Json(await client.GetAsync($"/api/charges?memberId={marcMember}"), "Marc's charges")).EnumerateArray().Select(Id)
            .Should().Contain([asked.GetProperty("money").GetProperty("depositChargeId").GetGuid(), invoiceCharge]);
        (await AccountAsync(client, "companies", acme)).GetProperty("owed").GetDecimal().Should().Be(18m, "Acme owes Marc's drink and nothing of his party");
    }

    [Fact]
    public async Task AnInvoicedEvent_IsCancelledOnceItsInvoicesChargesAreVoided_AndItsSpacesAreFreed()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var salon = await ResourceAsync(client, "Salon", 30);
        var party = await EventAsync(client, new
        {
            title = "Acme dinner", companyId = acme, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(4)), guestCount = 30, resourceIds = new[] { salon },
            lines = new object[]
            {
                new { name = "Dinner", quantity = 30m, unitPrice = 90m, vatRate = 8.1m },
                new { name = "Flowers", quantity = 1m, unitPrice = 200m, vatRate = 2.6m },
            },
        });
        var invoiced = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/invoice", null), "invoice it in advance");
        invoiced.GetProperty("money").GetProperty("invoiceVoided").GetBoolean().Should().BeFalse();
        var charges = invoiced.GetProperty("money").GetProperty("invoiceChargeIds").EnumerateArray().Select(c => c.GetGuid()).ToList();
        charges.Should().HaveCount(2, "one charge per VAT rate");

        // Acme calls it off. While the invoice stands on its account the event stays, so the two agree.
        (await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Acme called it off" })),
            HttpStatusCode.Conflict, "cancelling with the invoice standing")).Should().Contain("Void its charges");
        await Json(await client.PostAsync($"/api/charges/{charges[0]}/void", Body(new { reason = "Acme called it off" })), "void the dinner");
        await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Acme called it off" })),
            HttpStatusCode.Conflict, "cancelling with part of the invoice standing");
        await Json(await client.PostAsync($"/api/charges/{charges[1]}/void", Body(new { reason = "Acme called it off" })), "void the flowers");
        (await Json(await client.GetAsync($"/api/private-events/{Id(party)}"), "the event")).GetProperty("money").GetProperty("invoiceVoided").GetBoolean()
            .Should().BeTrue();

        // It used to be refused for good, and the salon stayed held for a dinner nobody was coming to.
        var cancelled = await Json(await client.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Acme called it off" })), "cancel");
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
        cancelled.GetProperty("spaces").GetArrayLength().Should().Be(0);
        var anna = await MemberAsync(client, "Anna");
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = salon, memberId = anna, partySize = 2, startAtUtc = At(Evening.AddHours(1)), endAtUtc = At(Evening.AddHours(2)),
        })), "Anna books the salon that evening");
        (await AccountAsync(client, "companies", acme)).GetProperty("owed").GetDecimal().Should().Be(0m);
    }

    // ── contract ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheContractIsDraftedFromThePrivateEventTemplateOnItsDeal_AndCountersigningConfirmsTheEventWithoutAdmittingAnyone()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var sara = await ContactAsync(client, "Sara Frei", "sara@acme.ch");
        // Sara has applied to join too: a membership contract's countersignature would admit her.
        var application = Id(await Json(await client.PostAsync("/api/applications", Body(new
        {
            firstName = "Sara", lastName = "Frei", email = "sara@acme.ch", requestedTier = "Gold", contactId = sara,
        })), "Sara applies"));
        var deal = Id(await Json(await client.PostAsync("/api/deals", Body(new { name = "Acme launch", amount = 5000m, currency = "CHF", contactId = sara })), "add the deal"));
        var library = await ResourceAsync(client, "Library", 40);
        var launch = await EventAsync(client, new
        {
            title = "Acme product launch", companyId = acme, contactId = sara, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(3)), guestCount = 40,
            resourceIds = new[] { library }, lines = new[] { new { name = "Canapés", quantity = 40m, unitPrice = 18m, vatRate = 8.1m } },
        });

        (await Refused(await client.PostAsync($"/api/private-events/{Id(launch)}/contract", null), HttpStatusCode.BadRequest, "a contract with no deal"))
            .Should().Contain("deal");
        var linked = await Json(await client.PutAsync($"/api/private-events/{Id(launch)}", Body(new { dealId = deal, version = Version(launch) })), "link the deal");
        linked.GetProperty("dealName").GetString().Should().Be("Acme launch");
        await Json(await client.PostAsync($"/api/private-events/{Id(launch)}/deposit", Body(new { amount = 250m })), "ask the deposit");

        var drafted = await Json(await client.PostAsync($"/api/private-events/{Id(launch)}/contract", null), "draft the contract");
        drafted.GetProperty("contractStatus").GetString().Should().Be("draft");
        var contractId = drafted.GetProperty("contractId").GetGuid();
        var contract = await Json(await client.GetAsync($"/api/contracts/{contractId}"), "the contract");
        var text = contract.GetProperty("body").GetString()!;
        text.Should().Contain("PRIVATE EVENT AGREEMENT").And.Contain("Acme product launch").And.Contain("Acme AG")
            .And.Contain("Library").And.Contain("40 × Canapés").And.Contain("deposit of").And.NotContain("{{");
        await Refused(await client.PostAsync($"/api/private-events/{Id(launch)}/contract", null), HttpStatusCode.Conflict, "a second contract");

        var token = (await Json(await client.PostAsync($"/api/contracts/{contractId}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        using (var anon = factory.CreateClient())
            await Json(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Sara Frei", agreed = true })), "Sara signs");
        await Json(await client.PostAsync($"/api/contracts/{contractId}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");

        var confirmed = await Json(await client.GetAsync($"/api/private-events/{Id(launch)}"), "the event");
        confirmed.GetProperty("status").GetString().Should().Be("Confirmed");
        confirmed.GetProperty("spaces").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Confirmed", "the room is the party's for certain now");
        confirmed.GetProperty("contractStatus").GetString().Should().Be("countersigned");
        (await Json(await client.GetAsync($"/api/applications/{application}"), "Sara's application")).GetProperty("status").GetString()
            .Should().Be("Submitted", "hiring a room is not joining the club");
        (await factory.Services.Read(db => db.ClubMembers.Count())).Should().Be(0);
    }

    // ── guests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheDoorAddsWalkInsAndChecksGuestsInOnce_ButOnlyThoseWhoRunTheMembersArrangeTheEvent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        var acme = await CompanyAsync(owner);
        var anna = await MemberAsync(owner, "Anna");
        await Refused(await desk.PostAsync("/api/private-events", Body(Party(acme))), HttpStatusCode.Forbidden, "the front desk making an event");
        var party = await EventAsync(owner, Party(acme));

        var listed = await Json(await owner.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { memberId = anna })), "Anna on the list");
        listed.GetProperty("guests").EnumerateArray().Single().GetProperty("name").GetString().Should().Be("Anna Keller");
        await Refused(await owner.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { name = "Bad Address", email = "not-an-address" })), HttpStatusCode.BadRequest, "a guest with a bad address");

        var walkIn = await Json(await desk.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { name = "Tom Berger", email = "tom@example.ch" })), "the door adds a walk-in");
        var tom = Id(walkIn.GetProperty("guests").EnumerateArray().Single(g => g.GetProperty("name").GetString() == "Tom Berger"));
        var first = await Json(await desk.PostAsync($"/api/private-events/{Id(party)}/guests/{tom}/check-in", null), "check Tom in");
        first.GetProperty("guestsCheckedIn").GetInt32().Should().Be(1);
        var at = first.GetProperty("guests").EnumerateArray().Single(g => Id(g) == tom).GetProperty("checkedInAtUtc").GetDateTime();
        var again = await Json(await desk.PostAsync($"/api/private-events/{Id(party)}/guests/{tom}/check-in", null), "a second press");
        again.GetProperty("guests").EnumerateArray().Single(g => Id(g) == tom).GetProperty("checkedInAtUtc").GetDateTime().Should().Be(at, "the first time stays");
        again.GetProperty("version").GetGuid().Should().Be(Version(party), "checking guests in does not stale a screen editing the event");
        (await Json(await desk.PostAsync($"/api/private-events/{Id(party)}/guests/{tom}/undo-check-in", null), "undo")).GetProperty("guestsCheckedIn").GetInt32().Should().Be(0);

        await Refused(await desk.DeleteAsync($"/api/private-events/{Id(party)}/guests/{tom}"), HttpStatusCode.Forbidden, "the front desk taking a guest off");
        await Json(await owner.DeleteAsync($"/api/private-events/{Id(party)}/guests/{tom}"), "take Tom off");

        await Json(await owner.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Rain" })), "cancel");
        var annaGuest = Id(listed.GetProperty("guests").EnumerateArray().Single());
        await Refused(await desk.PostAsync($"/api/private-events/{Id(party)}/guests/{annaGuest}/check-in", null), HttpStatusCode.Conflict, "checking in at a cancelled event");
    }

    // ── workspaces ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AnotherWorkspaceCanNeitherSeeNorTouchTheEvent_NorHoldTheClubsSpacesOrBillItsCompany()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ours, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(ours);
        var room = await ResourceAsync(ours, "Library", 40);
        var party = await EventAsync(ours, Party(acme, room));
        var guest = Id((await Json(await ours.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { name = "Tom Berger" })), "a guest"))
            .GetProperty("guests").EnumerateArray().Single());

        var (theirs, _, _) = await ClubAsync(factory, "owner@other.ch", "Other club");
        await Refused(await theirs.GetAsync($"/api/private-events/{Id(party)}"), HttpStatusCode.NotFound, "reading another workspace's event");
        (await Json(await theirs.GetAsync("/api/private-events"), "their events")).GetArrayLength().Should().Be(0);
        await Refused(await theirs.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { name = "Sneaky" })), HttpStatusCode.NotFound, "adding a guest");
        await Refused(await theirs.PostAsync($"/api/private-events/{Id(party)}/guests/{guest}/check-in", null), HttpStatusCode.NotFound, "checking a guest in");
        await Refused(await theirs.PostAsync($"/api/private-events/{Id(party)}/deposit", Body(new { amount = 10m })), HttpStatusCode.NotFound, "asking a deposit");
        await Refused(await theirs.PostAsync($"/api/private-events/{Id(party)}/cancel", Body(new { reason = "Sneaky" })), HttpStatusCode.NotFound, "cancelling it");
        await Refused(await theirs.PostAsync("/api/private-events", Body(Party(acme))), HttpStatusCode.NotFound, "an event for another workspace's company");
        var bolt = await CompanyAsync(theirs, "Bolt GmbH");
        await Refused(await theirs.PostAsync("/api/private-events", Body(Party(bolt, room))), HttpStatusCode.NotFound, "holding another workspace's room");

        (await Json(await ours.GetAsync($"/api/private-events/{Id(party)}"), "ours, untouched")).GetProperty("status").GetString().Should().Be("Enquiry");
    }

    [Fact]
    public async Task WithTheSalesSideOff_NoContractIsDrafted_ForNobodyCouldSendOrSignIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var deal = Id(await Json(await client.PostAsync("/api/deals", Body(new { name = "Acme launch", amount = 5000m, currency = "CHF" })), "add the deal"));
        var party = await EventAsync(client, new { title = "Acme launch", companyId = acme, dealId = deal, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(3)) });
        await Json(await client.PutAsync($"/api/organizations/{org}/modules", Body(new { sales = false })), "turn sales off");

        (await Refused(await client.PostAsync($"/api/private-events/{Id(party)}/contract", null), HttpStatusCode.Forbidden, "a contract with sales off"))
            .Should().Contain("Module.Off");
        (await factory.Services.Read(db => db.Contracts.Count())).Should().Be(0);
    }

    // ── privacy ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task APersonsExportHasTheEventsTheyHiredAndTheListsTheyAreOn_AndErasingThemKeepsTheEventForTheBooksWithoutThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var library = await ResourceAsync(client, "Library", 40);
        var lena = await ContactAsync(client, "Lena Graf", "lena@graf.ch");
        var wedding = await EventAsync(client, new
        {
            title = "Lena Graf's wedding", contactId = lena, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(8)), guestCount = 80,
            resourceIds = new[] { library }, notes = "Lena Graf wants white roses",
        });
        await Json(await client.PostAsync($"/api/private-events/{Id(wedding)}/deposit", Body(new { amount = 500m })), "ask Lena's deposit");
        var anna = await MemberAsync(client, "Anna");
        var party = await EventAsync(client, Party(await CompanyAsync(client)));
        await Json(await client.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { memberId = anna })), "Anna on Acme's list");
        await Json(await client.PostAsync($"/api/private-events/{Id(party)}/guests", Body(new { name = "Lena Graf", email = "lena@graf.ch" })), "Lena on Acme's list");

        var export = await client.PostAsync("/api/privacy/export", Body(new { contactIds = new[] { lena } }));
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(), ZipArchiveMode.Read))
        {
            using var hired = new StreamReader(zip.GetEntry("club/private-events.json")!.Open());
            (await hired.ReadToEndAsync()).Should().Contain("Lena Graf's wedding").And.Contain("white roses");
            using var lists = new StreamReader(zip.GetEntry("club/private-event-guest-lists.json")!.Open());
            (await lists.ReadToEndAsync()).Should().Contain("Acme summer party", "the list she is on names its event");
        }

        await Json(await client.PostAsync("/api/privacy/erasure", Body(new { contactIds = new[] { lena }, memberIds = new[] { anna }, confirm = "ERASE" })), "erase Lena and Anna");

        var kept = await Json(await client.GetAsync($"/api/private-events/{Id(wedding)}"), "the wedding, after");
        kept.TryGetProperty("contactId", out _).Should().BeFalse("the link to her contact goes");
        kept.GetProperty("title").GetString().Should().NotContain("Lena").And.EndWith("'s wedding");
        kept.GetProperty("notes").GetString().Should().NotContain("Lena").And.Contain("white roses");
        kept.GetProperty("money").GetProperty("depositAmount").GetDecimal().Should().Be(500m, "its money stays for the books");
        kept.GetProperty("money").GetProperty("billToAccountId").GetGuid().Should().Be(lena, "on the account it went on, under her pseudonym");
        var weddingId = Id(wedding);
        (await factory.Services.Read(db => db.ClubReservations.Single(r => r.PrivateEventId == weddingId).MemberName))
            .Should().NotContain("Lena", "the diary's hold carries the event's title");
        (await Json(await client.GetAsync($"/api/private-events/{Id(party)}"), "Acme's party, after")).GetProperty("guests").GetArrayLength()
            .Should().Be(0, "a place on a guest list is about the guest alone");
    }
}
