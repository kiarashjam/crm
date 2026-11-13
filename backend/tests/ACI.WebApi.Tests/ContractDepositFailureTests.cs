using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Services.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What happens when putting a contract's deposit on an account fails (the database is gone
/// for a moment, say) after the countersignature, the approval or the void it follows is
/// saved: the signature, the approval and the void stand and answer as done, and Stripe's next
/// word on the payment puts the deposit on the account.
/// </summary>
public class ContractDepositFailureTests
{
    private const string Key = "sk_test_good_4242424242abcd";
    private const string Secret = "whsec_test_signing_secret";

    /// <summary>The payments app, with the deposit service failing on demand when called from outside.</summary>
    private sealed class FailingDepositsApp : PaymentsApp
    {
        public bool Failing { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddScoped<IContractDeposits>(sp =>
                new FailingDeposits(ActivatorUtilities.CreateInstance<ContractDepositService>(sp), this)));
        }
    }

    /// <summary>The real deposit service, whose settling and closing throw while the app says so.</summary>
    private sealed class FailingDeposits(IContractDeposits inner, FailingDepositsApp app) : IContractDeposits
    {
        private void ThrowIfFailing()
        {
            if (app.Failing) throw new InvalidOperationException("The database went away for a moment.");
        }

        public Task<Result<DepositAsk>> CheckAskAsync(Guid organizationId, Guid userId, decimal amount, CancellationToken ct = default) =>
            inner.CheckAskAsync(organizationId, userId, amount, ct);

        public Task OpenAsync(Contract contract, CancellationToken ct = default) => inner.OpenAsync(contract, ct);

        public Task CloseAsync(Contract contract, CancellationToken ct = default)
        {
            ThrowIfFailing();
            return inner.CloseAsync(contract, ct);
        }

        public Task<IReadOnlyDictionary<Guid, ContractDepositDto>> ForContractsAsync(Guid organizationId, IReadOnlyCollection<Guid> contractIds, CancellationToken ct = default) =>
            inner.ForContractsAsync(organizationId, contractIds, ct);

        public Task<PublicDepositDto?> PublicAsync(Contract contract, CancellationToken ct = default) => inner.PublicAsync(contract, ct);

        public Task<Result<CheckoutStartedDto>> StartCheckoutAsync(Contract contract, string signingPageUrl, CancellationToken ct = default) =>
            inner.StartCheckoutAsync(contract, signingPageUrl, ct);

        public Task<Result<ContractDocument>> QrBillAsync(Contract contract, string? language, CancellationToken ct = default) =>
            inner.QrBillAsync(contract, language, ct);

        public Task<string?> HandleStripeEventAsync(Guid organizationId, ProviderEvent stripeEvent, CancellationToken ct = default) =>
            inner.HandleStripeEventAsync(organizationId, stripeEvent, ct);

        public Task<string?> RecordProviderRefundAsync(Guid organizationId, string paymentIntentId, IReadOnlyList<ProviderRefund> refunds, CancellationToken ct = default) =>
            inner.RecordProviderRefundAsync(organizationId, paymentIntentId, refunds, ct);

        public Task<DepositTransferOutcome> RecordBankTransferAsync(Guid organizationId, Guid contractId, DepositTransfer transfer, CancellationToken ct = default) =>
            inner.RecordBankTransferAsync(organizationId, contractId, transfer, ct);

        public Task<DepositSettlement?> SettleAsync(Guid organizationId, Guid contractId, Guid? userId, CancellationToken ct = default)
        {
            ThrowIfFailing();
            return inner.SettleAsync(organizationId, contractId, userId, ct);
        }

        public Task SettleForApplicationAsync(Guid organizationId, Guid applicationId, Guid? userId, CancellationToken ct = default)
        {
            ThrowIfFailing();
            return inner.SettleForApplicationAsync(organizationId, applicationId, userId, ct);
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<T> Read<T>(CustomWebApplicationFactory app, Func<ACI.Infrastructure.Persistence.AppDbContext, T> read) => app.Services.Read(read);

    /// <summary>A club on Stripe, and Anna's lead with an open Gold application and her Gold contract asking 500, sent.</summary>
    private static async Task<(HttpClient Owner, string Webhook, Guid Application, Guid Contract, string Token)> SentAsync(FailingDepositsApp app)
    {
        app.Stripe.AcceptingKeys();
        app.Stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
        {
            id = "cs_test_failing", @object = "checkout.session", mode = r.Form["mode"], url = "https://checkout.stripe.com/c/pay/cs_test_failing",
            status = "open", expires_at = long.Parse(r.Form["expires_at"]), metadata = new Dictionary<string, string>(),
        }));
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var settings = await Json(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = Secret })), "set Stripe up");
        var lead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();
        var application = (await Json(await owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead")).GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/applications/{application}", Body(new { requestedTier = "Gold" })), "pick the tier");
        var contract = (await Json(await owner.PostAsync("/api/contracts/draft", Body(new
        {
            leadId = lead,
            membershipTier = "Gold",
            depositAmount = 500m,
            values = new Dictionary<string, string>
            {
                ["contract.fee"] = "CHF 4,800 a year", ["contract.paymentTerms"] = "annually in advance", ["contract.startDate"] = "1 December 2026",
                ["contract.term"] = "12 months", ["contract.noticePeriod"] = "three months'", ["contract.jurisdiction"] = "Switzerland",
            },
        })), "draft")).GetProperty("id").GetGuid();
        var token = (await Json(await owner.PostAsync($"/api/contracts/{contract}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        return (owner, new Uri(settings.GetProperty("webhookUrl").GetString()!).AbsolutePath, application, contract, token);
    }

    private static object Paid(string eventId, Dictionary<string, string> metadata) => new
    {
        id = eventId, @object = "event", type = "payment_intent.succeeded",
        data = new
        {
            @object = new
            {
                id = "pi_flaky", @object = "payment_intent", status = "succeeded", amount = 50000, amount_received = 50000, currency = "chf",
                latest_charge = "ch_flaky", payment_method_types = new[] { "card" }, metadata,
            },
        },
    };

    [Fact]
    public async Task ACountersignatureStands_WhenItsDepositCannotBePutOnTheAccount_AndStripesNextWordPutsItThere()
    {
        await using var app = new FailingDepositsApp();
        var (owner, webhook, _, contract, token) = await SentAsync(app);
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "On contract.countersigned", trigger = "contract.countersigned",
            actions = new object[] { new { type = "notify", config = new { message = "countersigned" } } },
        })), "a rule on the countersignature");
        using var anon = app.CreateClient();
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Anna Muster", agreed = true })), "Anna signs");
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
        var metadata = app.Stripe.Sent("POST", "/v1/checkout/sessions").Single().Form
            .Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal)).ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);
        await Json(await anon.SendAsync(FakeStripe.Webhook(webhook, Secret, Paid("evt_flaky_1", metadata))), "Stripe says it is paid");

        app.Failing = true;
        var executed = await Json(await owner.PostAsync($"/api/contracts/{contract}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })),
            "countersign while the deposit cannot be put on the account");
        app.Failing = false;

        executed.GetProperty("status").GetString().Should().Be(ContractStatuses.Countersigned);
        var member = await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single());
        (await Read(app, db => db.Notifications.IgnoreQueryFilters().Count(n => n.Title == "On contract.countersigned")))
            .Should().Be(1, "what follows the deposit (the copies, the workspace's rules) still happens");
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().LedgerPaymentId)).Should().BeNull();

        // Stripe posts another event about the same payment: the deposit goes on the account now, once.
        await Json(await anon.SendAsync(FakeStripe.Webhook(webhook, Secret, Paid("evt_flaky_2", metadata))), "Stripe's next word");
        await Json(await anon.SendAsync(FakeStripe.Webhook(webhook, Secret, Paid("evt_flaky_3", metadata))), "and another");

        var payment = await Read(app, db => db.ClubPayments.IgnoreQueryFilters().Single(p => p.ReversesPaymentId == null));
        (payment.MemberId, payment.Amount, payment.ProviderPaymentId).Should().Be((member.Id, 500m, "pi_flaky"));
        (await Read(app, db => db.ContractDeposits.IgnoreQueryFilters().Single().LedgerPaymentId)).Should().Be(payment.Id);
    }

    [Fact]
    public async Task AnApprovalAndAVoidStand_WhenTheirDepositCannotBeSettledOrClosed()
    {
        await using var app = new FailingDepositsApp();
        var (owner, _, application, contract, _) = await SentAsync(app);
        app.Failing = true;

        var approved = await Json(await owner.PostAsync($"/api/applications/{application}/approve", Body(new { })), "approve");
        await Json(await owner.PostAsync($"/api/contracts/{contract}/void", Body(new { reason = "Sent by mistake" })), "void");

        approved.GetProperty("memberId").GetGuid().Should().Be(await Read(app, db => db.ClubMembers.IgnoreQueryFilters().Single().Id));
        (await Read(app, db => db.Contracts.IgnoreQueryFilters().Single().Status)).Should().Be(ContractStatuses.Voided);
    }
}
