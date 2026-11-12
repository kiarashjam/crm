using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The workspace's payment, bank and accounting settings: each value checked as a QR-bill or
/// Stripe will need it, the Stripe secrets kept out of every answer, and only those who manage
/// settings changing them.
/// </summary>
public class PaymentSettingsTests
{
    private const string Key = "sk_test_good_4242424242abcd";

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

    private static async Task<(HttpClient Client, Guid Org)> ClubAsync(PaymentsApp app, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, userId, "Pavillon 46");
        return (client.In(org), org);
    }

    private static object Bank(string? iban = "CH5800791123000889012", string? qrIban = null) => new
    {
        iban, qrIban,
        creditorName = "Pavillon 46", creditorStreet = "Rue du Lac", creditorBuildingNumber = "1268",
        creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
    };

    [Fact]
    public async Task NothingIsSetUpUntilTheClubSetsItUp()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await ClubAsync(app);

        var status = await Json(await owner.GetAsync("/api/payment-settings/status"), "status");
        status.GetProperty("onlinePayments").GetBoolean().Should().BeFalse();
        status.GetProperty("qrBills").GetBoolean().Should().BeFalse();
        status.GetProperty("methods").GetArrayLength().Should().Be(0);

        var settings = await Json(await owner.GetAsync("/api/payment-settings"), "settings");
        settings.GetProperty("stripeConfigured").GetBoolean().Should().BeFalse();
        settings.GetProperty("qrBillReady").GetBoolean().Should().BeFalse();
        settings.GetProperty("qrBillProblem").GetString().Should().Contain("IBAN");
        settings.GetProperty("accounts").GetProperty("bank").GetString().Should().Be("1020", "the Swiss SME chart is the default");
        settings.GetProperty("accounts").GetProperty("revenueByKind").GetProperty("Dues").GetString().Should().Be("3400");
        app.Stripe.Requests.Should().BeEmpty("nothing talks to Stripe until a key is set");
    }

    [Fact]
    public async Task BankDetailsAreCheckedAsAQrBillNeedsThem()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await ClubAsync(app);

        (await Refused(await owner.PutAsync("/api/payment-settings", Body(Bank("CH5800791123000889013"))), HttpStatusCode.BadRequest, "bad check digits"))
            .Should().Contain("check its digits");
        (await Refused(await owner.PutAsync("/api/payment-settings", Body(Bank("CH4431999123000889012"))), HttpStatusCode.BadRequest, "QR-IBAN as IBAN"))
            .Should().Contain("enter it as the QR-IBAN");
        (await Refused(await owner.PutAsync("/api/payment-settings", Body(Bank(null, "CH5800791123000889012"))), HttpStatusCode.BadRequest, "IBAN as QR-IBAN"))
            .Should().Contain("30000 to 31999");
        (await Refused(await owner.PutAsync("/api/payment-settings", Body(new { iban = "CH5800791123000889012", creditorName = "Pavillon 46" })),
            HttpStatusCode.BadRequest, "no town")).Should().Contain("postal code");

        var saved = await Json(await owner.PutAsync("/api/payment-settings", Body(Bank())), "save the IBAN");
        saved.GetProperty("iban").GetString().Should().Be("CH5800791123000889012");
        saved.GetProperty("qrBillReady").GetBoolean().Should().BeTrue();
        var status = await Json(await owner.GetAsync("/api/payment-settings/status"), "status");
        status.GetProperty("qrBills").GetBoolean().Should().BeTrue();
        status.GetProperty("qrReferenceType").GetString().Should().Be("SCOR", "a plain IBAN takes a creditor reference");

        await Json(await owner.PutAsync("/api/payment-settings", Body(Bank("CH58 0079 1123 0008 8901 2", "CH44 3199 9123 0008 8901 2"))), "add a QR-IBAN");
        status = await Json(await owner.GetAsync("/api/payment-settings/status"), "status");
        status.GetProperty("qrReferenceType").GetString().Should().Be("QRR", "a QR-IBAN takes a QR reference, before the IBAN");
    }

    [Fact]
    public async Task AStripeKeyIsTriedWithStripe_AndNeitherItNorTheWebhookSecretIsEverSentBack()
    {
        await using var app = new PaymentsApp();
        app.Stripe.AcceptingKeys();
        var (owner, _) = await ClubAsync(app);

        (await Refused(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = "pk_test_publishable_123456789" })),
            HttpStatusCode.BadRequest, "publishable key")).Should().Contain("publishable");
        (await Refused(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = "sk_test_wrong_123456789012" })),
            HttpStatusCode.BadRequest, "a key Stripe refuses")).Should().Contain("Stripe did not accept the key");
        (await Refused(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = "not-a-secret" })),
            HttpStatusCode.BadRequest, "a webhook secret that is not one")).Should().Contain("whsec_");

        var res = await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key, stripeWebhookSecret = "whsec_test_secret_value", stripeOfferTwint = true }));
        var text = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, text);
        text.Should().NotContain(Key).And.NotContain("whsec_test_secret_value");
        var saved = JsonDocument.Parse(text).RootElement;
        saved.GetProperty("stripeConfigured").GetBoolean().Should().BeTrue();
        saved.GetProperty("stripeKeyHint").GetString().Should().Be("sk_test_…abcd");
        saved.GetProperty("stripeMode").GetString().Should().Be("test");
        saved.GetProperty("stripeWebhookSecretSet").GetBoolean().Should().BeTrue();
        saved.GetProperty("webhookUrl").GetString().Should().Contain("/api/public/payments/stripe/");
        saved.GetProperty("webhookEvents").EnumerateArray().Select(e => e.GetString()).Should().Contain("payment_intent.succeeded");

        var read = await owner.GetStringAsync("/api/payment-settings");
        read.Should().NotContain(Key).And.NotContain("whsec_test_secret_value");
        var status = await Json(await owner.GetAsync("/api/payment-settings/status"), "status");
        status.GetProperty("onlinePayments").GetBoolean().Should().BeTrue();
        status.GetProperty("mode").GetString().Should().Be("test");
        status.GetProperty("methods").EnumerateArray().Select(m => m.GetString()).Should().Equal("card", "twint");
        status.GetProperty("webhookConfigured").GetBoolean().Should().BeTrue();

        // Saving again without the key keeps it; sending it empty takes it away.
        await Json(await owner.PutAsync("/api/payment-settings", Body(new { stripeOfferTwint = false })), "save without the key");
        (await Json(await owner.GetAsync("/api/payment-settings/status"), "status")).GetProperty("onlinePayments").GetBoolean().Should().BeTrue();
        await Json(await owner.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = "" })), "take the key away");
        (await Json(await owner.GetAsync("/api/payment-settings/status"), "status")).GetProperty("onlinePayments").GetBoolean().Should().BeFalse();
        app.Stripe.Sent("GET", "/v1/customers").Should().OnlyContain(r => r.Version == "2024-06-20", "every call names the API version it is written for");
    }

    [Fact]
    public async Task TheAccountsDefaultToTheSwissSmeChart_AndTheClubsOwnAreChecked()
    {
        await using var app = new PaymentsApp();
        var (owner, _) = await ClubAsync(app);

        (await Refused(await owner.PutAsync("/api/payment-settings", Body(new { accounts = new { bank = "10 20" } })), HttpStatusCode.BadRequest, "an account with a space"))
            .Should().Contain("bank account");
        var saved = await Json(await owner.PutAsync("/api/payment-settings", Body(new
        {
            accounts = new { giftCardLiability = "2035", revenueByKind = new Dictionary<string, string> { ["Bar"] = "3210" }, vatCodes = new Dictionary<string, string> { ["8.1"] = "UN81" } },
        })), "own accounts");
        var accounts = saved.GetProperty("accounts");
        accounts.GetProperty("giftCardLiability").GetString().Should().Be("2035");
        accounts.GetProperty("revenueByKind").GetProperty("Bar").GetString().Should().Be("3210");
        accounts.GetProperty("revenueByKind").GetProperty("Dues").GetString().Should().Be("3400", "an account not sent keeps the chart's");
        accounts.GetProperty("vatCodes").GetProperty("8.1").GetString().Should().Be("UN81");
        accounts.GetProperty("customerPrepayments").GetString().Should().Be("2030");
    }

    [Fact]
    public async Task OnlyThoseWhoManageSettingsSeeOrChangeThem_ButAnyMemberSeesWhatIsSetUp()
    {
        await using var app = new PaymentsApp();
        var (owner, org) = await ClubAsync(app);
        var (finance, financeId) = await SignInAsync(app, "finance@pavillon46.ch");
        await AddMemberAsync(app, org, financeId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{financeId}/permissions", Body(new { preset = "finance" })), "make finance");
        finance.In(org);

        await Refused(await finance.GetAsync("/api/payment-settings"), HttpStatusCode.Forbidden, "finance reads the settings");
        await Refused(await finance.PutAsync("/api/payment-settings", Body(Bank())), HttpStatusCode.Forbidden, "finance changes the settings");
        await Json(await finance.GetAsync("/api/payment-settings/status"), "finance sees what is set up");
    }

    [Fact]
    public async Task OneWorkspacesSettingsAreNotAnothers()
    {
        await using var app = new PaymentsApp();
        app.Stripe.AcceptingKeys();
        var (alpine, _) = await ClubAsync(app, "owner@alpine.example");
        var (lakeside, _) = await ClubAsync(app, "owner@lakeside.example");
        await Json(await alpine.PutAsync("/api/payment-settings", Body(new { stripeSecretKey = Key })), "Alpine sets Stripe up");
        await Json(await alpine.PutAsync("/api/payment-settings", Body(Bank())), "Alpine sets its bank up");

        var status = await Json(await lakeside.GetAsync("/api/payment-settings/status"), "Lakeside's status");
        status.GetProperty("onlinePayments").GetBoolean().Should().BeFalse();
        status.GetProperty("qrBills").GetBoolean().Should().BeFalse();
        var settings = await Json(await lakeside.GetAsync("/api/payment-settings"), "Lakeside's settings");
        settings.TryGetProperty("iban", out _).Should().BeFalse();
    }
}
