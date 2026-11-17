using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Referrals between the CRM and the workspace's portal (Pavillon 46's website and member area).
/// A lead the website sends with who referred them (from its form's referral code, or a member's
/// referral in the portal) shows that person as its "Referred by", as a contact here; and the
/// portal hears who referred someone it welcomes: in the handoff, and when it is recorded here
/// after the handoff, so it can credit that member.
/// </summary>
public class ReferralSyncTests
{
    private const string Portal = "https://portal.example.com/api/integrations/cadence/onboarding";
    private const string Key = "pavillon-website-key";

    private sealed class Receiver : IWebhookTransport
    {
        public readonly List<(Uri Url, string Body, IReadOnlyDictionary<string, string> Headers)> Posts = new();

        public Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            lock (Posts) Posts.Add((url, body, headers));
            return Task.FromResult(new WebhookPostResult(200, null, """{"accountId":"m1","status":"created"}"""));
        }
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly Receiver Hook = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IWebhookTransport>(_ => Hook));
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode? expected = null)
    {
        var body = await res.Content.ReadAsStringAsync();
        if (expected is { } code) res.StatusCode.Should().Be(code, $"{step}: {body}");
        else res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The Pavillon 46 workspace, with the website's key for sending leads, and its portal set up.</summary>
    private static async Task<(HttpClient Owner, HttpClient Website, Guid Org)> WorkspaceAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Organizations.FindAsync(org);
            row!.WebhookApiKeyHash = ACI.Application.Services.OrganizationService.HashApiKey(Key);
            await db.SaveChangesAsync();
        }
        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = Portal, defaultLanguage = "fr" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsync("/api/onboarding/settings/secret", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var website = app.CreateClient();
        website.DefaultRequestHeaders.Add("X-Api-Key", Key);
        return (owner, website, org);
    }

    /// <summary>A lead as the website sends one: from its form with a member's code, or from a member's referral in the portal.</summary>
    private static object WebsiteLead(string name, string email, string? referredBy = "Anna Muster", string? referrerEmail = "anna@pavillon46.ch", string? code = "PAV-7KQ") => new
    {
        name, email, phone = "+41 79 000 00 00", companyName = "", source = "Referral from pavillon46.ch",
        referredBy, referrerEmail, referralCode = code,
    };

    [Fact]
    public async Task ALeadFromTheWebsiteShowsWhoReferredThem_AsTheirContactHere_OrOneMadeForThem()
    {
        await using var app = new App();
        var (owner, website, _) = await WorkspaceAsync(app);

        var lea = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Léa Favre", "lea@example.ch"))), "Léa from the website", HttpStatusCode.Created);
        var anna = lea.GetProperty("referredByContactId").GetGuid();
        var read = await Json(await owner.GetAsync($"/api/leads/{lea.GetProperty("id").GetGuid()}"), "Léa's lead");
        read.GetProperty("referredByContactName").GetString().Should().Be("Anna Muster");
        read.GetProperty("description").GetString().Should().Be("Referred by Anna Muster (anna@pavillon46.ch) · referral code PAV-7KQ");
        var contact = await Json(await owner.GetAsync($"/api/contacts/{anna}"), "the referrer, as a contact");
        contact.GetProperty("email").GetString().Should().Be("anna@pavillon46.ch");

        // Anna's next referral is credited to the same contact, not a second one.
        var marc = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Marc Dubois", "marc@example.ch"))), "Marc from the website", HttpStatusCode.Created);
        marc.GetProperty("referredByContactId").GetGuid().Should().Be(anna);
        (await app.Services.Read(db => db.Contacts.Count(c => c.Email == "anna@pavillon46.ch"))).Should().Be(1);

        // A referrer already a contact here is that contact.
        var hugo = await Json(await owner.PostAsync("/api/contacts", Body(new { name = "Hugo Meyer", email = "hugo@pavillon46.ch" })), "Hugo, a contact");
        var sara = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Sara Blanc", "sara@example.ch", "Hugo M.", "HUGO@pavillon46.ch", "PAV-2QX"))),
            "Sara from the website", HttpStatusCode.Created);
        sara.GetProperty("referredByContactId").GetGuid().Should().Be(hugo.GetProperty("id").GetGuid());

        // Without a referrer, nothing is made up.
        var tom = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Tom Roth", "tom@example.ch", null, null, null))), "Tom, no referrer", HttpStatusCode.Created);
        tom.TryGetProperty("referredByContactId", out var none).Should().BeTrue();
        none.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnOpenLeadThatComesBackWithAReferrer_TakesIt_UnlessItHasOne()
    {
        await using var app = new App();
        var (owner, website, _) = await WorkspaceAsync(app);
        var first = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Léa Favre", "lea@example.ch", null, null, null))), "first, no referrer", HttpStatusCode.Created);
        var id = first.GetProperty("id").GetGuid();

        await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Léa Favre", "lea@example.ch"))), "again, referred by Anna", HttpStatusCode.OK);
        var lead = await Json(await owner.GetAsync($"/api/leads/{id}"), "the lead");
        lead.GetProperty("referredByContactName").GetString().Should().Be("Anna Muster");

        await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Léa Favre", "lea@example.ch", "Hugo Meyer", "hugo@pavillon46.ch"))), "again, by Hugo", HttpStatusCode.OK);
        (await Json(await owner.GetAsync($"/api/leads/{id}"), "the lead")).GetProperty("referredByContactName").GetString()
            .Should().Be("Anna Muster", "the referrer it had is kept");
    }

    [Fact]
    public async Task ThePortalHearsWhoReferredSomeone_InTheHandoff_AndWhenItIsRecordedHereLater()
    {
        await using var app = new App();
        var (owner, website, _) = await WorkspaceAsync(app);

        // Léa, referred by Anna, converted and welcomed: the handoff names Anna.
        var lea = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Léa Favre", "lea@example.ch"))), "Léa", HttpStatusCode.Created);
        var leaDeal = (await Json(await owner.PostAsync($"/api/leads/{lea.GetProperty("id").GetGuid()}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Membership Léa", dealAmount = 4800m })), "convert Léa")).GetProperty("dealId").GetGuid();
        await Json(await owner.PostAsync($"/api/deals/{leaDeal}/onboarding", Body(new { leadId = lea.GetProperty("id").GetGuid(), startUndetermined = true })),
            "welcome Léa", HttpStatusCode.Created);
        await app.DispatchOutboxAsync();
        var handoff = JsonDocument.Parse(app.Hook.Posts.Single().Body).RootElement;
        handoff.GetProperty("eventName").GetString().Should().Be("deal.onboarding");
        var referrer = handoff.GetProperty("payload").GetProperty("referrer");
        (referrer.GetProperty("name").GetString(), referrer.GetProperty("email").GetString()).Should().Be(("Anna Muster", "anna@pavillon46.ch"));

        // Marc, welcomed with no referrer: the handoff says so; recording Hugo afterwards tells the portal.
        var marc = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Marc Dubois", "marc@example.ch", null, null, null))), "Marc", HttpStatusCode.Created);
        var marcLead = marc.GetProperty("id").GetGuid();
        var marcDeal = (await Json(await owner.PostAsync($"/api/leads/{marcLead}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Membership Marc", dealAmount = 4800m })), "convert Marc")).GetProperty("dealId").GetGuid();
        await Json(await owner.PostAsync($"/api/deals/{marcDeal}/onboarding", Body(new { startUndetermined = true })), "welcome Marc", HttpStatusCode.Created);
        await app.DispatchOutboxAsync();
        JsonDocument.Parse(app.Hook.Posts.Last().Body).RootElement.GetProperty("payload").GetProperty("referrer").ValueKind.Should().Be(JsonValueKind.Null);

        var hugo = await Json(await owner.PostAsync("/api/contacts", Body(new { name = "Hugo Meyer", email = "hugo@pavillon46.ch" })), "Hugo");
        await Json(await owner.PutAsync($"/api/leads/{marcLead}/referral", Body(new { contactId = hugo.GetProperty("id").GetGuid() })), "Marc referred by Hugo");
        await app.DispatchOutboxAsync();

        var update = JsonDocument.Parse(app.Hook.Posts.Last().Body).RootElement;
        update.GetProperty("eventName").GetString().Should().Be("member.referrer");
        app.Hook.Posts.Last().Url.ToString().Should().Be(Portal, "it goes where handoffs go");
        app.Hook.Posts.Last().Headers.Should().ContainKey("X-Cadence-Signature-256", "signed, as a handoff is");
        var payload = update.GetProperty("payload");
        payload.GetProperty("contact").GetProperty("email").GetString().Should().Be("marc@example.ch");
        (payload.GetProperty("referrer").GetProperty("name").GetString(), payload.GetProperty("referrer").GetProperty("email").GetString())
            .Should().Be(("Hugo Meyer", "hugo@pavillon46.ch"));

        // Someone never handed over: nothing to tell the portal yet (their handoff will carry it).
        var tom = await Json(await website.PostAsync("/api/webhook/leads", Body(WebsiteLead("Tom Roth", "tom@example.ch", null, null, null))), "Tom", HttpStatusCode.Created);
        var before = app.Hook.Posts.Count;
        await Json(await owner.PutAsync($"/api/leads/{tom.GetProperty("id").GetGuid()}/referral", Body(new { contactId = hugo.GetProperty("id").GetGuid() })), "Tom referred by Hugo");
        await app.DispatchOutboxAsync();
        app.Hook.Posts.Count.Should().Be(before);
    }
}
