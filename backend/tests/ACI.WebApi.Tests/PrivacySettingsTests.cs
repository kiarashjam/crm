using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace's privacy settings, the marketing checkbox on its public forms, and what the
/// public privacy page is told.
/// </summary>
/// <remarks>
/// The capture form recorded no consent at all, and the privacy page named a placeholder
/// company in San Francisco, under California law, and no processor.
/// </remarks>
public class PrivacySettingsTests
{
    private const string FormToken = "fedcba9876543210fedcba9876543210";

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step, HttpStatusCode status = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid Org, Guid Owner)> ClubAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, owner) = await SignInAsync(factory, "olga@pavillon46.ch");
        var org = await CreateAsync(factory, owner, "Pavillon46");
        client.In(org);
        await using var db = factory.Services.EveryWorkspace();
        db.LeadCaptureForms.Add(new LeadCaptureForm
        {
            Id = Guid.NewGuid(), OrganizationId = org, CreatedByUserId = owner, Name = "Website", PublicToken = FormToken,
            IsActive = true, CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (factory, client, org, owner);
    }

    private static object Settings(string consent = "optional", string? text = null, int? leads = null, int? members = null, int? mail = null, int? details = null) => new
    {
        controllerName = "Pavillon 46 SA",
        controllerAddress = "Rue du Lac 46, 1006 Lausanne, Switzerland",
        controllerEmail = "privacy@pavillon46.ch",
        dpoContact = (string?)null,
        marketingConsent = consent,
        marketingConsentText = text,
        leadRetentionMonths = leads,
        memberRetentionYears = members,
        mailRetentionMonths = mail,
        detailRetentionMonths = details,
    };

    [Fact]
    public async Task SettingsStartOff_AreSavedWithTheirVersion_AndAStaleSaveIsRefused()
    {
        var (factory, client, _, _) = await ClubAsync();
        using var _ = factory;

        var start = await Ok(await client.GetAsync("/api/privacy/settings"), "read the defaults");
        start.GetProperty("marketingConsent").GetString().Should().Be("optional");
        start.GetProperty("marketingConsentTextIsDefault").GetBoolean().Should().BeTrue();
        foreach (var period in new[] { "leadRetentionMonths", "memberRetentionYears", "mailRetentionMonths", "detailRetentionMonths" })
            start.GetProperty(period).ValueKind.Should().Be(JsonValueKind.Null, $"{period} is off until a workspace sets it");

        var saved = await client.PutAsync("/api/privacy/settings", Body(Settings(leads: 24, members: 10)));
        var body = await Ok(saved, "save");
        body.GetProperty("controllerName").GetString().Should().Be("Pavillon 46 SA");
        body.GetProperty("leadRetentionMonths").GetInt32().Should().Be(24);
        body.GetProperty("updatedByName").GetString().Should().Be("olga@pavillon46.ch");
        var etag = saved.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/privacy/settings") { Content = Body(Settings(leads: 12)) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        await Ok(await client.SendAsync(request), "save with the version read");
        var stale = new HttpRequestMessage(HttpMethod.Put, "/api/privacy/settings") { Content = Body(Settings(leads: 6)) };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);
        (await client.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.Conflict, "someone saved since that version was read");
    }

    [Fact]
    public async Task BadSettingsAreRefused_AndOnlyWhoeverHandlesPrivacyMayChangeThem()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        (await client.PutAsync("/api/privacy/settings", Body(Settings(consent: "always")))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsync("/api/privacy/settings", Body(Settings(leads: 0)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsync("/api/privacy/settings", Body(Settings(members: 31)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (rep, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        (await rep.PutAsync("/api/privacy/settings", Body(Settings()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rep.PostAsync("/api/privacy/retention/run", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheCaptureFormAsksForMarketingConsent_AndRecordsTheAnswerOnTheLead()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        using var visitor = factory.CreateClient();

        var form = await Ok(await visitor.GetAsync($"/api/public/capture/{FormToken}"), "open the form");
        form.GetProperty("marketingConsent").GetString().Should().Be("optional", "a workspace that set nothing asks, and does not require it");
        form.GetProperty("marketingConsentText").GetString().Should().Contain("news, events and offers");

        await Ok(await visitor.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Lena Weber", email = "Lena@Example.ch", marketingConsent = true })), "a yes", HttpStatusCode.Created);
        await Ok(await visitor.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Tom Berg", email = "tom@example.ch" })), "no answer", HttpStatusCode.Created);

        var records = await factory.Services.Read(db => db.ConsentRecords.Where(c => c.OrganizationId == org).ToList());
        var leads = await factory.Services.Read(db => db.Leads.Where(l => l.OrganizationId == org).ToDictionary(l => l.Email.ToLower(), l => l.Id));
        var lena = records.Single(c => c.SubjectId == leads["lena@example.ch"]);
        (lena.SubjectType, lena.Channel, lena.Purpose, lena.Granted, lena.Source, lena.Address, lena.IsCurrent)
            .Should().Be((ConsentSubjects.Lead, ConsentChannels.Email, ConsentPurposes.Marketing, true, ConsentSources.Form, "lena@example.ch", true));
        lena.Detail.Should().Contain("Website").And.Contain("news, events and offers", "the words they were shown are kept");
        records.Single(c => c.SubjectId == leads["tom@example.ch"]).Granted.Should().BeFalse("a box left empty is recorded as a no");
    }

    [Fact]
    public async Task AWorkspaceCanRequireTheBox_OrNotAskAtAll()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        using var visitor = factory.CreateClient();

        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(consent: "required", text: "Sign me up for the newsletter."))), "require it");
        var required = await Ok(await visitor.GetAsync($"/api/public/capture/{FormToken}"), "open the form");
        (required.GetProperty("marketingConsent").GetString(), required.GetProperty("marketingConsentText").GetString())
            .Should().Be(("required", "Sign me up for the newsletter."));
        var refused = await visitor.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Lena Weber", email = "lena@example.ch" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.Services.Read(db => db.Leads.Count(l => l.OrganizationId == org))).Should().Be(0, "nothing is kept half-agreed");
        await Ok(await visitor.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Lena Weber", email = "lena@example.ch", marketingConsent = true })), "ticked", HttpStatusCode.Created);

        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(consent: "off"))), "do not ask");
        var off = await Ok(await visitor.GetAsync($"/api/public/capture/{FormToken}"), "open the form");
        off.GetProperty("marketingConsent").GetString().Should().Be("off");
        await Ok(await visitor.PostAsync($"/api/public/capture/{FormToken}", Body(new { name = "Tom Berg", email = "tom@example.ch" })), "submit", HttpStatusCode.Created);
        var tom = await factory.Services.Read(db => db.Leads.Single(l => l.Email == "tom@example.ch").Id);
        (await factory.Services.Read(db => db.ConsentRecords.Any(c => c.SubjectId == tom))).Should().BeFalse("a form that does not ask records nothing");
    }

    [Fact]
    public async Task TheApplicationFormFollowsTheSameSetting()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        var form = await Ok(await client.PutAsync("/api/applications/form", Body(new { isActive = true })), "set up the application form");
        var token = form.GetProperty("publicToken").GetString();
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(consent: "required"))), "require it");
        using var visitor = factory.CreateClient();

        (await Ok(await visitor.GetAsync($"/api/public/apply/{token}"), "open")).GetProperty("marketingConsent").GetString().Should().Be("required");
        var refused = await visitor.PostAsync($"/api/public/apply/{token}", Body(new { firstName = "Lena", lastName = "Weber", email = "lena@example.ch" }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.Services.Read(db => db.ClubApplications.Count(a => a.OrganizationId == org))).Should().Be(0);

        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(consent: "off"))), "do not ask");
        await Ok(await visitor.PostAsync($"/api/public/apply/{token}", Body(new { firstName = "Lena", lastName = "Weber", email = "lena@example.ch" })), "apply", HttpStatusCode.Created);
        (await factory.Services.Read(db => db.ConsentRecords.Count(c => c.OrganizationId == org && c.SubjectType == ConsentSubjects.Application))).Should().Be(0);
    }

    [Fact]
    public async Task ThePrivacyPageIsToldWhoAnswersForTheData_TheProcessors_AndTheRetentionPeriods()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        using var anyone = factory.CreateClient();

        // Without a form: the operator of this Cadence, from the server's configuration, which
        // the test host sets none of. It is left empty rather than invented.
        var plain = await Ok(await anyone.GetAsync("/api/public/privacy"), "the page on its own");
        plain.GetProperty("operator").GetProperty("name").ValueKind.Should().Be(JsonValueKind.Null);
        plain.GetProperty("hosting").GetString().Should().Contain("Azure");
        var processors = plain.GetProperty("processors").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        processors.Should().Contain("Microsoft (Azure)");
        processors.Should().NotContain("OpenAI", "the test host's key is not a real one, so nothing goes to OpenAI and the page says nothing of it");
        processors.Should().NotContain("Stripe", "no workspace is named");
        plain.GetProperty("sessionHours").GetInt32().Should().Be(24);

        // With a real OpenAI key configured, OpenAI is named, with what it is sent and what not.
        using (var withAi = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                   c.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "sk-test-not-real" }))))
        {
            var ai = await Ok(await withAi.CreateClient().GetAsync("/api/public/privacy"), "the page with AI drafting on");
            var openAi = ai.GetProperty("processors").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "OpenAI");
            openAi.GetProperty("location").GetString().Should().Be("United States");
            openAi.GetProperty("purpose").GetString().Should().Contain("email address does not")
                .And.Contain("questions in the assistant", "the assistant sends the questions and the records it reads on the same key");
        }
        // With the assistant turned off on the server, only the AI writer sends anything.
        using (var writerOnly = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                   c.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "sk-test-not-real", ["Assistant:Enabled"] = "false" }))))
        {
            var ai = await Ok(await writerOnly.CreateClient().GetAsync("/api/public/privacy"), "the page with only the AI writer on");
            ai.GetProperty("processors").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "OpenAI")
                .GetProperty("purpose").GetString().Should().NotContain("assistant");
        }
        plain.GetProperty("retention").ValueKind.Should().Be(JsonValueKind.Null);

        // From the workspace's form: the workspace answers for its data, with its own periods.
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(leads: 18, mail: 24))), "save");
        await using (var db = factory.Services.EveryWorkspace())
        {
            db.ClubPaymentSettings.Add(new ClubPaymentSettings { Id = Guid.NewGuid(), OrganizationId = org, StripeSecretKeyProtected = "protected", WebhookKey = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var notice = await Ok(await anyone.GetAsync($"/api/public/privacy?form={FormToken}"), "the page from the form");
        notice.GetProperty("workspaceName").GetString().Should().Be("Pavillon46");
        notice.GetProperty("controller").GetProperty("name").GetString().Should().Be("Pavillon 46 SA");
        notice.GetProperty("controller").GetProperty("email").GetString().Should().Be("privacy@pavillon46.ch");
        notice.GetProperty("retention").GetProperty("leadMonths").GetInt32().Should().Be(18);
        notice.GetProperty("retention").GetProperty("mailMonths").GetInt32().Should().Be(24);
        notice.GetProperty("retention").GetProperty("memberYears").ValueKind.Should().Be(JsonValueKind.Null);
        notice.GetProperty("processors").EnumerateArray().Select(p => p.GetProperty("name").GetString()).Should().Contain("Stripe", "this club takes card payments");

        (await Ok(await anyone.GetAsync("/api/public/privacy?form=not-a-form"), "an unknown form")).GetProperty("workspaceName").ValueKind
            .Should().Be(JsonValueKind.Null, "an unknown token names no workspace and reveals nothing");
    }

    [Fact]
    public async Task TheNoticeNamesWhatTheClubConnectedItself_AndPrintsTheWordsItsApplicationFormShows()
    {
        var (factory, client, org, _) = await ClubAsync();
        using var _ = factory;
        using var anyone = factory.CreateClient();
        await Ok(await client.PutAsync("/api/privacy/settings", Body(Settings(text: "Send me news about events."))), "save");

        var before = await Ok(await anyone.GetAsync($"/api/public/privacy?form={FormToken}"), "nothing connected");
        before.GetProperty("processors").EnumerateArray().Select(p => p.GetProperty("name").GetString())
            .Should().NotContain(n => n!.Contains("connected") || n.Contains("linked"));

        // Its webhook gets an event for every new lead, with the lead's name and address, and its
        // API key lets a linked system (an AI assistant through MCP, say) read the records.
        await using (var db = factory.Services.EveryWorkspace())
        {
            var workspace = await db.Organizations.SingleAsync(o => o.Id == org);
            workspace.OutboundWebhookUrl = "https://hooks.example.com/cadence/abc123";
            workspace.McpApiKeyHash = "hash-of-a-key";
            await db.SaveChangesAsync();
        }
        var notice = await Ok(await anyone.GetAsync($"/api/public/privacy?form={FormToken}"), "the page from the form");
        var processors = notice.GetProperty("processors").EnumerateArray().ToList();
        processors.Select(p => p.GetProperty("name").GetString()).Should().Contain(["Services the club connected", "Systems the club linked with its API key"]);
        notice.GetRawText().Should().NotContain("hooks.example.com", "the address of the club's webhook is the club's own business");
        notice.GetProperty("marketingConsentText").GetString().Should().Be("Send me news about events.", "the enquiry form shows the workspace's words");

        // The application form shows its own words beside the box, and its notice prints those.
        var form = await Ok(await client.PutAsync("/api/applications/form", Body(new { isActive = true, consentText = "Yes, send me the club's newsletter." })), "the application form");
        var fromApplication = await Ok(await anyone.GetAsync($"/api/public/privacy?form={form.GetProperty("publicToken").GetString()}"), "the page from the application form");
        fromApplication.GetProperty("marketingConsentText").GetString().Should().Be("Yes, send me the club's newsletter.");

        // What the AI writer sends to OpenAI, in full.
        using var withAi = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "sk-test-not-real" })));
        var ai = await Ok(await withAi.CreateClient().GetAsync("/api/public/privacy"), "the page with AI drafting on");
        ai.GetProperty("processors").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "OpenAI")
            .GetProperty("purpose").GetString().Should().Contain("deal stage and value");
    }

    [Fact]
    public async Task TheNoticeFromAForm_IsLimitedPerLink_AndTheOperatorsNoticeIsNot()
    {
        var (factory, _, _, _) = await ClubAsync();
        using var _ = factory;
        using var anyone = factory.CreateClient();

        // A form's token is looked up on each read: a script reading it in a loop is stopped.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 65; i++)
            statuses.Add((await anyone.GetAsync($"/api/public/privacy?form={FormToken}")).StatusCode);
        statuses.Take(60).Should().AllSatisfy(s => s.Should().Be(HttpStatusCode.OK), "every visitor of a club's page shares its link");
        statuses.Should().Contain(HttpStatusCode.TooManyRequests, "a script reading the notice in a loop is stopped");

        // Without a form the notice reads only the server's settings, and is not limited.
        for (var i = 0; i < 65; i++)
            (await anyone.GetAsync("/api/public/privacy")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
