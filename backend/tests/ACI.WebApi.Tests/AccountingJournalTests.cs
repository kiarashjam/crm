using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The accounting journal: the ledger's entries as balanced bookings in the club's accounts,
/// adding up to what the members owe and hold, period by period, as a CSV Bexio and Abacus import.
/// </summary>
public class AccountingJournalTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string Period(DateTime from, DateTime to) =>
        $"from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";

    private static Dictionary<string, decimal> NetByAccount(JsonElement journal) =>
        journal.GetProperty("accounts").EnumerateArray().ToDictionary(a => a.GetProperty("account").GetString()!, a => a.GetProperty("net").GetDecimal());

    /// <summary>
    /// A member's month: dues with VAT and a bar tab, paid part in cash two days ago and the rest by a
    /// bank transfer with 61 over, a spa booking charged and voided, 20 refunded in cash, and a gift
    /// card sold and part spent.
    /// </summary>
    private static async Task<(HttpClient Owner, Guid Org, Guid Member)> MonthAsync(PaymentsApp app)
    {
        var (owner, org) = await BankFileTests.ClubAsync(app);
        await Json(await owner.PutAsync("/api/payment-settings", Body(new
        {
            iban = "CH5800791123000889012", qrIban = "CH4431999123000889012",
            creditorName = "Pavillon 46", creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
            accounts = new { giftCardLiability = "2035", vatCodes = new Dictionary<string, string> { ["8.1"] = "UN81" } },
        })), "accounts");
        var member = await BankFileTests.MemberAsync(owner);
        var dues = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = member, kind = "Dues", description = "Gold dues", amount = 299, vatRate = 8.1 })), "dues"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = member, kind = "Bar", description = "Bar tab", amount = 40 })), "bar");
        var spa = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = member, kind = "Spa", description = "Massage", amount = 50 })), "spa"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/charges/{spa}/void", Body(new { reason = "Booked twice" })), "void the spa");
        await Json(await owner.PostAsync("/api/payments", Body(new
        {
            amount = 100, method = "Cash", chargeId = dues, receivedAtUtc = DateTime.UtcNow.AddDays(-2).ToString("o"),
        })), "100 in cash, two days ago");
        var reference = await BankFileTests.StatementReferenceAsync(app, owner, member);
        await Json(await BankFileTests.ImportAsync(owner, BankFileTests.Camt054(("TX-1", 300m, reference))), "300 by bank transfer");
        var transfer = (await Json(await owner.GetAsync($"/api/payments?memberId={member}"), "payments")).EnumerateArray()
            .Single(p => p.GetProperty("method").GetString() == "Wire").GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/payments/{transfer}/refund", Body(new { amount = 20, reason = "Paid too much", method = "Cash" })), "20 back in cash");
        var card = (await Json(await owner.PostAsync("/api/gift-cards", Body(new { faceValue = 100, purchaserName = "Jean", recipientName = "Lea" })), "sell a gift card"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/gift-cards/{card}/redeem", Body(new { amount = 30 })), "spend 30 of it");
        return (owner, org, member);
    }

    [Fact]
    public async Task EveryBookingBalances_AndReceivablesAndPrepaymentsAreWhatMembersOweAndHold()
    {
        await using var app = new PaymentsApp();
        var (owner, _, member) = await MonthAsync(app);

        var journal = await Json(await owner.GetAsync($"/api/accounting/journal?{Period(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(1))}"), "the journal");

        var rows = journal.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().OnlyContain(r => r.GetProperty("amount").GetDecimal() > 0 && r.GetProperty("debit").GetString() != r.GetProperty("credit").GetString());
        var net = NetByAccount(journal);
        net.Values.Sum().Should().Be(0m, "every booking debits and credits the same amount");
        net["1100"].Should().Be(0m, "every charge is paid or voided");
        net["2030"].Should().Be(-41m, "the 61 paid over, less the 20 refunded, is the member's credit");
        var balance = await Json(await owner.GetAsync($"/api/members/{member}/balance"), "balance");
        balance.GetProperty("balance").GetDecimal().Should().Be(41m, "the ledger agrees");
        net["3400"].Should().Be(-299m, "dues, and the spa charge taken back");
        net["3200"].Should().Be(-40m);
        net["1000"].Should().Be(180m, "100 cash in, 20 cash back, 100 for the gift card");
        net["1020"].Should().Be(300m);
        net["2035"].Should().Be(-70m, "the gift card's 70 still to be spent, in the club's own account for them");
        net["3600"].Should().Be(-30m);

        var duesRow = rows.Single(r => r.GetProperty("type").GetString() == "Charge" && r.GetProperty("amount").GetDecimal() == 299m);
        duesRow.GetProperty("debit").GetString().Should().Be("1100");
        duesRow.GetProperty("vatCode").GetString().Should().Be("UN81");
        duesRow.GetProperty("vatRate").GetDecimal().Should().Be(8.1m);
        duesRow.GetProperty("vatAmount").GetDecimal().Should().Be(22.40m);
        rows.Should().Contain(r => r.GetProperty("type").GetString() == "ChargeVoid" && r.GetProperty("debit").GetString() == "3400");
    }

    [Fact]
    public async Task TwoPeriodsAddUpToBoth_EvenForAPaymentDatedBeforeItWasEntered()
    {
        await using var app = new PaymentsApp();
        var (owner, _, _) = await MonthAsync(app);
        var start = DateTime.UtcNow.AddDays(-7);
        var cut = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(1);

        var before = NetByAccount(await Json(await owner.GetAsync($"/api/accounting/journal?{Period(start, cut)}"), "before the cut"));
        var after = NetByAccount(await Json(await owner.GetAsync($"/api/accounting/journal?{Period(cut, end)}"), "after the cut"));
        var whole = NetByAccount(await Json(await owner.GetAsync($"/api/accounting/journal?{Period(start, end)}"), "the whole"));

        before.Should().ContainKey("1000", "the cash payment dated two days ago is in the earlier period");
        foreach (var account in whole.Keys.Union(before.Keys).Union(after.Keys))
            (before.GetValueOrDefault(account) + after.GetValueOrDefault(account)).Should().Be(whole.GetValueOrDefault(account), $"account {account} adds up");
    }

    [Fact]
    public async Task TheCsvIsTheDocumentedLayout_AndTheNetLayoutBooksVatOnALineOfItsOwn()
    {
        await using var app = new PaymentsApp();
        var (owner, _, _) = await MonthAsync(app);
        var period = Period(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(1));

        var res = await owner.GetAsync($"/api/accounting/journal?{period}&format=csv");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        res.Content.Headers.ContentDisposition!.FileName.Should().StartWith("journal-");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "a byte-order mark, so Excel reads UTF-8");
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Date;Document;Debit;Credit;Amount;Currency;VatCode;VatRate;VatAmount;Text;Member;Type;EntryId");
        var dues = lines.Single(l => l.Contains(";1100;3400;299.00;"));
        dues.Should().MatchRegex(@"^\d{2}\.\d{2}\.\d{4};C-[0-9A-F]{8};1100;3400;299\.00;CHF;UN81;8\.1;22\.40;Gold dues · Jean Dupont;Jean Dupont;Charge;[0-9a-f-]{36}$");

        var net = await Json(await owner.GetAsync($"/api/accounting/journal?{period}&layout=net"), "net layout");
        var rows = net.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().Contain(r => r.GetProperty("credit").GetString() == "3400" && r.GetProperty("amount").GetDecimal() == 276.60m && r.GetProperty("type").GetString() == "Charge");
        rows.Should().Contain(r => r.GetProperty("credit").GetString() == "2200" && r.GetProperty("amount").GetDecimal() == 22.40m && r.GetProperty("type").GetString() == "ChargeVat");
        NetByAccount(net)["2200"].Should().Be(-22.40m, "the VAT the dues included is owed");
    }

    [Fact]
    public async Task OnlyThoseWhoTakePaymentsExportTheJournal_AndAnotherWorkspacesIsNotInIt()
    {
        await using var app = new PaymentsApp();
        var (owner, org, _) = await MonthAsync(app);
        var period = Period(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(1));
        var (desk, deskId) = await SignInAsync(app, "desk@pavillon46.ch");
        await AddMemberAsync(app, org, deskId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{deskId}/permissions", Body(new { preset = "front_desk" })), "make front desk");
        desk.In(org);
        (await desk.GetAsync($"/api/accounting/journal?{period}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (other, _) = await BankFileTests.ClubAsync(app, "owner@lakeside.example");
        var theirs = await Json(await other.GetAsync($"/api/accounting/journal?{period}"), "Lakeside's journal");
        theirs.GetProperty("rows").GetArrayLength().Should().Be(0);
        (await owner.GetAsync($"/api/accounting/journal?{Period(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1))}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
