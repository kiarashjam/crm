using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The deposit a membership contract asks for: paid on the signing page through Stripe (played
/// by the test) or by a bank transfer quoting its QR-bill, kept until the club admits the signer,
/// then put on their account once, however often Stripe or the bank file says so, and in
/// whichever order the countersignature and the payment arrive.
/// </summary>
public class ContractDepositTests
{
    private const string Key = "sk_test_good_4242424242abcd";
    private const string Secret = "whsec_test_signing_secret";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org, string Webhook);

    private static void PlayStripe(FakeStripe stripe)
    {
        stripe.AcceptingKeys();
        stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
        {
            id = $"cs_test_{r.Form["client_reference_id"][..8]}_{r.IdempotencyKey![^1..]}",
            @object = "checkout.session",
            mode = r.Form["mode"],
            url = $"https://checkout.stripe.com/c/pay/cs_test_{r.Form["client_reference_id"][..8]}",
            status = "open",
            expires_at = long.Parse(r.Form["expires_at"]),
            metadata = new Dictionary<string, string>(),
        }));
    }

    /// <summary>A club that takes payments through Stripe (and, with <paramref name="bank"/>, by QR-bill too).</summary>
    private static async Task<Club> StripeClubAsync(PaymentsApp app, string email = "owner@pavillon46.ch")
    {
        PlayStripe(app.Stripe);
        var (client, userId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, userId, "Pavillon 46");
        client.In(org);
        var settings = await Json(await client.PutAsync("/api/payment-settings", Body(new
        {
            stripeSecretKey = Key, stripeWebhookSecret = Secret, stripeOfferTwint = true,
        })), "set Stripe up");
        return new Club(client, userId, org, new Uri(settings.GetProperty("webhookUrl").GetString()!).AbsolutePath);
    }

    /// <summary>
    /// A lead with an open Gold application, and their membership contract asking a deposit of
    /// 500, sent: the signing link's token and the contract's id.
    /// </summary>
    private static async Task<(Guid Lead, Guid Contract, string Token)> SentWithDepositAsync(HttpClient owner, decimal deposit = 500m, bool application = true)
    {
        var lead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();
        if (application)
        {
            var app = await Json(await owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead");
            await Json(await owner.PutAsync($"/api/applications/{app.GetProperty("id").GetGuid()}", Body(new { requestedTier = "Gold" })), "pick the tier");
        }
        var draft = await Json(await owner.PostAsync("/api/contracts/draft", Body(new
        {
            leadId = lead,
            membershipTier = "Gold",
            depositAmount = deposit,
            values = new Dictionary<string, string>
            {
                ["contract.fee"] = "CHF 4,800 a year", ["contract.paymentTerms"] = "annually in advance", ["contract.startDate"] = "1 December 2026",
                ["contract.term"] = "12 months", ["contract.noticePeriod"] = "three months'", ["contract.jurisdiction"] = "Switzerland",
            },
        })), "draft with a deposit");
        var id = draft.GetProperty("id").GetGuid();
        var sent = await Json(await owner.PostAsync($"/api/contracts/{id}/send", null), "send");
        return (lead, id, sent.GetProperty("signingUrl").GetString()!.Split("/sign/").Last());
    }

    private static Task<HttpResponseMessage> SignAsync(HttpClient anon, string token) =>
        anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Anna Muster", agreed = true }));

    private static Dictionary<string, string> MetadataOf(FakeStripe.Request request) =>
        request.Form.Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal))
            .ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);

    private static object IntentEvent(string eventId, string type, string intentId, string status, long amountMinor, Dictionary<string, string> metadata, string? failure = null) => new
    {
        id = eventId,
        @object = "event",
        type,
        data = new
        {
            @object = new
            {
                id = intentId,
                @object = "payment_intent",
                status,
                amount = amountMinor,
                amount_received = status == "succeeded" ? amountMinor : 0,
                currency = "chf",
                latest_charge = $"ch_{intentId}",
                payment_method_types = new[] { "twint" },
                metadata,
                last_payment_error = failure == null ? null : new { code = "card_declined", message = failure },
            },
        },
    };

    private static Task<T> Read<T>(CustomWebApplicationFactory app, Func<ACI.Infrastructure.Persistence.AppDbContext, T> read) => app.Services.Read(read);

    // ── asking ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ADepositIsAskedOnlyOnAMembershipContract_BySomeoneWhoMayTakePayments_WhenTheClubCanBePaid()
    {
        await using var app = new PaymentsApp();
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var lead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();

        (await Json(await owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold", depositAmount = 500m })), "no Stripe, no bank", HttpStatusCode.BadRequest))
            .GetProperty("errorCode").GetString().Should().Be("ContractDeposit.NoWayToPay");

        PlayStripe(app.Stripe);
        await Json(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = Secret })), "set Stripe up");
        (await Json(await owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, depositAmount = 500m })), "no tier", HttpStatusCode.BadRequest))
            .GetProperty("errorCode").GetString().Should().Be("ContractDeposit.NeedsTier");
        await Json(await owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold", depositAmount = 0m })), "nothing", HttpStatusCode.BadRequest);

        var draft = await Json(await owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold", depositAmount = 500m })), "a Gold contract with a deposit");
        (draft.GetProperty("depositAmount").GetDecimal(), draft.GetProperty("depositCurrency").GetString()).Should().Be((500m, "CHF"));
        draft.GetProperty("body").GetString().Should().Contain("A deposit of CHF 500.00 is payable on signing.");

        var (rep, repId) = await SignInAsync(app, "rep@pavillon46.ch");
        await AddMemberAsync(app, org, repId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{repId}/permissions", Body(new { preset = "sales_rep" })), "make a sales rep");
        rep.In(org);
        (await Json(await rep.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold", depositAmount = 500m })), "a rep asks for money", HttpStatusCode.Forbidden))
            .GetProperty("errorCode").GetString().Should().Be("Permission.money.manage");
        await Json(await rep.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold" })), "a rep drafts without one");

        // Changing the deposit on the draft changes the sentence the draft wrote, and taking it off removes it.
        var id = draft.GetProperty("id").GetGuid();
        (await Json(await owner.PutAsync($"/api/contracts/{id}", Body(new { depositAmount = 750m })), "change the deposit"))
            .GetProperty("body").GetString().Should().Contain("A deposit of CHF 750.00").And.NotContain("CHF 500.00");
        var off = await Json(await owner.PutAsync($"/api/contracts/{id}", Body(new { depositAmount = (decimal?)null })), "take it off");
        off.GetProperty("body").GetString().Should().NotContain("A deposit of");
        off.GetProperty("depositAmount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AContractWhoseTextNoLongerSaysTheDeposit_IsNotSent()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var lead = (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();
        var draft = await Json(await club.Owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, membershipTier = "Gold", depositAmount = 500m })), "draft");
        var id = draft.GetProperty("id").GetGuid();
        await Json(await club.Owner.PutAsync($"/api/contracts/{id}", Body(new { body = "MEMBERSHIP AGREEMENT\n\nGold membership.\n" })), "rewrite it without the deposit");

        (await Json(await club.Owner.PostAsync($"/api/contracts/{id}/send", null), "send", HttpStatusCode.BadRequest))
            .GetProperty("errorCode").GetString().Should().Be("ContractDeposit.NotInText");
    }

    // ── paying on the signing page ───────────────────────────────────────────

    [Fact]
    public async Task TheSigningPageShowsTheDeposit_AndPayStartsStripesPage_OnlyOnceSigned_OnceHoweverOftenItIsPressed()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, _, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();

        var page = await Json(await anon.GetAsync($"/api/public/contracts/{token}"), "open the link");
        var deposit = page.GetProperty("deposit");
        (deposit.GetProperty("amount").GetDecimal(), deposit.GetProperty("currency").GetString(), deposit.GetProperty("status").GetString())
            .Should().Be((500m, "CHF", "awaiting"));
        deposit.GetProperty("payable").GetBoolean().Should().BeFalse("it is paid once signed");
        deposit.GetProperty("methods").EnumerateArray().Select(m => m.GetString()).Should().Equal("card", "twint");
        page.GetProperty("membershipTier").GetString().Should().Be("Gold");
        (await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "pay before signing", HttpStatusCode.BadRequest))
            .GetProperty("errorCode").GetString().Should().Be("ContractDeposit.SignFirst");

        (await Json(await SignAsync(anon, token), "sign")).GetProperty("deposit").GetProperty("payable").GetBoolean().Should().BeTrue();
        var started = await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        started.GetProperty("url").GetString().Should().StartWith("https://checkout.stripe.com/");
        var again = await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay again");
        again.GetProperty("url").GetString().Should().Be(started.GetProperty("url").GetString());

        var sessions = app.Stripe.Sent("POST", "/v1/checkout/sessions");
        sessions.Should().ContainSingle("the page made once is offered again while it lasts");
        var form = sessions[0].Form;
        (form["mode"], form["line_items[0][price_data][unit_amount]"], form["line_items[0][price_data][currency]"]).Should().Be(("payment", "50000", "chf"));
        form["success_url"].Should().EndWith($"/sign/{token}?deposit=done");
        form["customer_email"].Should().Be("anna@example.ch");
        MetadataOf(sessions[0]).Should().ContainKeys("cadence_deposit", "cadence_contract", "cadence_org");
        form.Should().ContainKey("payment_intent_data[metadata][cadence_deposit]", "Stripe's word on the payment must name the deposit too");
    }

    [Fact]
    public async Task APaidDepositWaitsForTheClubsYes_ThenGoesOnTheNewMembersAccountOnce_PayingTheirInitiationFeeFirst()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (lead, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());

        var paid = IntentEvent("evt_dep_1", "payment_intent.succeeded", "pi_deposit_1", "succeeded", 50000, metadata);
        (await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, paid)), "Stripe says it is paid")).GetProperty("result").GetString().Should().Be("processed");
        // Stripe posts it again, and posts a second event about the same payment.
        (await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, paid)), "posted again")).GetProperty("result").GetString().Should().Be("already processed");
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_dep_2", "payment_intent.succeeded", "pi_deposit_1", "succeeded", 50000, metadata))), "a second event about the same payment");

        var held = await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single());
        (held.Status, held.AmountPaid, held.ProviderPaymentId, held.LedgerPaymentId).Should().Be((ContractDepositStatuses.Paid, (decimal?)500m, "pi_deposit_1", (Guid?)null));
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(0, "a paid deposit admits nobody on its own");
        (await Json(await anon.GetAsync($"/api/public/contracts/{token}"), "the page")).GetProperty("deposit").GetProperty("status").GetString().Should().Be("paid");

        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");

        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        member.LeadId.Should().Be(lead);
        var payments = await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Where(p => p.MemberId == member.Id && p.ReversesPaymentId == null).ToList());
        payments.Should().ContainSingle().Which.Should().Match<ClubPayment>(p => p.Amount == 500m && p.Provider == "stripe" && p.ProviderPaymentId == "pi_deposit_1" && p.Method == "Twint");
        var fee = await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single().InitiationChargeId);
        fee.Should().NotBeNull("the owner may charge accounts, so the approval posted the fee");
        var feeCharge = await Read(app, db => db.ClubCharges.IgnoreQueryFilters().Single(c => c.Id == fee));
        var paidOnFee = await Read(app, db => db.ClubPaymentAllocations.IgnoreQueryFilters().Where(a => a.ChargeId == fee).Sum(a => a.Amount));
        paidOnFee.Should().Be(Math.Min(500m, feeCharge.Amount), "the deposit pays the initiation fee first");
        var recorded = await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single());
        (recorded.MemberId, recorded.LedgerPaymentId).Should().Be(((Guid?)member.Id, (Guid?)payments[0].Id));

        // A retried countersignature, and Stripe's word again, add nothing.
        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign again", HttpStatusCode.BadRequest);
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_dep_3", "payment_intent.succeeded", "pi_deposit_1", "succeeded", 50000, metadata))), "Stripe again after the countersignature");
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(1);
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.ReversesPaymentId == null))).Should().Be(1, "the deposit is on the ledger once");

        var events = await Json(await club.Owner.GetAsync($"/api/contracts/{contract}"), "the contract");
        events.GetProperty("deposit").GetProperty("memberId").GetGuid().Should().Be(member.Id);
        events.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("type").GetString()).Should().Contain(["deposit_paid", "deposit_recorded"]);
    }

    [Fact]
    public async Task ADepositPaidAfterTheCountersignature_GoesOnTheAccountWhenStripeSaysSo_AndALateFailureChangesNothing()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());
        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign first");
        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.MemberId == member.Id))).Should().Be(0, "nothing is paid yet");

        // Out of order: the processing word comes after the money, and a failure after that.
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, IntentEvent("evt_a", "payment_intent.succeeded", "pi_late", "succeeded", 50000, metadata))), "paid");
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, IntentEvent("evt_b", "payment_intent.processing", "pi_late", "processing", 50000, metadata))), "late: processing");
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, IntentEvent("evt_c", "payment_intent.payment_failed", "pi_late", "requires_payment_method", 50000, metadata, "Declined"))), "late: failed");

        var deposit = await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single());
        (deposit.Status, deposit.MemberId).Should().Be((ContractDepositStatuses.Paid, (Guid?)member.Id));
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single(p => p.MemberId == member.Id).ProviderPaymentId)).Should().Be("pi_late");
        (await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "pay again", HttpStatusCode.Conflict))
            .GetProperty("errorCode").GetString().Should().Be("ContractDeposit.Paid.Conflict");
    }

    [Fact]
    public async Task AFailedPaymentLeavesTheDepositToBePaid_WithStripesReasonOnThePage()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, _, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());

        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_f", "payment_intent.payment_failed", "pi_f", "requires_payment_method", 50000, metadata, "Your card was declined."))), "declined");

        var deposit = (await Json(await anon.GetAsync($"/api/public/contracts/{token}"), "the page")).GetProperty("deposit");
        (deposit.GetProperty("status").GetString(), deposit.GetProperty("payable").GetBoolean(), deposit.GetProperty("failureMessage").GetString())
            .Should().Be(("awaiting", true, "Your card was declined."));
    }

    [Fact]
    public async Task AVoidedContractsDepositCanNoLongerBePaid()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");

        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/void", Body(new { reason = "Changed their mind" })), "void");

        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().Status)).Should().Be(ContractDepositStatuses.Cancelled);
        app.Stripe.Sent("POST", "/v1/checkout/sessions").Should().BeEmpty();
    }

    [Fact]
    public async Task ADepositPaidOnAContractTheClubThenVoided_IsNotPutOnAnAccount_WhenTheSignerIsAdmittedAnotherWay()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_v1", "payment_intent.succeeded", "pi_withdrawn", "succeeded", 50000, metadata))), "paid");

        // Withdrawn after it was paid: no agreement stands behind the money any more.
        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/void", Body(new { reason = "Wrong tier" })), "void");
        // And the signer is admitted all the same, by a reviewer approving their lead's application.
        var application = await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single());
        await Json(await club.Owner.PostAsync($"/api/applications/{application.Id}/approve", Body(new { })), "a reviewer approves");

        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.MemberId == member.Id && p.Provider == "stripe")))
            .Should().Be(0, "money paid on a withdrawn contract is paid back, never put on an account by itself");
        var deposit = await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single());
        (deposit.Status, deposit.LedgerPaymentId).Should().Be((ContractDepositStatuses.Paid, (Guid?)null));
        (await Json(await club.Owner.GetAsync($"/api/contracts/{contract}"), "the contract")).GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString()).Should().Contain("deposit_to_refund", "staff are told to pay it back");
    }

    [Fact]
    public async Task APaymentArrivingAfterTheContractWasVoided_IsKeptAsPaid_AndStaffAreToldToPayItBack()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());
        // The applicant is admitted by a reviewer meanwhile, and the contract voided while Stripe's page is still open.
        var application = await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single());
        await Json(await club.Owner.PostAsync($"/api/applications/{application.Id}/approve", Body(new { })), "a reviewer approves");
        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/void", Body(new { reason = "Sent by mistake" })), "void");

        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_late", "payment_intent.succeeded", "pi_after_void", "succeeded", 50000, metadata))), "the signer pays on the open page");

        var deposit = await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single());
        (deposit.Status, deposit.ProviderPaymentId, deposit.LedgerPaymentId)
            .Should().Be((ContractDepositStatuses.Paid, "pi_after_void", (Guid?)null), "the money came in, and it is the signer's to get back");
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.Provider == "stripe"))).Should().Be(0);
        (await Json(await club.Owner.GetAsync($"/api/contracts/{contract}"), "the contract")).GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString()).Should().Contain("deposit_to_refund");
    }

    [Fact]
    public async Task ADepositPaidBackInStripeBeforeTheMemberWasAdmitted_IsNotPutOnTheirAccount()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, IntentEvent("evt_p", "payment_intent.succeeded", "pi_back", "succeeded", 50000, metadata))), "paid");
        app.Stripe.On("GET", "/v1/refunds", new
        {
            @object = "list",
            data = new[] { new { id = "re_1", @object = "refund", status = "succeeded", amount = 50000, currency = "chf", payment_intent = "pi_back" } },
            has_more = false,
        });

        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, new
        {
            id = "evt_r", @object = "event", type = "charge.refunded",
            data = new { @object = new { id = "ch_pi_back", @object = "charge", payment_intent = "pi_back", currency = "chf", amount = 50000, amount_refunded = 50000, refunded = true } },
        })), "refunded in Stripe's dashboard");
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().Status)).Should().Be(ContractDepositStatuses.Refunded);

        await Json(await club.Owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count())).Should().Be(0, "money paid back is not put on the account");
    }

    [Fact]
    public async Task AnotherWorkspacesWebhook_CannotPayThisWorkspacesDeposit()
    {
        await using var app = new PaymentsApp();
        var club = await StripeClubAsync(app);
        var (_, _, token) = await SentWithDepositAsync(club.Owner);
        using var anon = app.CreateClient();
        await Json(await SignAsync(anon, token), "sign");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = MetadataOf(app.Stripe.Sent("POST", "/v1/checkout/sessions").Single());
        var (lakeside, lakesideId) = await SignInAsync(app, "owner@lakeside.ch");
        var other = await CreateAsync(app, lakesideId, "Lakeside");
        lakeside.In(other);
        var otherSettings = await Json(await lakeside.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = Secret })), "Lakeside sets Stripe up");
        var otherWebhook = new Uri(otherSettings.GetProperty("webhookUrl").GetString()!).AbsolutePath;

        await Json(await anon.SendAsync(FakeStripe.Webhook(otherWebhook, Secret, IntentEvent("evt_x", "payment_intent.succeeded", "pi_x", "succeeded", 50000, metadata))),
            "Lakeside's endpoint carries Pavillon's deposit");

        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().Status)).Should().Be(ContractDepositStatuses.Awaiting);
    }

    // ── paying by QR-bill ────────────────────────────────────────────────────

    [Fact]
    public async Task WithoutOnlinePayments_TheSignerPaysByQrBill_AndTheBankFilesTransferPaysTheDepositOnce()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await BankFileTests.ClubAsync(app);
        var (_, contract, token) = await SentWithDepositAsync(owner, 350m);
        using var anon = app.CreateClient();

        var page = (await Json(await SignAsync(anon, token), "sign")).GetProperty("deposit");
        page.GetProperty("methods").GetArrayLength().Should().Be(0, "the club takes no payments online");
        page.GetProperty("qrBill").GetBoolean().Should().BeTrue();
        var pdf = await anon.GetAsync($"/api/public/contracts/{token}/deposit/qr-bill?lang=de");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = PdfText.Of(await pdf.Content.ReadAsByteArrayAsync());
        text.Should().Contain("Anzahlung").And.Contain("350.00");
        var reference = await Read(app, db => db.ClubPaymentReferences.IgnoreQueryFilters().Single(r => r.ContractId == contract));
        (reference.Purpose, reference.MemberId).Should().Be(("deposit", (Guid?)null));
        (await Json(await anon.GetAsync($"/api/public/contracts/{token}"), "the page again")).GetProperty("deposit").GetProperty("reference").GetString()
            .Should().NotBeNullOrEmpty("the reference printed is shown for typing into e-banking");

        var file = BankFileTests.Camt054(("DEP-1", 350m, reference.QrReference));
        var import = await Json(await BankFileTests.ImportAsync(owner, file), "import the bank file");
        import.GetProperty("matched").GetInt32().Should().Be(1);
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().Status)).Should().Be(ContractDepositStatuses.Paid);

        await Json(await owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");
        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        var payment = await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single(p => p.MemberId == member.Id));
        (payment.Amount, payment.Provider, payment.Method).Should().Be((350m, "bank", "Wire"));
        (await Read(app, db => db.ClubBankTransactions.IgnoreQueryFilters().Single())).MemberId.Should().Be(member.Id, "the bank file's line learns whose payment it became");

        (await Json(await BankFileTests.ImportAsync(owner, file), "the same file again")).GetProperty("alreadyImported").GetInt32().Should().Be(1);
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.OrganizationId == org && p.ReversesPaymentId == null))).Should().Be(1);
    }
}
