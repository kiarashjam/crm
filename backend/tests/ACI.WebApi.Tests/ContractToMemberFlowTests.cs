using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The whole way from a deal to a member: the deal's membership contract is sent, signed and its
/// deposit paid on the signing page, and the club's countersignature wins the deal, admits the
/// signer once (onto the member a welcome handoff already made, when it made one), puts the
/// deposit on their account and sets off the workspace's rules, each once, however often the
/// countersignature is retried or Stripe posts.
/// </summary>
public class ContractToMemberFlowTests
{
    private const string Key = "sk_test_good_4242424242abcd";
    private const string Secret = "whsec_test_signing_secret";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<T> Read<T>(CustomWebApplicationFactory app, Func<ACI.Infrastructure.Persistence.AppDbContext, T> read) => app.Services.Read(read);

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org, string Webhook, Guid Pipeline, Guid Proposal, Guid Signed);

    /// <summary>A club taking payments through Stripe, with a pipeline whose last stage is won.</summary>
    private static async Task<Club> ClubAsync(PaymentsApp app)
    {
        app.Stripe.AcceptingKeys();
        app.Stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
        {
            id = $"cs_test_{r.Form["client_reference_id"][..8]}_{r.IdempotencyKey![^1..]}",
            @object = "checkout.session",
            mode = r.Form["mode"],
            url = $"https://checkout.stripe.com/c/pay/cs_test_{r.Form["client_reference_id"][..8]}",
            status = "open",
            expires_at = long.Parse(r.Form["expires_at"]),
            metadata = new Dictionary<string, string>(),
        }));
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var settings = await Json(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = Secret, stripeOfferTwint = true })), "set Stripe up");
        var pipeline = (await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), "add the pipeline")).GetProperty("id").GetGuid();
        async Task<Guid> Stage(string name, int order, bool isWon = false) =>
            (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId = pipeline, name, displayOrder = order, isWon })), $"add {name}")).GetProperty("id").GetGuid();
        var proposal = await Stage("Proposal", 1);
        var signed = await Stage("Signed", 2, isWon: true);
        return new Club(owner, ownerId, org, new Uri(settings.GetProperty("webhookUrl").GetString()!).AbsolutePath, pipeline, proposal, signed);
    }

    /// <summary>Anna's lead, converted to a contact and a deal in the Proposal stage.</summary>
    private static async Task<(Guid Lead, Guid Contact, Guid Deal)> DealAsync(Club club, string email = "anna@example.ch")
    {
        var lead = (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email, status = "New" })), "lead")).GetProperty("id").GetGuid();
        var converted = await Json(await club.Owner.PostAsync($"/api/leads/{lead}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Gold membership Anna", dealAmount = 4800m })), "convert");
        var deal = converted.GetProperty("dealId").GetGuid();
        await Json(await club.Owner.PutAsync($"/api/deals/{deal}", Body(new { dealStageId = club.Proposal })), "to Proposal");
        return (lead, converted.GetProperty("contactId").GetGuid(), deal);
    }

    /// <summary>The deal's Gold membership contract asking a deposit, sent and signed: its id and signing token.</summary>
    private static async Task<(Guid Contract, string Token)> SignedOnDealAsync(PaymentsApp app, HttpClient drafter, Guid deal, decimal? deposit = 500m)
    {
        var draft = await Json(await drafter.PostAsync("/api/contracts/draft", Body(new
        {
            dealId = deal,
            membershipTier = "Gold",
            depositAmount = deposit,
            values = new Dictionary<string, string>
            {
                ["contract.paymentTerms"] = "annually in advance", ["contract.startDate"] = "1 December 2026", ["contract.term"] = "12 months",
                ["contract.noticePeriod"] = "three months'", ["contract.jurisdiction"] = "Switzerland",
            },
        })), "draft on the deal");
        var id = draft.GetProperty("id").GetGuid();
        var token = (await Json(await drafter.PostAsync($"/api/contracts/{id}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        using var anon = app.CreateClient();
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Anna Muster", agreed = true })), "Anna signs");
        return (id, token);
    }

    /// <summary>Anna pays the deposit on Stripe's page, and Stripe says so: the event, to post again.</summary>
    private static async Task<object> PayAsync(PaymentsApp app, Club club, string token, string intent = "pi_anna")
    {
        using var anon = app.CreateClient();
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = app.Stripe.Sent("POST", "/v1/checkout/sessions").Last().Form
            .Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal)).ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);
        var paid = Event($"evt_{intent}", intent, metadata);
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, paid)), "Stripe says it is paid");
        return paid;
    }

    private static object Event(string eventId, string intent, Dictionary<string, string> metadata) => new
    {
        id = eventId,
        @object = "event",
        type = "payment_intent.succeeded",
        data = new
        {
            @object = new
            {
                id = intent, @object = "payment_intent", status = "succeeded", amount = 50000, amount_received = 50000, currency = "chf",
                latest_charge = $"ch_{intent}", payment_method_types = new[] { "card" }, metadata,
            },
        },
    };

    private static Task<HttpResponseMessage> CountersignAsync(HttpClient client, Guid contract) =>
        client.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true }));

    private static async Task RulesAsync(HttpClient owner, params string[] triggers)
    {
        foreach (var trigger in triggers)
            await Json(await owner.PostAsync("/api/automations", Body(new
            {
                name = $"On {trigger}", trigger, actions = new object[] { new { type = "notify", config = new { message = trigger } } },
            })), $"a rule on {trigger}");
    }

    private static Task<int> FiredAsync(PaymentsApp app, string trigger) => Read(app, db => db.Notifications.IgnoreQueryFilters().Count(n => n.Title == $"On {trigger}"));

    // ── the flow ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountersigningADealsMembershipContract_WinsTheDeal_AdmitsTheSignerOnce_AndPutsTheirDepositOnTheirAccount()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        await RulesAsync(club.Owner, "contract.countersigned", "deal.won", "member.created", "application.approved");
        var (lead, contact, deal) = await DealAsync(club);
        var (contract, token) = await SignedOnDealAsync(app, club.Owner, deal);
        var paid = await PayAsync(app, club, token);
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(0, "paid and signed, but the club has not said yes");

        var executed = await Json(await CountersignAsync(club.Owner, contract), "countersign");

        // The deal is won, moved to the pipeline's won stage, and the move is in its history.
        var won = await Json(await club.Owner.GetAsync($"/api/deals/{deal}"), "the deal");
        (won.GetProperty("isWon").GetBoolean(), won.GetProperty("dealStageId").GetGuid()).Should().Be((true, club.Signed));
        var history = (await Json(await club.Owner.GetAsync($"/api/deals/{deal}/stage-history"), "its history")).EnumerateArray().ToList();
        // Newest first: the move to Signed, made by whoever countersigned.
        history.Select(h => h.GetProperty("toStageName").GetString()).Should().ContainInOrder("Signed", "Proposal");
        history[0].GetProperty("changedByName").GetString().Should().Be("owner@pavillon46.ch");

        // One member: Anna, at the tier she signed for, linked to the lead, contact and deal she came from.
        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        (member.FirstName, member.LastName, member.Tier, member.Status).Should().Be(("Anna", "Muster", "Gold", "Active"));
        (member.LeadId, member.ContactId, member.DealId).Should().Be(((Guid?)lead, (Guid?)contact, (Guid?)deal));
        var application = await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single());
        (application.Source, application.Status, application.ContractId, application.MemberId, application.RequestedTier)
            .Should().Be((ApplicationSources.Contract, ApplicationStatuses.Approved, (Guid?)contract, (Guid?)member.Id, "Gold"));

        // The deposit is on her account, paying the initiation fee first.
        var payment = await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single());
        (payment.MemberId, payment.Amount, payment.ProviderPaymentId).Should().Be((member.Id, 500m, "pi_anna"));
        (await Read(app, db => db.ClubPaymentAllocations.IgnoreQueryFilters().Single().ChargeId)).Should().Be(application.InitiationChargeId!.Value);
        executed.GetProperty("deposit").GetProperty("memberId").GetGuid().Should().Be(member.Id);
        executed.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("type").GetString())
            .Should().Contain(["countersigned", "deal_won", "member_admitted", "deposit_recorded"]);
        (executed.GetProperty("dealId").GetGuid()).Should().Be(deal);

        foreach (var trigger in new[] { "contract.countersigned", "deal.won", "member.created", "application.approved" })
            (await FiredAsync(app, trigger)).Should().Be(1, $"{trigger} fires once");

        // Retried: the countersignature, Stripe's post, and another event about the same payment.
        await Json(await CountersignAsync(club.Owner, contract), "countersign again", HttpStatusCode.BadRequest);
        using var anon = app.CreateClient();
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, paid)), "Stripe posts again");
        var metadata = app.Stripe.Sent("POST", "/v1/checkout/sessions").Last().Form
            .Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal)).ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);
        await Json(await anon.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, Event("evt_other", "pi_anna", metadata))), "another event about the payment");

        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(1);
        (await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Count())).Should().Be(1);
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Count())).Should().Be(1, "the deposit is on the ledger once");
        (await Read(app, db => db.ClubCharges.IgnoreQueryFilters().Count(c => c.Id == application.InitiationChargeId))).Should().Be(1);
        (await Json(await club.Owner.GetAsync($"/api/deals/{deal}/stage-history"), "its history")).GetArrayLength().Should().Be(history.Count);
        foreach (var trigger in new[] { "contract.countersigned", "deal.won", "member.created", "application.approved" })
            (await FiredAsync(app, trigger)).Should().Be(1, $"{trigger} still fired once");
    }

    [Fact]
    public async Task ADealHandedOverFirst_IsCountersignedOntoTheMemberTheHandoffMade_AndItsDepositPaidAfterwardsGoesOnTheirAccount()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, contact, deal) = await DealAsync(club);
        await HandOverAsync(club, deal, lead);
        var handedOver = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        (handedOver.Status, handedOver.DealId).Should().Be(("Pending", (Guid?)deal));

        var (contract, token) = await SignedOnDealAsync(app, club.Owner, deal);
        await Json(await CountersignAsync(club.Owner, contract), "countersign before the deposit is paid");

        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        member.Id.Should().Be(handedOver.Id, "the member the handoff made is the one admitted, not a second one");
        (member.Status, member.Tier, member.ContactId, member.LeadId).Should().Be(("Active", "Gold", (Guid?)contact, (Guid?)lead));
        (await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single().MemberId)).Should().Be(member.Id);
        (await Read(app, db => db.DealOnboardings.IgnoreQueryFilters().Single().ClubMemberId)).Should().Be(member.Id);
        (await Read(app, db => db.ClubSubscriptions.IgnoreQueryFilters().Count(s => s.MemberId == member.Id))).Should().Be(1, "her Gold dues started");

        // Stripe's word comes after the countersignature.
        await PayAsync(app, club, token, "pi_late");
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single())).Should().Match<ClubPayment>(p => p.MemberId == member.Id && p.ProviderPaymentId == "pi_late");
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(1);
    }

    [Fact]
    public async Task AReviewersApproval_AlsoAdmitsTheMemberAHandoffMade_WhateverAddressTheApplicationCarries()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, contact, deal) = await DealAsync(club);
        // The contact's address changed before the handoff: the member it makes has the new one.
        await Json(await club.Owner.PutAsync($"/api/contacts/{contact}", Body(new { email = "anna.muster@work.ch" })), "new address");
        await HandOverAsync(club, deal, lead);
        var handedOver = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        handedOver.Email.Should().Be("anna.muster@work.ch");

        // The application, started from the lead, carries the lead's old address.
        var application = await Json(await club.Owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead");
        application.GetProperty("email").GetString().Should().Be("anna@example.ch");
        var id = application.GetProperty("id").GetGuid();
        await Json(await club.Owner.PutAsync($"/api/applications/{id}", Body(new { requestedTier = "Gold" })), "pick the tier");
        var approved = await Json(await club.Owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve");

        approved.GetProperty("memberId").GetGuid().Should().Be(handedOver.Id);
        approved.GetProperty("memberCreated").GetBoolean().Should().BeFalse();
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(1);
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single().Status)).Should().Be("Active");
    }

    [Fact]
    public async Task ASalesRepsCountersignature_WinsTheirPrivateDeal_StartsTheApplicationAtTheSignedTier_AndTheReviewersApprovalTakesTheDeposit()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, _, deal) = await DealAsync(club);
        var (contract, token) = await SignedOnDealAsync(app, club.Owner, deal);
        await PayAsync(app, club, token);
        // The owner's deal, made private: the rep who countersigns may not open it.
        await Json(await club.Owner.PutAsync($"/api/deals/{deal}", Body(new { isPrivate = true })), "make the deal private");
        var (rep, repId) = await SignInAsync(app, "rep@pavillon46.ch");
        await AddMemberAsync(app, club.Org, repId, OrgMemberRole.Member);
        await Json(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{repId}/permissions", Body(new { preset = "sales_rep" })), "a sales rep");
        rep.In(club.Org);
        (await rep.GetAsync($"/api/deals/{deal}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the rep may not open the deal");

        await Json(await CountersignAsync(rep, contract), "the rep countersigns");

        // The signature wins the deal all the same: an executed contract is the workspace's fact.
        var won = await Json(await club.Owner.GetAsync($"/api/deals/{deal}"), "the deal");
        (won.GetProperty("isWon").GetBoolean(), won.GetProperty("dealStageId").GetGuid()).Should().Be((true, club.Signed));
        // Nobody is admitted by a rep, but the club is asked: an application at the signed tier, for a reviewer.
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(0);
        var application = await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single());
        (application.Status, application.RequestedTier, application.ContractId, application.LeadId, application.Source)
            .Should().Be((ApplicationStatuses.Submitted, "Gold", (Guid?)contract, (Guid?)lead, ApplicationSources.Contract));
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().LedgerPaymentId)).Should().BeNull("held until someone is admitted");

        var approved = await Json(await club.Owner.PostAsync($"/api/applications/{application.Id}/approve", Body(new { })), "a reviewer approves");

        var memberId = approved.GetProperty("memberId").GetGuid();
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single())).Should().Match<ClubMember>(m => m.Id == memberId && m.Tier == "Gold" && m.DealId == deal);
        (await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single())).Should().Match<ClubPayment>(p => p.MemberId == memberId && p.Amount == 500m);
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().MemberId)).Should().Be(memberId);
    }

    [Fact]
    public async Task AnApplicationAskingForAnotherTier_IsAdmittedAtTheTierTheContractWasSignedFor()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, _, deal) = await DealAsync(club);
        var application = (await Json(await club.Owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply")).GetProperty("id").GetGuid();
        await Json(await club.Owner.PutAsync($"/api/applications/{application}", Body(new { requestedTier = "Silver" })), "Silver asked");
        var (contract, _) = await SignedOnDealAsync(app, club.Owner, deal, deposit: null);

        await Json(await CountersignAsync(club.Owner, contract), "countersign the Gold contract");

        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single().Tier)).Should().Be("Gold");
        (await Read(app, db => db.ClubApplications.IgnoreQueryFilters().Single()))
            .Should().Match<ClubApplication>(a => a.Id == application && a.Status == ApplicationStatuses.Approved && a.ContractId == contract && a.RequestedTier == "Gold");
    }

    [Fact]
    public async Task AHandoffAfterTheCountersignature_LinksTheMemberItAdmitted_EvenAtANewAddress()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, contact, deal) = await DealAsync(club);
        var (contract, _) = await SignedOnDealAsync(app, club.Owner, deal, deposit: null);
        await Json(await CountersignAsync(club.Owner, contract), "countersign");
        var admitted = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        await Json(await club.Owner.PutAsync($"/api/contacts/{contact}", Body(new { email = "anna.muster@work.ch" })), "new address");

        await HandOverAsync(club, deal, lead);

        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Count())).Should().Be(1, "the handoff links the member the contract admitted");
        (await Read(app, db => db.DealOnboardings.IgnoreQueryFilters().Single().ClubMemberId)).Should().Be(admitted.Id);
        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single().Status)).Should().Be("Active", "a handoff does not make a member pending again");
    }

    [Fact]
    public async Task MergingADealIntoAnother_TakesItsMemberAndHandoffAlong()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (lead, _, deal) = await DealAsync(club);
        await HandOverAsync(club, deal, lead);
        var keep = (await Json(await club.Owner.PostAsync("/api/deals", Body(new { name = "Anna (duplicate)", amount = 4800m, currency = "CHF" })), "a second deal"))
            .GetProperty("id").GetGuid();

        await Json(await club.Owner.PostAsync($"/api/deals/{keep}/merge", Body(new { otherDealId = deal })), "merge the handed-over deal into it", HttpStatusCode.NoContent);

        (await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single().DealId)).Should().Be(keep, "the member's page links to the deal kept");
        (await Read(app, db => db.DealOnboardings.IgnoreQueryFilters().Single().DealId)).Should().Be(keep);
    }

    private static async Task HandOverAsync(Club club, Guid deal, Guid lead)
    {
        await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = "https://portal.example.com/api/integrations/cadence/onboarding", defaultLanguage = "en" })), "set the portal up");
        await Json(await club.Owner.PostAsync("/api/onboarding/settings/secret", null), "its secret");
        await Json(await club.Owner.PostAsync($"/api/deals/{deal}/onboarding", Body(new { leadId = lead, startUndetermined = true, language = "en", addToClubMembers = true })),
            "hand the deal over", HttpStatusCode.Created);
    }
}
