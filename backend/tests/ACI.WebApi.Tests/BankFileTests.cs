using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The bank's camt files: a transfer quoting a statement's QR reference is the member's
/// payment, recorded once however often the file is imported; one quoting nothing waits for
/// someone to say whose it is.
/// </summary>
public class BankFileTests
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

    internal static async Task<(HttpClient Client, Guid Org)> ClubAsync(CustomWebApplicationFactory app, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, userId, "Pavillon 46");
        client.In(org);
        await Json(await client.PutAsync("/api/payment-settings", Body(new
        {
            iban = "CH5800791123000889012", qrIban = "CH4431999123000889012",
            creditorName = "Pavillon 46", creditorStreet = "Rue du Lac", creditorBuildingNumber = "1268",
            creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
        })), "set the bank");
        return (client, org);
    }

    internal static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    /// <summary>Prints the member's statement, which issues its QR reference, and returns that reference.</summary>
    internal static async Task<string> StatementReferenceAsync(CustomWebApplicationFactory app, HttpClient client, Guid memberId)
    {
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("o"));
        (await client.GetAsync($"/api/members/{memberId}/statement.pdf?from={from}&to={to}")).StatusCode.Should().Be(HttpStatusCode.OK);
        return await app.Services.Read(db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.MemberId == memberId).QrReference!);
    }

    /// <summary>A camt.054 as a Swiss bank sends it: a batch of QR-bill payments, each with its reference, and a fee debited.</summary>
    internal static string Camt054(params (string Ref, decimal Amount, string? Reference)[] credits)
    {
        var details = string.Concat(credits.Select(c => $"""
            <TxDtls>
              <Refs><AcctSvcrRef>{c.Ref}</AcctSvcrRef></Refs>
              <Amt Ccy="CHF">{c.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <RltdPties><Dbtr><Pty><Nm>A Member</Nm></Pty></Dbtr></RltdPties>
              {(c.Reference != null
                  ? $"<RmtInf><Strd><CdtrRefInf><Tp><CdOrPrtry><Prtry>QRR</Prtry></CdOrPrtry></Tp><Ref>{c.Reference}</Ref></CdtrRefInf></Strd></RmtInf>"
                  : "<RmtInf><Ustrd>For the club</Ustrd></RmtInf>")}
            </TxDtls>
            """));
        var total = credits.Sum(c => c.Amount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.054.001.08">
              <BkToCstmrDbtCdtNtfctn>
                <GrpHdr><MsgId>MSG-1</MsgId></GrpHdr>
                <Ntfctn>
                  <Acct><Id><IBAN>CH4431999123000889012</IBAN></Id></Acct>
                  <Ntry>
                    <Amt Ccy="CHF">{total}</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
                    <BookgDt><Dt>{DateTime.UtcNow:yyyy-MM-dd}</Dt></BookgDt><AcctSvcrRef>ENTRY-1</AcctSvcrRef>
                    <NtryDtls>{details}</NtryDtls>
                  </Ntry>
                  <Ntry>
                    <Amt Ccy="CHF">4.50</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts><Cd>BOOK</Cd></Sts>
                    <BookgDt><Dt>{DateTime.UtcNow:yyyy-MM-dd}</Dt></BookgDt><AcctSvcrRef>FEE-1</AcctSvcrRef>
                  </Ntry>
                </Ntfctn>
              </BkToCstmrDbtCdtNtfctn>
            </Document>
            """;
    }

    internal static Task<HttpResponseMessage> ImportAsync(HttpClient client, string xml, string name = "camt054.xml") =>
        client.PostAsync("/api/bank/imports", Body(new { fileName = name, content = xml }));

    [Fact]
    public async Task ATransferQuotingAStatementsReferencePaysIt_AndAReimportRecordsNothingTwice()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await ClubAsync(app);
        var memberId = await MemberAsync(owner);
        var charge = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId, kind = "Dues", description = "Gold dues", amount = 299 })), "dues"))
            .GetProperty("id").GetGuid();
        var reference = await StatementReferenceAsync(app, owner, memberId);
        var file = Camt054(("TX-1", 350m, reference), ("TX-2", 51m, null));

        var import = await Json(await ImportAsync(owner, file), "import the file");
        import.GetProperty("format").GetString().Should().Be("camt.054");
        import.GetProperty("transactions").GetInt32().Should().Be(2);
        import.GetProperty("matched").GetInt32().Should().Be(1);
        import.GetProperty("unmatched").GetInt32().Should().Be(1);
        import.GetProperty("skipped").GetInt32().Should().Be(1, "the bank's fee is a debit, not a member's payment");
        import.GetProperty("amountMatched").GetDecimal().Should().Be(350m);

        var charges = await Json(await owner.GetAsync($"/api/charges?memberId={memberId}"), "the member's charges");
        charges.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == charge).GetProperty("status").GetString().Should().Be("Paid");
        var payment = (await Json(await owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).EnumerateArray().Single();
        payment.GetProperty("amount").GetDecimal().Should().Be(350m);
        payment.GetProperty("method").GetString().Should().Be("Wire");
        payment.GetProperty("provider").GetString().Should().Be("bank");
        payment.GetProperty("providerPaymentId").GetString().Should().Be("CH4431999123000889012:TX-1");
        var balance = await Json(await owner.GetAsync($"/api/members/{memberId}/balance"), "balance");
        balance.GetProperty("balance").GetDecimal().Should().Be(51m, "the 51 paid beyond the dues is the member's credit, not lost");

        var again = await Json(await ImportAsync(owner, file), "import the same file again");
        again.GetProperty("matched").GetInt32().Should().Be(0);
        again.GetProperty("alreadyImported").GetInt32().Should().Be(2);
        (await app.Services.Read(db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.OrganizationId == org && p.EntryType == ClubEntryTypes.Payment)))
            .Should().Be(1, "a transfer is recorded once");
        (await Json(await owner.GetAsync("/api/bank/imports"), "the files")).GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task AnUnmatchedTransferWaitsUntilSomeoneSaysWhoseItIs_OrSetsItAsideWithAReason()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await ClubAsync(app);
        var ana = await MemberAsync(owner, "Ana");
        var bar = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = ana, kind = "Bar", description = "Bar", amount = 51 })), "bar"))
            .GetProperty("id").GetGuid();
        await Json(await ImportAsync(owner, Camt054(("TX-9", 51m, null), ("TX-10", 20m, null))), "import");

        var unmatched = await Json(await owner.GetAsync("/api/bank/transactions?status=unmatched"), "unmatched");
        unmatched.GetArrayLength().Should().Be(2);
        var transfer = unmatched.EnumerateArray().Single(t => t.GetProperty("amount").GetDecimal() == 51m);
        transfer.GetProperty("note").GetString().Should().Contain("No reference");
        transfer.GetProperty("debtorName").GetString().Should().Be("A Member");
        var id = transfer.GetProperty("id").GetGuid();

        var allocated = await Json(await owner.PostAsync($"/api/bank/transactions/{id}/allocate", Body(new { memberId = ana })), "allocate to Ana");
        allocated.GetProperty("status").GetString().Should().Be("allocated");
        allocated.GetProperty("memberId").GetGuid().Should().Be(ana);
        var charges = await Json(await owner.GetAsync($"/api/charges?memberId={ana}"), "Ana's charges");
        charges.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == bar).GetProperty("status").GetString().Should().Be("Paid");
        await Refused(await owner.PostAsync($"/api/bank/transactions/{id}/allocate", Body(new { memberId = ana })), HttpStatusCode.Conflict, "allocate it twice");

        var other = (await Json(await owner.GetAsync("/api/bank/transactions?status=unmatched"), "unmatched")).EnumerateArray().Single().GetProperty("id").GetGuid();
        await Refused(await owner.PostAsync($"/api/bank/transactions/{other}/ignore", Body(new { reason = "" })), HttpStatusCode.BadRequest, "set aside without a reason");
        var ignored = await Json(await owner.PostAsync($"/api/bank/transactions/{other}/ignore", Body(new { reason = "A supplier's refund" })), "set aside");
        ignored.GetProperty("status").GetString().Should().Be("ignored");
        ignored.GetProperty("note").GetString().Should().Be("A supplier's refund");
        (await Json(await owner.GetAsync("/api/bank/transactions?status=unmatched"), "unmatched")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ATransferQuotingAMergedMembersOldStatementLandsOnTheKeptMembersAccount()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await ClubAsync(app);
        var keep = await MemberAsync(owner, "Jean");
        var drop = await MemberAsync(owner, "Jeanne");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = drop, kind = "Dues", description = "Gold dues", amount = 120 })), "dues on the duplicate");
        var reference = await StatementReferenceAsync(app, owner, drop);

        (await owner.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var import = await Json(await ImportAsync(owner, Camt054(("TX-1", 120m, reference))), "import");

        import.GetProperty("matched").GetInt32().Should().Be(1, "the reference moved with the duplicate's statement");
        var balance = await Json(await owner.GetAsync($"/api/members/{keep}/balance"), "the kept member's balance");
        balance.GetProperty("balance").GetDecimal().Should().Be(0m, "the dues moved to the kept member, and the transfer paid them");
    }

    [Fact]
    public async Task AFileThatIsNotACamtFileIsRefused_AndTheFrontDeskCannotImportOrReadBankFiles()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await ClubAsync(app);
        (await Refused(await ImportAsync(owner, "Date;Amount\n2026-11-01;10"), HttpStatusCode.BadRequest, "a CSV")).Should().Contain("camt");

        var (desk, deskId) = await SignInAsync(app, "desk@pavillon46.ch");
        await AddMemberAsync(app, org, deskId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{deskId}/permissions", Body(new { preset = "front_desk" })), "make front desk");
        desk.In(org);
        await Refused(await ImportAsync(desk, Camt054(("TX-1", 10m, null))), HttpStatusCode.Forbidden, "front desk imports");
        await Refused(await desk.GetAsync("/api/bank/transactions"), HttpStatusCode.Forbidden, "front desk reads transfers");
    }

    [Fact]
    public async Task AnotherWorkspacesTransfersCanBeNeitherSeenNorAllocated()
    {
        await using var app = new PaymentsApp();
        var (alpine, _) = await ClubAsync(app, "owner@alpine.example");
        await Json(await ImportAsync(alpine, Camt054(("TX-1", 10m, null))), "Alpine imports");
        var id = (await Json(await alpine.GetAsync("/api/bank/transactions"), "Alpine's")).EnumerateArray().Single().GetProperty("id").GetGuid();

        var (lakeside, _) = await ClubAsync(app, "owner@lakeside.example");
        var someone = await MemberAsync(lakeside, "Lea");
        (await Json(await lakeside.GetAsync("/api/bank/transactions"), "Lakeside's")).GetArrayLength().Should().Be(0);
        await Refused(await lakeside.PostAsync($"/api/bank/transactions/{id}/allocate", Body(new { memberId = someone })), HttpStatusCode.NotFound, "Lakeside allocates Alpine's");
        await Refused(await lakeside.PostAsync($"/api/bank/transactions/{id}/ignore", Body(new { reason = "not ours" })), HttpStatusCode.NotFound, "Lakeside sets Alpine's aside");
        // And the same file in another workspace is that workspace's own transfer.
        (await Json(await ImportAsync(lakeside, Camt054(("TX-1", 10m, null))), "Lakeside imports the same file")).GetProperty("transactions").GetInt32().Should().Be(1);
    }
}
