using System.Net;
using System.Text.Json;
using ACI.Application.Common.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The Swiss QR-bill on a member's statement: printed once the club's bank details are set and
/// something is due, with a reference that names the member and the statement, the same one
/// every time the statement is printed.
/// </summary>
public class QrBillStatementTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid Org)> ClubAsync(PaymentsApp app, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, userId, "Pavillon 46");
        return (client.In(org), org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    private static async Task SetBankAsync(HttpClient client, string? qrIban = null) =>
        await Json(await client.PutAsync("/api/payment-settings", Body(new
        {
            iban = "CH5800791123000889012", qrIban, qrReferencePrefix = qrIban != null ? "1234" : null,
            creditorName = "Pavillon 46", creditorStreet = "Rue du Lac", creditorBuildingNumber = "1268",
            creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
        })), "set the bank");

    private static async Task<string> StatementTextAsync(HttpClient client, Guid memberId, string from, string to)
    {
        var res = await client.GetAsync($"/api/members/{memberId}/statement.pdf?from={from}&to={to}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return PdfText.Of(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AStatementCarriesAQrBillForWhatIsDue_WithTheSameReferenceEveryTime()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await ClubAsync(app);
        var memberId = await MemberAsync(owner);
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId, kind = "Dues", description = "Gold dues", amount = 299 })), "dues");
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("o"));

        (await StatementTextAsync(owner, memberId, from, to)).Should().NotContain("Payment part", "without bank details there is no slip");

        await SetBankAsync(owner);
        var text = await StatementTextAsync(owner, memberId, from, to);
        text.Should().Contain("Payment part").And.Contain("Receipt").And.Contain("CH58 0079 1123 0008 8901 2").And.Contain("299.00");
        var reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.OrganizationId == org));
        reference.MemberId.Should().Be(memberId);
        reference.AmountDue.Should().Be(299m);
        PaymentReferences.IsCreditorReference(reference.ScorReference).Should().BeTrue();
        text.Should().Contain(PaymentReferences.Format(reference.ScorReference), "with a plain IBAN the slip carries the creditor reference");

        // Printed again: the same reference, not a second one.
        await StatementTextAsync(owner, memberId, from, to);
        (await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Count(r => r.OrganizationId == org))).Should().Be(1);

        // With a QR-IBAN the same statement gets its QR reference, from the same number, after the bank's prefix.
        await SetBankAsync(owner, "CH4431999123000889012");
        text = await StatementTextAsync(owner, memberId, from, to);
        reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.OrganizationId == org));
        reference.QrReference.Should().StartWith("1234").And.HaveLength(27);
        PaymentReferences.IsQrReference(reference.QrReference).Should().BeTrue();
        text.Should().Contain("CH44 3199 9123 0008 8901 2").And.Contain(PaymentReferences.Format(reference.QrReference));

        var found = await Json(await owner.GetAsync($"/api/payment-references/{Uri.EscapeDataString(PaymentReferences.Format(reference.QrReference))}"), "look it up");
        found.GetProperty("memberId").GetGuid().Should().Be(memberId);
        found.GetProperty("purpose").GetString().Should().Be("statement");
        found.GetProperty("type").GetString().Should().Be("QRR");
    }

    [Fact]
    public async Task NothingDueMeansNoSlip_AndAnotherWorkspaceCannotLookUpTheReference()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await ClubAsync(app);
        await SetBankAsync(owner);
        var paidUp = await MemberAsync(owner, "Ana");
        var owing = await MemberAsync(owner, "Ben");
        var charge = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = owing, kind = "Bar", description = "Bar", amount = 40 })), "charge"))
            .GetProperty("id").GetGuid();
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("o"));

        (await StatementTextAsync(owner, paidUp, from, to)).Should().NotContain("Payment part", "nothing is due");
        (await StatementTextAsync(owner, owing, from, to)).Should().Contain("Payment part");
        await Json(await owner.PostAsync("/api/payments", Body(new { amount = 40, method = "Cash", chargeId = charge })), "paid in cash");
        var later = Uri.EscapeDataString(DateTime.UtcNow.AddDays(2).ToString("o"));
        (await StatementTextAsync(owner, owing, from, later)).Should().NotContain("Payment part", "paid: nothing is due at the end");

        var reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.OrganizationId == org));
        var (other, _) = await ClubAsync(app, "owner@lakeside.example");
        (await other.GetAsync($"/api/payment-references/{reference.ScorReference}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
