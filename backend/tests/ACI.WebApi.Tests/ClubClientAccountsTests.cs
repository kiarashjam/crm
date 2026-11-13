using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// House accounts for clients who are not members: a company, or a private client. The ledger
/// only knew members, so a company's dues or a private event's invoice could only go on some
/// member's account. Each test posts or reads money and checks whose account it landed on.
/// </summary>
public class ClubClientAccountsTests
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

    private static async Task<Guid> CompanyAsync(HttpClient client, string name = "Acme AG") =>
        (await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}")).GetProperty("id").GetGuid();

    [Fact]
    public async Task ACompanysAccount_IsOpenedOnce_TakesChargesAndPayments_AndItsStatementIsTheCompanys()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);

        var before = await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "read the account");
        before.GetProperty("open").GetBoolean().Should().BeFalse();
        // A charge naming the company before its account is open has nowhere to go.
        await Refused(await client.PostAsync("/api/charges", Body(new { memberId = acme, kind = "Event", description = "Room hire", amount = 500m })),
            HttpStatusCode.NotFound, "a charge before the account is open");

        var opened = await Json(await client.PostAsync($"/api/club-accounts/companies/{acme}", null), "open the account");
        opened.GetProperty("open").GetBoolean().Should().BeTrue();
        opened.GetProperty("name").GetString().Should().Be("Acme AG");
        opened.GetProperty("currency").GetString().Should().Be("CHF");
        (await Json(await client.PostAsync($"/api/club-accounts/companies/{acme}", null), "open it again")).GetProperty("open").GetBoolean().Should().BeTrue();
        (await factory.Services.Read(db => db.ClubHouseAccounts.Count(a => a.CompanyId == acme))).Should().Be(1, "opening twice opens one account");

        var charge = await Json(await client.PostAsync("/api/charges", Body(new { memberId = acme, kind = "Event", description = "Room hire", amount = 500m })), "charge the company");
        charge.GetProperty("memberId").GetGuid().Should().Be(acme);
        charge.GetProperty("memberName").GetString().Should().Be("Acme AG");
        charge.GetProperty("accountType").GetString().Should().Be("company");

        var account = await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "read the account");
        account.GetProperty("balance").GetDecimal().Should().Be(-500m);
        account.GetProperty("owed").GetDecimal().Should().Be(500m);
        account.GetProperty("openCharges").GetInt32().Should().Be(1);

        var paid = await Json(await client.PostAsync("/api/payments", Body(new { chargeId = charge.GetProperty("id").GetGuid(), amount = 500m, method = "Wire" })), "the company pays");
        paid.GetProperty("accountType").GetString().Should().Be("company");
        (await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "read the account")).GetProperty("balance").GetDecimal().Should().Be(0m);

        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var statement = await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}/statement?from={from}&to={to}"), "the statement");
        statement.GetProperty("memberName").GetString().Should().Be("Acme AG");
        statement.GetProperty("lines").GetArrayLength().Should().Be(2);
        statement.GetProperty("closingBalance").GetDecimal().Should().Be(0m);

        var pdf = await client.GetAsync($"/api/club-accounts/companies/{acme}/statement.pdf?from={from}&to={to}&email=accounts@acme.ch");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        var text = PdfText.Of(await pdf.Content.ReadAsByteArrayAsync());
        text.Should().Contain("Acme AG").And.Contain("Room hire").And.Contain("accounts@acme.ch");

        // The club's lists say the entries are a company's, so a screen links them to the company.
        var charges = await Json(await client.GetAsync("/api/charges"), "the charges");
        charges.EnumerateArray().Single().GetProperty("accountType").GetString().Should().Be("company");
    }

    [Fact]
    public async Task APrivateClientsAccount_IsTheirs_ButAContactWhoIsAMember_IsBilledOnTheirMembersAccount()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var clara = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Clara Dupont", email = "clara@example.ch" })), "add Clara")).GetProperty("id").GetGuid();
        var eva = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Eva Muster", email = "eva@example.ch" })), "add Eva")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Eva", lastName = "Muster", email = "eva@example.ch", tier = "Gold", status = "Active", contactId = eva,
        })), "Eva is a member");

        var opened = await Json(await client.PostAsync($"/api/club-accounts/contacts/{clara}", null), "open Clara's account");
        opened.GetProperty("kind").GetString().Should().Be("contact");
        var charge = await Json(await client.PostAsync("/api/charges", Body(new { memberId = clara, kind = "Event", description = "Wedding deposit", amount = 2000m })), "charge Clara");
        charge.GetProperty("accountType").GetString().Should().Be("contact");
        charge.GetProperty("memberName").GetString().Should().Be("Clara Dupont");

        var refused = await Refused(await client.PostAsync($"/api/club-accounts/contacts/{eva}", null), HttpStatusCode.BadRequest, "an account for a member's contact");
        refused.Should().Contain("member's house account");
    }

    [Fact]
    public async Task AnotherWorkspacesCompany_HasNoAccountHere_AndCannotBeBilledFromHere()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ours, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(ours);
        await Json(await ours.PostAsync($"/api/club-accounts/companies/{acme}", null), "open Acme's account");

        var (theirs, _, _) = await ClubAsync(factory, "owner@elsewhere.ch");
        await Refused(await theirs.GetAsync($"/api/club-accounts/companies/{acme}"), HttpStatusCode.NotFound, "reading another workspace's company");
        await Refused(await theirs.PostAsync($"/api/club-accounts/companies/{acme}", null), HttpStatusCode.NotFound, "opening another workspace's company");
        await Refused(await theirs.PostAsync("/api/charges", Body(new { memberId = acme, kind = "Event", description = "Sneaky", amount = 10m })),
            HttpStatusCode.NotFound, "charging another workspace's company");
        (await factory.Services.Read(db => db.ClubCharges.Count())).Should().Be(0);
    }

    [Fact]
    public async Task OpeningAnAccount_TakesTheMoneyPermission_ReadingItDoesNot()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var acme = await CompanyAsync(owner);
        var (desk, deskId) = await SignInAsync(factory, "desk@pavillon46.ch");
        await AddMemberAsync(factory, org, deskId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{deskId}/permissions", Body(new { preset = "front_desk" })), "make them front desk");
        desk.In(org);

        await Refused(await desk.PostAsync($"/api/club-accounts/companies/{acme}", null), HttpStatusCode.Forbidden, "opening without money.manage");
        (await Json(await desk.GetAsync($"/api/club-accounts/companies/{acme}"), "reading it")).GetProperty("open").GetBoolean().Should().BeFalse();
    }
}
