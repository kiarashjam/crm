using System.Net;
using System.Text.Json;
using ACI.Application.Common.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Where households meet the money that comes from outside: a spouse's bill rolled up to the
/// household's primary is on the primary's account, so the primary's QR-bills ask for it and a
/// transfer quoting them pays it.
/// </summary>
/// <remarks>
/// The household statement was built beside the QR-bill and missed it: it asked for the
/// household's bills with no QR-bill to pay them by, while the primary's own statement for the
/// same account carried one.
/// </remarks>
public class HouseholdPaymentsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Meier", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>The Meier household: Peter its primary, Anna his spouse with her bills rolling up to him.</summary>
    private static async Task<(Guid Household, Guid Peter, Guid Anna)> MeiersAsync(HttpClient client)
    {
        var peter = await MemberAsync(client, "Peter");
        var anna = await MemberAsync(client, "Anna");
        var household = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier", primaryMemberId = peter })), "household"))
            .GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/households/{household}/members", Body(new { memberId = anna, role = "Spouse", rollUpBilling = true })), "add Anna");
        return (household, peter, anna);
    }

    /// <summary>Anna's massage, which goes on Peter's account because her bills roll up to him.</summary>
    private static async Task<Guid> AnnasMassageAsync(HttpClient client, Guid peter, Guid anna)
    {
        var charge = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Massage", amount = 120 })), "Anna's massage");
        charge.GetProperty("memberId").GetGuid().Should().Be(peter, "Anna's bills roll up to Peter");
        return charge.GetProperty("id").GetGuid();
    }

    private static async Task<string> PdfTextAsync(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        return PdfText.Of(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TheHouseholdStatementCarriesThePrimarysQrBill_AndATransferQuotingItPaysTheSpousesRolledUpBill()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await BankFileTests.ClubAsync(app);
        var (household, peter, anna) = await MeiersAsync(owner);
        var massage = await AnnasMassageAsync(owner, peter, anna);
        var from = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(2).ToString("o"));

        var householdText = await PdfTextAsync(owner, $"/api/households/{household}/statement.pdf?from={from}&to={to}");
        var ownText = await PdfTextAsync(owner, $"/api/members/{peter}/statement.pdf?from={from}&to={to}");

        // One reference for Peter's account and the period, whichever of the two printed it first.
        var reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.OrganizationId == org));
        reference.MemberId.Should().Be(peter, "the household's bills are on the primary's account");
        reference.Purpose.Should().Be("statement");
        var printed = PaymentReferences.Format(reference.QrReference!);
        householdText.Should().Contain("Household statement").And.Contain("Incurred by Anna Meier");
        householdText.Should().Contain("Payment part").And.Contain("Receipt").And.Contain(printed).And.Contain("120.00",
            "the household statement asks for what the primary's account owes, with the QR-bill to pay it by");
        householdText.Should().Contain("Pay with the QR-bill at the foot of the statement");
        ownText.Should().Contain(printed, "the primary's own statement for the period carries the same reference");

        // The family pays by the household statement: Anna's massage, on Peter's account, is paid.
        var imported = await Json(await BankFileTests.ImportAsync(owner, BankFileTests.Camt054(("TX-MEIER", 120m, reference.QrReference))), "import the transfer");
        imported.GetProperty("matched").GetInt32().Should().Be(1);
        var charges = (await Json(await owner.GetAsync($"/api/charges?memberId={peter}"), "Peter's charges")).EnumerateArray().ToList();
        var paid = charges.Single(c => c.GetProperty("id").GetGuid() == massage);
        paid.GetProperty("status").GetString().Should().Be("Paid");
        paid.GetProperty("incurredByMemberId").GetGuid().Should().Be(anna, "it is still Anna's massage");
        (await Json(await owner.GetAsync($"/api/members/{peter}/balance"), "Peter's balance")).GetProperty("balance").GetDecimal().Should().Be(0m);
        (await Json(await owner.GetAsync($"/api/members/{anna}/balance"), "Anna's balance")).GetProperty("balance").GetDecimal()
            .Should().Be(0m, "nothing was ever on Anna's own account, and the transfer did not land there");
    }
}
