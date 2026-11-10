using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Online payments through Stripe, with Stripe played by the test: pay links and Checkout, a
/// card saved on Stripe's page, dues charged to it, refunds through Stripe, and Stripe's
/// webhooks, signed, acted on once by their id, and recorded on the ledger once by Stripe's.
/// </summary>
public class StripePaymentsTests
{
    private const string Key = "sk_test_good_4242424242abcd";
    private const string Secret = "whsec_test_signing_secret";

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

    private sealed record Club(HttpClient Owner, Guid Org, string Webhook);

    private static void PlayStripe(FakeStripe stripe)
    {
        stripe.AcceptingKeys();
        stripe.On("POST", "/v1/customers", r => (HttpStatusCode.OK, new { id = $"cus_{r.Form["metadata[cadence_member]"][..8]}", @object = "customer" }));
        stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
        {
            id = $"cs_test_{r.Form["client_reference_id"][..8]}",
            @object = "checkout.session",
            mode = r.Form["mode"],
            url = $"https://checkout.stripe.com/c/pay/cs_test_{r.Form["client_reference_id"][..8]}",
            status = "open",
            expires_at = long.Parse(r.Form["expires_at"]),
            metadata = new Dictionary<string, string>(),
        }));
    }

    private static async Task<Club> ClubAsync(PaymentsApp app, string email = "owner@pavillon46.ch", bool twint = true)
    {
        PlayStripe(app.Stripe);
        var (client, userId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, userId, "Pavillon 46");
        client.In(org);
        var settings = await Json(await client.PutAsync("/api/payment-settings", Body(new
        {
            stripeSecretKey = Key, stripeWebhookSecret = Secret, stripeOfferTwint = twint,
        })), "set Stripe up");
        return new Club(client, org, new Uri(settings.GetProperty("webhookUrl").GetString()!).AbsolutePath);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    private static async Task<Guid> ChargeAsync(HttpClient client, Guid memberId, decimal amount, string kind = "Dues", string description = "Gold dues") =>
        (await Json(await client.PostAsync("/api/charges", Body(new { memberId, kind, description, amount })), "post a charge")).GetProperty("id").GetGuid();

    private static string TokenOf(string url) => Uri.UnescapeDataString(url[(url.IndexOf("/pay/", StringComparison.Ordinal) + 5)..]);

    /// <summary>The pay page reading what a link asks for: its token in a header, as the page sends it.</summary>
    private static Task<HttpResponseMessage> OpenAsync(HttpClient client, string token) => client.SendAsync(PayRequest(HttpMethod.Get, "/api/public/pay", token));

    /// <summary>The member pressing Pay on the page.</summary>
    private static Task<HttpResponseMessage> PayAsync(HttpClient client, string token) => client.SendAsync(PayRequest(HttpMethod.Post, "/api/public/pay/checkout", token));

    private static HttpRequestMessage PayRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Pay-Token", token);
        return request;
    }

    private static object IntentEvent(string eventId, string type, string intentId, string status, long amountMinor, Dictionary<string, string> metadata,
        string method = "card", string? failure = null) => new
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
                payment_method_types = new[] { method },
                metadata,
                last_payment_error = failure == null ? null : new { code = "card_declined", decline_code = "insufficient_funds", message = failure },
            },
        },
    };

    private static Dictionary<string, string> MetadataOf(FakeStripe.Request request) =>
        request.Form.Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal))
            .ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);

    /// <summary>A member pays their dues on Stripe's page from a pay link; returns the metadata Cadence gave Stripe.</summary>
    private static async Task<(Guid Member, Guid Charge, Dictionary<string, string> Metadata)> PayOnlineAsync(PaymentsApp app, Club club)
    {
        var memberId = await MemberAsync(club.Owner);
        var charge = await ChargeAsync(club.Owner, memberId, 299m);
        var link = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), "make a pay link");
        var anonymous = app.CreateClient();
        await Json(await PayAsync(anonymous, TokenOf(link.GetProperty("url").GetString()!)), "press Pay");
        var checkout = app.Stripe.Sent("POST", "/v1/checkout/sessions").Last();
        return (memberId, charge, MetadataOf(checkout));
    }

    [Fact]
    public async Task WithoutAStripeKeyThereIsNoPayLink()
    {
        await using var app = new PaymentsApp();
        var (client, userId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, userId, "Pavillon 46");
        client.In(org);
        var memberId = await MemberAsync(client);
        await ChargeAsync(client, memberId, 50m);

        (await Refused(await client.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), HttpStatusCode.BadRequest, "a pay link"))
            .Should().Contain("not set up");
        var profile = await Json(await client.GetAsync($"/api/members/{memberId}/payment-profile"), "the payment profile");
        profile.GetProperty("onlinePayments").GetBoolean().Should().BeFalse();
        app.Stripe.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task APayLinkOpensAPageThatSaysWhatIsOwed_AndPayStartsStripesCheckoutForIt()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var memberId = await MemberAsync(club.Owner);
        await ChargeAsync(club.Owner, memberId, 299m);

        var link = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), "make a pay link");
        link.GetProperty("amount").GetDecimal().Should().Be(299m);
        link.GetProperty("currency").GetString().Should().Be("CHF");
        var url = link.GetProperty("url").GetString()!;
        url.Should().StartWith("http://localhost:5173/pay/");
        var token = TokenOf(url);

        var anonymous = app.CreateClient();
        var page = await Json(await OpenAsync(anonymous, token), "open the page");
        page.GetProperty("clubName").GetString().Should().Be("Pavillon 46");
        page.GetProperty("memberFirstName").GetString().Should().Be("Jean");
        page.GetProperty("amount").GetDecimal().Should().Be(299m);
        page.GetProperty("available").GetBoolean().Should().BeTrue();
        page.GetProperty("methods").EnumerateArray().Select(m => m.GetString()).Should().Equal("card", "twint");
        app.Stripe.Sent("POST", "/v1/checkout/sessions").Should().BeEmpty("opening the page changes nothing: mail scanners open every link");

        var started = await Json(await PayAsync(anonymous, token), "press Pay");
        started.GetProperty("url").GetString().Should().StartWith("https://checkout.stripe.com/");
        var customer = app.Stripe.Sent("POST", "/v1/customers").Single();
        customer.IdempotencyKey.Should().StartWith("cadence-customer-");
        customer.Form["email"].Should().Be("jean@example.ch");
        var checkout = app.Stripe.Sent("POST", "/v1/checkout/sessions").Single();
        checkout.Form["mode"].Should().Be("payment");
        checkout.Form["line_items[0][price_data][unit_amount]"].Should().Be("29900", "Stripe counts in cents");
        checkout.Form["line_items[0][price_data][currency]"].Should().Be("chf");
        checkout.Form["payment_method_types[0]"].Should().Be("card");
        checkout.Form["payment_method_types[1]"].Should().Be("twint", "TWINT is offered for francs when the club turned it on");
        checkout.Form["customer"].Should().StartWith("cus_");
        checkout.Form["payment_intent_data[metadata][cadence_payment]"].Should().NotBeNullOrEmpty();
        checkout.Form["success_url"].Should().Contain("/pay/").And.Contain("status=done");
        checkout.IdempotencyKey.Should().StartWith("cadence-checkout-");
        checkout.Authorization.Should().Be($"Bearer {Key}");

        // Pressing Pay again finds the page already made rather than making another.
        await Json(await PayAsync(anonymous, token), "press Pay again");
        app.Stripe.Sent("POST", "/v1/checkout/sessions").Should().HaveCount(1);
        (await OpenAsync(anonymous, "not-a-token")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        url.Should().NotContain("%", "the token is plain base64url, safe in a link as it is");
    }

    [Fact]
    public async Task ALinkForAsManyChargesAsStaffMayTickStillOpens()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var memberId = await MemberAsync(club.Owner);
        var charges = new List<Guid>();
        for (var i = 0; i < 50; i++) charges.Add(await ChargeAsync(club.Owner, memberId, 10m, "Bar", $"Bar tab {i + 1}"));

        var link = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "charges", chargeIds = charges })), "a link for 50 charges");
        link.GetProperty("amount").GetDecimal().Should().Be(500m);
        var token = TokenOf(link.GetProperty("url").GetString()!);
        var page = await Json(await OpenAsync(app.CreateClient(), token), "the member opens it");
        page.GetProperty("amount").GetDecimal().Should().Be(500m);

        charges.Add(await ChargeAsync(club.Owner, memberId, 10m, "Bar", "Bar tab 51"));
        (await Refused(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "charges", chargeIds = charges })), HttpStatusCode.BadRequest,
            "a link for 51 charges")).Should().Contain("50");
    }

    [Fact]
    public async Task APayLinkIsEmailedToTheMemberThroughTheOutbox_AsAServiceMessage()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var memberId = await MemberAsync(club.Owner);
        await ChargeAsync(club.Owner, memberId, 120m);

        var sent = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link/email", Body(new { purpose = "account" })), "email the link");

        sent.GetProperty("emailStatus").GetString().Should().Be("queued");
        var message = await app.Services.Read(db => db.OutboundMessages.IgnoreQueryFilters()
            .Single(m => m.OrganizationId == club.Org && m.Source == OutboundSources.PaymentLink));
        message.To.Should().Contain("jean@example.ch");
        message.Purpose.Should().Be("service", "a member's own account is not marketing");
        message.Body.Should().Contain(sent.GetProperty("url").GetString()).And.Contain("120");
        (await app.Services.Read(db => db.MemberMessages.IgnoreQueryFilters().Count(m => m.MemberId == memberId))).Should().Be(1, "it is in the member's communications");
        var paidUp = await MemberAsync(club.Owner, "Ana");
        (await Refused(await club.Owner.PostAsync($"/api/members/{paidUp}/pay-link/email", Body(new { purpose = "account" })), HttpStatusCode.BadRequest,
            "a link for nothing")).Should().Contain("nothing to pay");
    }

    [Fact]
    public async Task StripesWordThatThePaymentWentThroughRecordsItOnTheLedgerOnce_HoweverOftenItComes()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, charge, metadata) = await PayOnlineAsync(app, club);
        var stripe = app.CreateClient();
        var succeeded = IntentEvent("evt_1", "payment_intent.succeeded", "pi_1", "succeeded", 29900, metadata, "twint");

        (await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, succeeded)), "Stripe posts"))
            .GetProperty("result").GetString().Should().Be("processed");
        var payment = (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).EnumerateArray().Single();
        payment.GetProperty("amount").GetDecimal().Should().Be(299m);
        payment.GetProperty("method").GetString().Should().Be("Twint");
        payment.GetProperty("provider").GetString().Should().Be("stripe");
        payment.GetProperty("providerPaymentId").GetString().Should().Be("pi_1");
        var charges = await Json(await club.Owner.GetAsync($"/api/charges?memberId={memberId}"), "charges");
        charges.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == charge).GetProperty("status").GetString().Should().Be("Paid");
        var online = (await Json(await club.Owner.GetAsync($"/api/online-payments?memberId={memberId}"), "online payments")).EnumerateArray().Single();
        online.GetProperty("status").GetString().Should().Be("succeeded");
        online.GetProperty("ledgerPaymentId").GetGuid().Should().Be(payment.GetProperty("id").GetGuid());

        // The same event again, and the same payment told by another event: nothing twice.
        (await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, succeeded)), "Stripe posts it again"))
            .GetProperty("result").GetString().Should().Be("already processed");
        await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_2", "payment_intent.succeeded", "pi_1", "succeeded", 29900, metadata, "twint"))), "another event for the same payment");
        (await app.Services.Read(db => db.ClubPayments.IgnoreQueryFilters().Count(p => p.OrganizationId == club.Org && p.ProviderPaymentId == "pi_1")))
            .Should().Be(1, "a payment is recorded once, by Stripe's id for it");
        var balance = await Json(await club.Owner.GetAsync($"/api/members/{memberId}/balance"), "balance");
        balance.GetProperty("balance").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task APostWithoutTheRightSignature_OrAnOldOne_OrForNoWorkspace_IsRefused()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, _, metadata) = await PayOnlineAsync(app, club);
        var stripe = app.CreateClient();
        var succeeded = IntentEvent("evt_1", "payment_intent.succeeded", "pi_1", "succeeded", 29900, metadata);

        await Refused(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, "whsec_someone_else", succeeded)), HttpStatusCode.BadRequest, "signed with another secret");
        await Refused(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, succeeded, DateTime.UtcNow.AddMinutes(-30))), HttpStatusCode.BadRequest,
            "an old post played again");
        var unsigned = FakeStripe.Webhook(club.Webhook, Secret, succeeded);
        unsigned.Headers.Remove("Stripe-Signature");
        await Refused(await stripe.SendAsync(unsigned), HttpStatusCode.BadRequest, "no signature");
        await Refused(await stripe.SendAsync(FakeStripe.Webhook($"/api/public/payments/stripe/{Guid.NewGuid():N}", Secret, succeeded)), HttpStatusCode.NotFound,
            "an address that is no workspace's");
        (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).GetArrayLength().Should().Be(0, "nothing was recorded");
    }

    [Fact]
    public async Task AFailedPaymentIsMarkedFailed_WithStripesReason_ForStaffToSee()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, charge, metadata) = await PayOnlineAsync(app, club);

        await Json(await app.CreateClient().SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_1", "payment_intent.payment_failed", "pi_1", "requires_payment_method", 29900, metadata, failure: "Your card has insufficient funds."))),
            "Stripe says it failed");

        var failed = (await Json(await club.Owner.GetAsync("/api/online-payments?status=failed"), "failed payments")).EnumerateArray().Single();
        failed.GetProperty("memberId").GetGuid().Should().Be(memberId);
        failed.GetProperty("failureMessage").GetString().Should().Be("Your card has insufficient funds.");
        failed.GetProperty("failureCode").GetString().Should().Be("insufficient_funds");
        (await Json(await club.Owner.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == charge).GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task ACardSavedOnStripesPageKeepsOnlyStripesIdsAndWhatItShows_AndTheMembersAgreementForTheirDues()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var memberId = await MemberAsync(club.Owner);
        var link = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "setup", autoPay = true })), "a link to save a card");
        var token = TokenOf(link.GetProperty("url").GetString()!);
        var anonymous = app.CreateClient();
        var page = await Json(await OpenAsync(anonymous, token), "open it");
        page.GetProperty("purpose").GetString().Should().Be("setup");
        page.GetProperty("autoPay").GetBoolean().Should().BeTrue();
        await Json(await PayAsync(anonymous, token), "go to Stripe's page");
        var checkout = app.Stripe.Sent("POST", "/v1/checkout/sessions").Single();
        checkout.Form["mode"].Should().Be("setup");
        checkout.Form["custom_text[submit][message]"].Should().Contain("membership dues", "the member agrees on Stripe's page, under these words");
        checkout.Form.Keys.Should().NotContain(k => k.StartsWith("line_items", StringComparison.Ordinal));
        var metadata = MetadataOf(checkout);
        metadata["cadence_autopay"].Should().Be("true");

        app.Stripe.On("GET", "/v1/setup_intents/seti_1", new
        {
            id = "seti_1", @object = "setup_intent", status = "succeeded", customer = "cus_saved", metadata,
            payment_method = new { id = "pm_1", @object = "payment_method", type = "card", card = new { brand = "visa", last4 = "4242", exp_month = 12, exp_year = 2030 } },
        });
        await Json(await app.CreateClient().SendAsync(FakeStripe.Webhook(club.Webhook, Secret, new
        {
            id = "evt_setup", @object = "event", type = "checkout.session.completed",
            data = new { @object = new { id = "cs_x", @object = "checkout.session", mode = "setup", status = "complete", setup_intent = "seti_1", metadata } },
        })), "Stripe says the card is saved");

        var profile = await Json(await club.Owner.GetAsync($"/api/members/{memberId}/payment-profile"), "the profile");
        var card = profile.GetProperty("card");
        card.GetProperty("brand").GetString().Should().Be("visa");
        card.GetProperty("last4").GetString().Should().Be("4242");
        card.GetProperty("expMonth").GetInt32().Should().Be(12);
        profile.GetProperty("autoPayDues").GetBoolean().Should().BeTrue();
        profile.GetProperty("autoPayAgreedBy").GetString().Should().Contain("member");
        var stored = await app.Services.Read(db => db.ClubPaymentProfiles.IgnoreQueryFilters().Single(p => p.MemberId == memberId));
        stored.PaymentMethodId.Should().Be("pm_1");
        stored.CustomerId.Should().Be("cus_saved");

        app.Stripe.On("POST", "/v1/payment_methods/pm_1/detach", new { id = "pm_1", @object = "payment_method" });
        var removed = await Json(await club.Owner.DeleteAsync($"/api/members/{memberId}/card"), "remove the card");
        removed.TryGetProperty("card", out _).Should().BeFalse();
        removed.GetProperty("autoPayDues").GetBoolean().Should().BeFalse("nothing is charged to a card that is gone");
        app.Stripe.Sent("POST", "/v1/payment_methods/pm_1/detach").Should().HaveCount(1, "it is removed at Stripe too");
    }

    private static async Task<(Guid Member, Dictionary<string, string> Metadata)> SavedCardAsync(PaymentsApp app, Club club, bool autoPay)
    {
        var memberId = await MemberAsync(club.Owner);
        var metadata = new Dictionary<string, string> { ["cadence_org"] = club.Org.ToString(), ["cadence_member"] = memberId.ToString() };
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.ClubPaymentProfiles.Add(new ClubPaymentProfile
            {
                Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = memberId, CustomerId = "cus_1", PaymentMethodId = "pm_1", PaymentMethodType = "card",
                Brand = "visa", Last4 = "4242", SavedAtUtc = DateTime.UtcNow.AddMinutes(-5), AutoPayDues = autoPay,
                AutoPayAgreedAtUtc = autoPay ? DateTime.UtcNow.AddMinutes(-5) : null, AutoPayAgreedBy = autoPay ? "The member, on Stripe's card page" : null,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        return (memberId, metadata);
    }

    [Fact]
    public async Task DuesAreChargedToTheCardOfAMemberWhoAgreed_OncePerCharge()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, _) = await SavedCardAsync(app, club, autoPay: true);
        var (otherId, _) = await SavedCardAsync(app, club, autoPay: false);
        var dues = await ChargeAsync(club.Owner, memberId, 299m);
        await ChargeAsync(club.Owner, memberId, 40m, "Bar", "Bar tab");
        await ChargeAsync(club.Owner, otherId, 299m);
        app.Stripe.On("POST", "/v1/payment_intents", r => (HttpStatusCode.OK, new
        {
            id = "pi_auto", @object = "payment_intent", status = "succeeded", amount = long.Parse(r.Form["amount"]), amount_received = long.Parse(r.Form["amount"]),
            currency = "chf", latest_charge = "ch_auto", payment_method_types = new[] { "card" }, metadata = MetadataOf(r),
        }));

        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow)).Should().Be(1, "only the dues of the member who agreed");

        var sent = app.Stripe.Sent("POST", "/v1/payment_intents").Single();
        sent.Form["amount"].Should().Be("29900");
        sent.Form["off_session"].Should().Be("true");
        sent.Form["confirm"].Should().Be("true");
        sent.Form["customer"].Should().Be("cus_1");
        sent.Form["payment_method"].Should().Be("pm_1");
        sent.IdempotencyKey.Should().StartWith("cadence-charge-");
        var payment = (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).EnumerateArray().Single();
        payment.GetProperty("method").GetString().Should().Be("CardOnFile");
        payment.GetProperty("providerPaymentId").GetString().Should().Be("pi_auto");
        (await Json(await club.Owner.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == dues).GetProperty("status").GetString().Should().Be("Paid");

        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow)).Should().Be(0, "a charge is charged once");
        app.Stripe.Sent("POST", "/v1/payment_intents").Should().HaveCount(1);
    }

    [Fact]
    public async Task TheCardIsNeverChargedForWhatTheMembersCreditAlreadyCovers()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        // Credit is spent on charges by hand (AppliedCredit), so dues can stand open on an
        // account in credit: one member prepaid more than the dues, another part of them.
        var (prepaid, _) = await SavedCardAsync(app, club, autoPay: true);
        var (partly, _) = await SavedCardAsync(app, club, autoPay: true);
        await Json(await club.Owner.PutAsync($"/api/members/{prepaid}", Body(new { houseAccountBalance = 500m })), "credit 500, the year paid ahead");
        await Json(await club.Owner.PutAsync($"/api/members/{partly}", Body(new { houseAccountBalance = 100m })), "credit 100, overpaid last month");
        await ChargeAsync(club.Owner, prepaid, 299m);
        var partlyDues = await ChargeAsync(club.Owner, partly, 299m);
        app.Stripe.On("POST", "/v1/payment_intents", r => (HttpStatusCode.OK, new
        {
            id = $"pi_{r.Form["metadata[cadence_member]"][..8]}", @object = "payment_intent", status = "succeeded", amount = long.Parse(r.Form["amount"]),
            amount_received = long.Parse(r.Form["amount"]), currency = "chf", payment_method_types = new[] { "card" }, metadata = MetadataOf(r),
        }));

        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow);

        var sent = app.Stripe.Sent("POST", "/v1/payment_intents");
        sent.Should().ContainSingle("the prepaid member's credit covers their dues").Which.Form["amount"].Should().Be("19900", "299 of dues less 100 of credit");
        sent.Single().Form["metadata[cadence_member]"].Should().Be(partly.ToString());
        (await Json(await club.Owner.GetAsync($"/api/members/{prepaid}/balance"), "the prepaid balance")).GetProperty("balance").GetDecimal()
            .Should().Be(201m, "still in credit, and nothing charged to the card");
        (await Json(await club.Owner.GetAsync($"/api/members/{partly}/balance"), "the other balance")).GetProperty("balance").GetDecimal()
            .Should().Be(0m, "the credit and the card pay the dues between them");

        // Staff charging the card for everything open take no more than the account owes either.
        (await Refused(await club.Owner.PostAsync($"/api/members/{prepaid}/card/charge", Body(new { })), HttpStatusCode.BadRequest,
            "charge a card the credit covers")).Should().Contain("nothing to pay");
        await ChargeAsync(club.Owner, prepaid, 250m, "Dining", "Dinner");
        var charged = await Json(await club.Owner.PostAsync($"/api/members/{prepaid}/card/charge", Body(new { })), "charge what is owed");
        charged.GetProperty("amount").GetDecimal().Should().Be(49m, "549 open less 500 of credit");
        app.Stripe.Sent("POST", "/v1/payment_intents").Last().Form["amount"].Should().Be("4900");
        // The other member's dues show what the card paid; their credit stays for staff to apply.
        (await Json(await club.Owner.GetAsync($"/api/charges?memberId={partly}"), "the other member's charges")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == partlyDues).GetProperty("paidAmount").GetDecimal().Should().Be(199m);
    }

    [Fact]
    public async Task ADeclinedCardIsMarkedFailedAndNotTriedAgainOnItsOwn_AndStaffCanChargeTheCardThemselves()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, _) = await SavedCardAsync(app, club, autoPay: true);
        var dues = await ChargeAsync(club.Owner, memberId, 299m);
        var declined = true;
        app.Stripe.On("POST", "/v1/payment_intents", r => declined
            ? ((HttpStatusCode)402, new
            {
                error = new
                {
                    type = "card_error", code = "card_declined", decline_code = "insufficient_funds", message = "Your card has insufficient funds.",
                    payment_intent = new { id = "pi_declined", @object = "payment_intent", status = "requires_payment_method", amount = 29900, currency = "chf", metadata = MetadataOf(r) },
                },
            })
            : (HttpStatusCode.OK, new
            {
                id = "pi_manual", @object = "payment_intent", status = "succeeded", amount = 29900, amount_received = 29900, currency = "chf",
                payment_method_types = new[] { "card" }, metadata = MetadataOf(r),
            }));

        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow);
        var failed = (await Json(await club.Owner.GetAsync("/api/online-payments?status=failed"), "failed")).EnumerateArray().Single();
        failed.GetProperty("failureMessage").GetString().Should().Be("Your card has insufficient funds.");
        failed.GetProperty("purpose").GetString().Should().Be("autopay");
        (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).GetArrayLength().Should().Be(0);
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow);
        app.Stripe.Sent("POST", "/v1/payment_intents").Should().HaveCount(1, "a declined card is not charged again without someone asking");

        declined = false;
        var charged = await Json(await club.Owner.PostAsync($"/api/members/{memberId}/card/charge", Body(new { chargeIds = new[] { dues } })), "staff charge the card");
        charged.GetProperty("status").GetString().Should().Be("succeeded");
        (await Json(await club.Owner.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == dues).GetProperty("status").GetString().Should().Be("Paid");
    }

    [Fact]
    public async Task ARefundGoesThroughStripe_AndIsRecordedOnce_AsIsOneMadeInStripesDashboard()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var (memberId, _, metadata) = await PayOnlineAsync(app, club);
        var stripe = app.CreateClient();
        await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret,
            IntentEvent("evt_1", "payment_intent.succeeded", "pi_1", "succeeded", 29900, metadata))), "paid");
        var paymentId = (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).EnumerateArray().Single().GetProperty("id").GetGuid();
        app.Stripe.On("POST", "/v1/refunds", r => (HttpStatusCode.OK, new
        {
            id = "re_1", @object = "refund", status = "succeeded", amount = long.Parse(r.Form["amount"]), currency = "chf", payment_intent = r.Form["payment_intent"],
        }));

        var refunded = await Json(await club.Owner.PostAsync($"/api/payments/{paymentId}/refund", Body(new { amount = 50, reason = "Charged twice for a guest" })), "refund 50");
        refunded.GetProperty("status").GetString().Should().Be("PartiallyRefunded");
        var refund = refunded.GetProperty("refunds").EnumerateArray().Single();
        refund.GetProperty("provider").GetString().Should().Be("stripe");
        refund.GetProperty("providerRefundId").GetString().Should().Be("re_1");
        var sent = app.Stripe.Sent("POST", "/v1/refunds").Single();
        sent.Form["payment_intent"].Should().Be("pi_1");
        sent.Form["amount"].Should().Be("5000");
        sent.IdempotencyKey.Should().StartWith("cadence-refund-");

        // Stripe then reports that refund, and one made in its own dashboard. Since API version
        // 2022-11-15 a charge in an event no longer lists its refunds (and a webhook cannot ask for
        // them), so the charge says only that it was refunded; Stripe lists the refunds when asked.
        var atStripe = new List<(string Id, long Amount)>();
        app.Stripe.On("GET", "/v1/refunds", r => (HttpStatusCode.OK, new
        {
            @object = "list",
            data = atStripe.Where(_ => r.Query.Contains("payment_intent=pi_1", StringComparison.Ordinal))
                .Select(x => new { id = x.Id, @object = "refund", status = "succeeded", amount = x.Amount, currency = "chf", payment_intent = "pi_1" }).ToArray(),
            has_more = false,
        }));
        object Refunded(long amountRefunded) => new
        {
            id = $"evt_refund_{amountRefunded}", @object = "event", type = "charge.refunded",
            data = new
            {
                @object = new
                {
                    id = "ch_pi_1", @object = "charge", payment_intent = "pi_1", currency = "chf", amount = 29900, amount_refunded = amountRefunded, refunded = false,
                },
            },
        };
        atStripe.Add(("re_1", 5000));
        await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, Refunded(5000))), "Stripe reports the refund");
        atStripe.Add(("re_2", 1000));
        await Json(await stripe.SendAsync(FakeStripe.Webhook(club.Webhook, Secret, Refunded(6000))), "and one made in its dashboard");

        var payment = (await Json(await club.Owner.GetAsync($"/api/payments?memberId={memberId}"), "payments")).EnumerateArray().Single();
        payment.GetProperty("refundedAmount").GetDecimal().Should().Be(60m, "50 asked for here, 10 made in Stripe, each once");
        payment.GetProperty("refunds").GetArrayLength().Should().Be(2);
        var balance = await Json(await club.Owner.GetAsync($"/api/members/{memberId}/balance"), "balance");
        balance.GetProperty("balance").GetDecimal().Should().Be(-60m, "the dues owe again what was paid back");
        app.Stripe.Sent("GET", "/v1/refunds").Should().NotBeEmpty()
            .And.OnlyContain(r => r.Query.Contains("payment_intent=pi_1", StringComparison.Ordinal), "the payment's refunds are read from Stripe");
    }

    [Fact]
    public async Task GoingLiveForgetsTheCardsAndCustomersOfStripesTestMode_SoMembersCanPayWithTheLiveKey()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        // The club tried things out in test mode: a member saved a test card, dues on it, and a
        // pay link was left open.
        var (tester, _) = await SavedCardAsync(app, club, autoPay: true);
        await ChargeAsync(club.Owner, tester, 299m);
        var link = await Json(await club.Owner.PostAsync($"/api/members/{tester}/pay-link", Body(new { purpose = "account" })), "a pay link in test mode");
        await Json(await PayAsync(app.CreateClient(), TokenOf(link.GetProperty("url").GetString()!)), "press Pay in test mode");

        // Stripe accepts the live key too; then the club pastes it.
        app.Stripe.On("GET", "/v1/customers", _ => (HttpStatusCode.OK, new { @object = "list", data = Array.Empty<object>() }));
        await Json(await club.Owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = "sk_live_good_4242424242abcd" })), "go live");

        var profile = await Json(await club.Owner.GetAsync($"/api/members/{tester}/payment-profile"), "the member's profile");
        profile.TryGetProperty("card", out _).Should().BeFalse("a card saved in test mode cannot be charged with the live key");
        profile.GetProperty("autoPayDues").GetBoolean().Should().BeFalse("nothing is charged to it");
        (await app.Services.Read(db => db.ClubPaymentProfiles.IgnoreQueryFilters().Single(p => p.MemberId == tester).CustomerId)).Should().BeNull();
        profile.GetProperty("payments").EnumerateArray().Should().NotContain(p => p.GetProperty("status").GetString() == "open",
            "a test page left open will never be paid");

        // The member's next payment is made with a live customer, not the test one Stripe's live
        // mode has never heard of.
        var customers = app.Stripe.Sent("POST", "/v1/customers").Count;
        await Json(await PayAsync(app.CreateClient(), TokenOf(link.GetProperty("url").GetString()!)), "press Pay after going live");
        app.Stripe.Sent("POST", "/v1/customers").Should().HaveCount(customers + 1);
        var checkout = app.Stripe.Sent("POST", "/v1/checkout/sessions").Last();
        checkout.Authorization.Should().Be("Bearer sk_live_good_4242424242abcd");
        checkout.Form["customer"].Should().NotBe("cus_1");

        // A live card saved since stays when the club rolls its live key: only test mode's go.
        var (liveMember, _) = await SavedCardAsync(app, club, autoPay: true);
        await Json(await club.Owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = "sk_live_good_rolled_9999abcd" })), "roll the live key");
        var kept = await Json(await club.Owner.GetAsync($"/api/members/{liveMember}/payment-profile"), "the live member's profile");
        kept.GetProperty("card").GetProperty("last4").GetString().Should().Be("4242");
        kept.GetProperty("autoPayDues").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task OnlyMoneyPeopleSendPayLinksOrChargeCards_AndOnlyThoseWhoVoidRefund()
    {
        await using var app = new PaymentsApp();
        var club = await ClubAsync(app);
        var memberId = await MemberAsync(club.Owner);
        await ChargeAsync(club.Owner, memberId, 80m);
        async Task<HttpClient> JoinAsync(string email, string preset)
        {
            var (client, userId) = await SignInAsync(app, email);
            await AddMemberAsync(app, club.Org, userId, OrgMemberRole.Member);
            await Json(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{userId}/permissions", Body(new { preset })), $"make {preset}");
            return client.In(club.Org);
        }
        var desk = await JoinAsync("desk@pavillon46.ch", "front_desk");
        var finance = await JoinAsync("finance@pavillon46.ch", "finance");

        await Refused(await desk.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), HttpStatusCode.Forbidden, "the desk sends a pay link");
        await Refused(await desk.PostAsync($"/api/members/{memberId}/card/charge", Body(new { })), HttpStatusCode.Forbidden, "the desk charges a card");
        await Refused(await desk.DeleteAsync($"/api/members/{memberId}/card"), HttpStatusCode.Forbidden, "the desk removes a card");
        await Json(await desk.GetAsync($"/api/members/{memberId}/payment-profile"), "the desk sees whether a card is saved");
        await Json(await finance.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), "finance sends a pay link");
    }

    [Fact]
    public async Task AnotherWorkspaceSeesNothing_AndItsWebhookCannotSettleThisWorkspacesPayments()
    {
        await using var app = new PaymentsApp();
        var alpine = await ClubAsync(app, "owner@alpine.example");
        var (memberId, _, metadata) = await PayOnlineAsync(app, alpine);
        var lakeside = await ClubAsync(app, "owner@lakeside.example");

        await Refused(await lakeside.Owner.GetAsync($"/api/members/{memberId}/payment-profile"), HttpStatusCode.NotFound, "Lakeside reads Alpine's member");
        (await Json(await lakeside.Owner.GetAsync("/api/online-payments"), "Lakeside's online payments")).GetArrayLength().Should().Be(0);
        await Refused(await lakeside.Owner.PostAsync($"/api/members/{memberId}/pay-link", Body(new { purpose = "account" })), HttpStatusCode.NotFound,
            "Lakeside makes a link to Alpine's member");

        // Lakeside's own webhook, rightly signed, naming Alpine's payment: not Lakeside's to settle.
        var outcome = await Json(await app.CreateClient().SendAsync(FakeStripe.Webhook(lakeside.Webhook, Secret,
            IntentEvent("evt_x", "payment_intent.succeeded", "pi_x", "succeeded", 29900, metadata))), "Lakeside's post");
        outcome.GetProperty("result").GetString().Should().Be("ignored");
        (await Json(await alpine.Owner.GetAsync($"/api/payments?memberId={memberId}"), "Alpine's payments")).GetArrayLength().Should().Be(0);
    }
}
