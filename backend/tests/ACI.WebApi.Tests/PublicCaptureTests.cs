using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The public capture form and the lead webhook: what a stranger can do with them.
/// </summary>
public class PublicCaptureTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    private static async Task<(CustomWebApplicationFactory Factory, Guid Org, Guid Owner)> WorkspaceWithForm(string formName = "Website")
    {
        var factory = new CustomWebApplicationFactory();
        var (_, owner) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, owner, "Club Nautique");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LeadCaptureForms.Add(new LeadCaptureForm
        {
            Id = Guid.NewGuid(), OrganizationId = org, CreatedByUserId = owner, Name = formName,
            PublicToken = Token, IsActive = true, CreatedAtUtc = DateTime.UtcNow, SuccessMessage = "Merci, à bientôt.",
        });
        var org1 = await db.Organizations.FindAsync(org);
        org1!.WebhookApiKeyHash = ACI.Application.Services.OrganizationService.HashApiKey("test-webhook-key");
        await db.SaveChangesAsync();
        return (factory, org, owner);
    }

    private static async Task<Guid> ExistingLead(CustomWebApplicationFactory factory, Guid org, Guid owner, string name, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = name, Email = email,
            Status = "Contacted", Source = "Referral", CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
    }

    private static StringContent Submission(string email, string name = "Jean-Michel Dupont", string? country = "Switzerland", string? website = null) =>
        Body(new { name, email, phone = "+41790000000", jobTitle = "Owner", country, company = "Dupont SA", website });

    [Fact]
    public async Task AReturningProspectIsRecordedOnTheirLead_AndTheirNameIsNotRevealed()
    {
        // It used to answer 409 "An open lead with this email already exists
        // (Jane Doe)." to anyone holding the public token — a name lookup by email —
        // and the second inquiry was lost.
        var (factory, org, owner) = await WorkspaceWithForm();
        using var _ = factory;
        var leadId = await ExistingLead(factory, org, owner, "Secret Name", "known@example.ch");
        var stranger = factory.CreateClient();

        var res = await stranger.PostAsync($"/api/public/capture/{Token}", Submission("known@example.ch", name: "Someone Else"));
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotContain("Secret Name");
        JsonDocument.Parse(body).RootElement.GetProperty("message").GetString().Should().Be("Merci, à bientôt.");

        var (leads, notes) = await factory.Services.Read(db => (
            db.Leads.Count(l => l.OrganizationId == org),
            db.Activities.Where(a => a.LeadId == leadId).Select(a => a.Subject).ToList()));
        leads.Should().Be(1, "the inquiry is attached, not duplicated");
        notes.Should().ContainSingle().Which.Should().Contain("New inquiry via Website");
    }

    [Fact]
    public async Task ASuccessSaysThanksAndNothingElse()
    {
        // A success used to return the whole lead: the matched company, the
        // assigned rep, the score and internal ids.
        var (factory, org, _) = await WorkspaceWithForm();
        using var _f = factory;
        var res = await factory.CreateClient().PostAsync($"/api/public/capture/{Token}", Submission("new@example.ch"));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().Equal("message");

        (await factory.Services.Read(db => db.Leads.Count(l => l.OrganizationId == org && l.Email == "new@example.ch")))
            .Should().Be(1);
    }

    [Fact]
    public async Task AFormWithALongNameStillSaves()
    {
        // Leads.Source is nvarchar(64), forms may be named up to 120 characters, and
        // the name went straight into Source — so such a form failed every time.
        var longName = new string('W', 100);
        var (factory, org, _) = await WorkspaceWithForm(longName);
        using var _f = factory;

        var res = await factory.CreateClient().PostAsync($"/api/public/capture/{Token}", Submission("long@example.ch"));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var source = await factory.Services.Read(db => db.Leads.Single(l => l.OrganizationId == org).Source);
        source.Should().HaveLength(64);
    }

    [Fact]
    public async Task TheHoneypotThanksABotAndSavesNothing()
    {
        var (factory, org, _) = await WorkspaceWithForm();
        using var _f = factory;
        var res = await factory.CreateClient().PostAsync($"/api/public/capture/{Token}", Submission("bot@example.ch", website: "http://spam.example"));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await factory.Services.Read(db => db.Leads.Count(l => l.OrganizationId == org))).Should().Be(0);
    }

    [Fact]
    public async Task OversizedFieldsAreRefusedBeforeAnythingIsWritten()
    {
        // An 81-character country used to fail the insert with a 500 — after the
        // company row and the round-robin position were already saved.
        var (factory, org, _) = await WorkspaceWithForm();
        using var _f = factory;
        var res = await factory.CreateClient().PostAsync($"/api/public/capture/{Token}",
            Submission("long-country@example.ch", country: new string('X', 81)));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (leads, companies) = await factory.Services.Read(db => (
            db.Leads.Count(l => l.OrganizationId == org), db.Companies.Count(c => c.OrganizationId == org)));
        leads.Should().Be(0);
        companies.Should().Be(0);
    }

    [Fact]
    public async Task AScriptPostingTheTokenInALoopIsSlowedDown()
    {
        var (factory, _, _) = await WorkspaceWithForm();
        using var _f = factory;
        var client = factory.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i += 1)
        {
            var res = await client.PostAsync($"/api/public/capture/{Token}", Submission($"spam{i}@example.ch", website: "x"));
            codes.Add(res.StatusCode);
        }
        codes.Take(20).Should().OnlyContain(c => c == HttpStatusCode.Created);
        codes[20].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TheWebhookAddsAReturningProspectsInquiryInsteadOfFailing()
    {
        // Since #57 a duplicate email made the webhook answer 500: senders retried,
        // the inquiry was lost, and a company row was orphaned on every attempt.
        var (factory, org, owner) = await WorkspaceWithForm();
        using var _f = factory;
        var leadId = await ExistingLead(factory, org, owner, "Jean-Michel Dupont", "jm@example.ch");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-webhook-key");

        var res = await client.PostAsync("/api/webhook/leads",
            Body(new { name = "Jean-Michel Dupont", email = "jm@example.ch", companyName = "Dupont SA", source = "Zapier" }));
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var root = JsonDocument.Parse(body).RootElement;
        root.GetProperty("id").GetGuid().Should().Be(leadId);
        root.GetProperty("duplicate").GetBoolean().Should().BeTrue();

        var (leads, companies, notes) = await factory.Services.Read(db => (
            db.Leads.Count(l => l.OrganizationId == org),
            db.Companies.Count(c => c.OrganizationId == org),
            db.Activities.Count(a => a.LeadId == leadId)));
        leads.Should().Be(1);
        companies.Should().Be(0, "no company is created for an inquiry that goes on an existing lead");
        notes.Should().Be(1);
    }
}
