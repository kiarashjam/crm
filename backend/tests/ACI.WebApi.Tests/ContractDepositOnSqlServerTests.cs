using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Services.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// A contract's deposit on a real SQL Server: Stripe's webhook, posted twice, and the club's
/// countersignature all arriving at once, with the migration's unique keys and the ledger's
/// provider key deciding what is recorded.
/// </summary>
/// <remarks>
/// The in-memory provider the HTTP tests run on has no transactions and enforces no unique
/// index. Here the deposit's row, the approval's claim and the ledger's key are what they are
/// in production, so whichever order the three requests run in, one member is admitted and the
/// deposit is on their account once.
/// </remarks>
public class ContractDepositOnSqlServerTests
{
    private const string Secret = "whsec_test_signing_secret";

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task StripesWebhookTwiceAndTheCountersignatureAtOnce_AdmitOneMember_AndRecordTheDepositOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var stripe = new FakeStripe().AcceptingKeys();
            stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
            {
                id = "cs_deposit", @object = "checkout.session", mode = "payment", url = "https://checkout.stripe.com/c/pay/cs_deposit", status = "open",
                expires_at = long.Parse(r.Form["expires_at"]), metadata = new Dictionary<string, string>(),
            }));
            using var app = SqlServerApp.Start(database, services: services =>
                services.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
            var (owner, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Ok(await owner.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            owner.In(org);
            var settings = await Ok(await owner.PutAsync("/api/payment-settings", Workspaces.Body(new
            {
                stripeSecretKey = "sk_test_good_4242424242abcd", stripeWebhookSecret = Secret,
            })), "set Stripe up");
            var webhook = new Uri(settings.GetProperty("webhookUrl").GetString()!).AbsolutePath;

            var lead = (await Ok(await owner.PostAsync("/api/leads", Workspaces.Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead"))
                .GetProperty("id").GetGuid();
            var contract = (await Ok(await owner.PostAsync("/api/contracts/draft", Workspaces.Body(new
            {
                leadId = lead, membershipTier = "Gold", depositAmount = 500m,
                values = new Dictionary<string, string>
                {
                    ["contract.fee"] = "CHF 4,800 a year", ["contract.paymentTerms"] = "annually in advance", ["contract.startDate"] = "1 December 2026",
                    ["contract.term"] = "12 months", ["contract.noticePeriod"] = "three months'", ["contract.jurisdiction"] = "Switzerland",
                },
            })), "draft")).GetProperty("id").GetGuid();
            var token = (await Ok(await owner.PostAsync($"/api/contracts/{contract}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
            using var anon = app.CreateClient();
            await Ok(await anon.PostAsync($"/api/public/contracts/{token}/sign", Workspaces.Body(new { signatureName = "Anna Muster", agreed = true })), "sign");
            await Ok(await anon.PostAsync($"/api/public/contracts/{token}/deposit/checkout", null), "press Pay");
            var metadata = stripe.Sent("POST", "/v1/checkout/sessions").Single().Form
                .Where(f => f.Key.StartsWith("metadata[", StringComparison.Ordinal)).ToDictionary(f => f.Key["metadata[".Length..^1], f => f.Value);
            object Paid(string eventId) => new
            {
                id = eventId, @object = "event", type = "payment_intent.succeeded",
                data = new
                {
                    @object = new
                    {
                        id = "pi_race", @object = "payment_intent", status = "succeeded", amount = 50000, amount_received = 50000, currency = "chf",
                        latest_charge = "ch_race", payment_method_types = new[] { "card" }, metadata,
                    },
                },
            };

            var answers = await Task.WhenAll(
                anon.SendAsync(FakeStripe.Webhook(webhook, Secret, Paid("evt_race_1"))),
                owner.PostAsync($"/api/contracts/{contract}/countersign", Workspaces.Body(new { signatureName = "Club Secretary", agreed = true })),
                anon.SendAsync(FakeStripe.Webhook(webhook, Secret, Paid("evt_race_2"))));

            foreach (var answer in answers) await Ok(answer, "each of the three");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [OrganizationId] = '{org}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [OrganizationId] = '{org}' AND [Provider] = N'stripe'"))
                .Should().Be(1, "the deposit is on the ledger once");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ContractDeposits] WHERE [ContractId] = '{contract}' AND [Status] = N'paid' AND [LedgerPaymentId] IS NOT NULL"))
                .Should().Be(1, "and the deposit knows which payment it became");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubApplications] WHERE [OrganizationId] = '{org}' AND [Status] = N'Approved'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
