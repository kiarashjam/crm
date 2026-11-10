using System.Net;
using System.Text.Json;
using ACI.Application.Common.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An invoice for one charge: a PDF asking for what is still owed on it, with a Swiss QR-bill
/// whose reference names that charge, so a bank transfer quoting it pays that charge first,
/// even when older charges are open; the same reference however often it is printed.
/// </summary>
public class ChargeInvoiceTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> ChargeAsync(HttpClient owner, Guid memberId, string kind, string description, decimal amount, decimal? vatRate = null) =>
        (await Json(await owner.PostAsync("/api/charges", Body(new { memberId, kind, description, amount, vatRate })), description))
            .GetProperty("id").GetGuid();

    private static async Task<string> InvoiceTextAsync(HttpClient client, Guid chargeId)
    {
        var res = await client.GetAsync($"/api/charges/{chargeId}/invoice.pdf");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        res.Content.Headers.ContentDisposition!.FileName.Should().StartWith("invoice-C-");
        return PdfText.Of(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AnInvoiceAsksForWhatIsLeftOnTheCharge_WithAReferenceForItAlone_TheSameEachTime()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await BankFileTests.ClubAsync(app);
        var memberId = await BankFileTests.MemberAsync(owner);
        var spa = await ChargeAsync(owner, memberId, "Spa", "Massage", 120, 8.1m);
        await Json(await owner.PostAsync("/api/payments", Body(new { amount = 20, method = "Cash", chargeId = spa })), "20 in cash");

        var text = await InvoiceTextAsync(owner, spa);

        var number = "C-" + spa.ToString("N")[..8].ToUpperInvariant();
        text.Should().Contain("Invoice").And.Contain(number).And.Contain("Massage").And.Contain("VAT 8.1% included").And.Contain("Paid so far");
        text.Should().Contain("Payment part").And.Contain("Receipt").And.Contain("100.00", "the slip asks for what is left: 120 less the 20 paid");
        var reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.OrganizationId == org));
        reference.Purpose.Should().Be("charge");
        reference.ChargeId.Should().Be(spa);
        reference.MemberId.Should().Be(memberId);
        PaymentReferences.IsQrReference(reference.QrReference).Should().BeTrue();
        text.Should().Contain(PaymentReferences.Format(reference.QrReference!)).And.Contain("Invoice " + number);

        await InvoiceTextAsync(owner, spa);
        (await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Count(r => r.OrganizationId == org)))
            .Should().Be(1, "printed again, the invoice carries the same reference");
    }

    [Fact]
    public async Task ATransferQuotingTheInvoicePaysThatCharge_NotTheOldestOne()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await BankFileTests.ClubAsync(app);
        var memberId = await BankFileTests.MemberAsync(owner);
        var dues = await ChargeAsync(owner, memberId, "Dues", "Gold dues", 299);
        var spa = await ChargeAsync(owner, memberId, "Spa", "Massage", 120);
        await InvoiceTextAsync(owner, spa);
        var reference = await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.ChargeId == spa).QrReference!);

        var imported = await Json(await BankFileTests.ImportAsync(owner, BankFileTests.Camt054(("TX-9", 120m, reference))), "import the transfer");

        imported.GetProperty("matched").GetInt32().Should().Be(1);
        var charges = (await Json(await owner.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray().ToList();
        charges.Single(c => c.GetProperty("id").GetGuid() == spa).GetProperty("status").GetString().Should().Be("Paid");
        charges.Single(c => c.GetProperty("id").GetGuid() == dues).GetProperty("paidAmount").GetDecimal()
            .Should().Be(0m, "the older dues are not what the transfer paid");
        var text = await InvoiceTextAsync(owner, spa);
        text.Should().Contain("Paid in full").And.NotContain("Payment part", "nothing is owed on it any more");
    }

    [Fact]
    public async Task AVoidedChargeAsksForNothing_NoBankDetailsMeansNoSlip_AndAnotherWorkspacesChargeIsNotFound()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await BankFileTests.ClubAsync(app);
        var memberId = await BankFileTests.MemberAsync(owner);
        var bar = await ChargeAsync(owner, memberId, "Bar", "Bar tab", 40);
        await Json(await owner.PostAsync($"/api/charges/{bar}/void", Body(new { reason = "Entered twice" })), "void it");

        (await InvoiceTextAsync(owner, bar)).Should().Contain("Cancelled: nothing to pay").And.NotContain("Payment part");

        var (other, otherOrg) = await BankFileTests.ClubAsync(app, "owner@lakeside.example");
        (await other.GetAsync($"/api/charges/{bar}/invoice.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A club without bank details prints the invoice with no slip.
        var (plain, plainUser) = await SignInAsync(app, "owner@plain.example");
        var plainOrg = await CreateAsync(app, plainUser, "Plain Club");
        plain.In(plainOrg);
        var someone = await BankFileTests.MemberAsync(plain, "Ana");
        var fee = await ChargeAsync(plain, someone, "Other", "Locker fee", 25);
        var text = await InvoiceTextAsync(plain, fee);
        text.Should().Contain("Amount due").And.NotContain("Payment part");
        otherOrg.Should().NotBe(plainOrg);
    }
}
