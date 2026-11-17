using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Prepared emails for the composer: a starter set in French and English given once, the
/// workspace's own added and changed, and only placeholders the outbox fills.
/// </summary>
public class EmailTemplatesTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory App, HttpClient Owner, Guid OwnerId, Guid Org)> WorkspaceAsync(bool clubOff = false)
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        if (clubOff)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Organizations.FirstAsync(o => o.Id == org)).ClubModuleOff = true;
            await db.SaveChangesAsync();
        }
        owner.In(org);
        return (app, owner, ownerId, org);
    }

    private static object Version(string language, string subject, string body) => new { language, subject, body };

    [Fact]
    public async Task AWorkspaceStartsWithTheStarterSet_InFrenchAndEnglish_GivenOnce()
    {
        var (app, owner, _, _) = await WorkspaceAsync();
        using var _app = app;

        var first = await Json(await owner.GetAsync("/api/email-templates"));
        var templates = first.GetProperty("templates").EnumerateArray().ToList();
        templates.Should().HaveCount(10, "six for everyone and four for a club");
        templates.Should().OnlyContain(t => t.GetProperty("starter").GetBoolean());
        foreach (var t in templates)
            t.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("language").GetString()).Should().Equal("fr", "en");
        first.GetProperty("languages").EnumerateArray().Select(l => l.GetString()).Should().Equal("fr", "en");
        first.GetProperty("placeholders").EnumerateArray().Select(p => p.GetString()).Should().Contain(["firstName", "company", "senderName", "workspace"]);

        var invitation = templates.Single(t => t.GetProperty("name").GetString() == "Invitation to visit the club");
        var fr = invitation.GetProperty("versions")[0];
        fr.GetProperty("subject").GetString().Should().Be("Une invitation à découvrir {{workspace}}");
        fr.GetProperty("body").GetString().Should().StartWith("Bonjour {{firstName|Madame, Monsieur}},");

        // Deleted, it stays deleted: the set is given once.
        await Json(await owner.DeleteAsync($"/api/email-templates/{invitation.GetProperty("id").GetGuid()}"), HttpStatusCode.NoContent);
        var again = await Json(await owner.GetAsync("/api/email-templates"));
        again.GetProperty("templates").GetArrayLength().Should().Be(9);
    }

    [Fact]
    public async Task AWorkspaceWithoutTheClubIsGivenNoClubEmails()
    {
        var (app, owner, _, _) = await WorkspaceAsync(clubOff: true);
        using var _app = app;

        var list = await Json(await owner.GetAsync("/api/email-templates"));
        list.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("category").GetString()).Should().NotContain("Club");
        list.GetProperty("templates").GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task APreparedEmailIsWrittenChangedAndRemoved_ByLanguage()
    {
        var (app, owner, _, _) = await WorkspaceAsync();
        using var _app = app;

        var made = await Json(await owner.PostAsync("/api/email-templates", Body(new
        {
            name = "Wine evening", category = "Events", description = "For the autumn tasting.",
            versions = new[] { Version("fr", "Soirée dégustation, {{firstName}}", "Bonjour {{firstName|Madame, Monsieur}},\r\n\r\nNous serions ravis…\r\n") },
        })), HttpStatusCode.Created);
        var id = made.GetProperty("id").GetGuid();
        made.GetProperty("starter").GetBoolean().Should().BeFalse();
        made.GetProperty("versions")[0].GetProperty("body").GetString().Should().Be("Bonjour {{firstName|Madame, Monsieur}},\n\nNous serions ravis…", "line ends are kept one way");

        // The English version added, the French one changed.
        var changed = await Json(await owner.PutAsync($"/api/email-templates/{id}", Body(new
        {
            name = "Wine evening", category = "Events",
            versions = new[]
            {
                Version("en", "Wine evening, {{firstName}}", "Dear {{firstName}},\n\nWe would be delighted…"),
                Version("fr", "Soirée dégustation", "Bonjour {{firstName}},"),
            },
        })));
        changed.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("language").GetString()).Should().Equal("fr", "en");
        changed.GetProperty("versions")[0].GetProperty("subject").GetString().Should().Be("Soirée dégustation");
        changed.GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);

        // A language left blank is taken off.
        var english = await Json(await owner.PutAsync($"/api/email-templates/{id}", Body(new
        {
            name = "Wine evening",
            versions = new[] { Version("en", "Wine evening", "Dear {{firstName}},"), Version("fr", "", "  ") },
        })));
        english.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("language").GetString()).Should().Equal("en");

        await Json(await owner.DeleteAsync($"/api/email-templates/{id}"), HttpStatusCode.NoContent);
        await Json(await owner.DeleteAsync($"/api/email-templates/{id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnlyPlaceholdersTheOutboxFillsAreTaken_AndBlanksAreFine()
    {
        var (app, owner, _, _) = await WorkspaceAsync();
        using var _app = app;

        var refused = await owner.PostAsync("/api/email-templates", Body(new
        {
            name = "Typo", versions = new[] { Version("en", "Hello {{firstname}}", "Dear {{first_name}}, see you on [[date]].") },
        }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("{{first_name}}").And.Contain("{{firstName}}");

        // Placeholders are matched as the outbox matches them, whatever the case.
        await Json(await owner.PostAsync("/api/email-templates", Body(new
        {
            name = "Fine", versions = new[] { Version("en", "Hello {{FirstName|there}}", "See you on [[date]], {{senderName}}") },
        })), HttpStatusCode.Created);
    }

    [Fact]
    public async Task ANameIsNeeded_OnceInTheWorkspace_AndALanguageWeOffer()
    {
        var (app, owner, _, _) = await WorkspaceAsync();
        using var _app = app;
        await owner.GetAsync("/api/email-templates");

        async Task<string> Refusal(object request)
        {
            var res = await owner.PostAsync("/api/email-templates", Body(request));
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            return await res.Content.ReadAsStringAsync();
        }

        (await Refusal(new { name = " ", versions = new[] { Version("en", "Hi", "Hello") } })).Should().Contain("name");
        (await Refusal(new { name = "thank you", versions = new[] { Version("en", "Hi", "Hello") } })).Should().Contain("already a prepared email named");
        (await Refusal(new { name = "Danke", versions = new[] { Version("de", "Hallo", "Guten Tag") } })).Should().Contain("French or English");
        (await Refusal(new { name = "Empty", versions = new[] { Version("en", "", "") } })).Should().Contain("at least one language");
        (await Refusal(new { name = "No subject", versions = new[] { Version("fr", "", "Bonjour") } })).Should().Contain("needs a subject");
    }

    [Fact]
    public async Task AViewerReadsThem_ButOnlyAMemberWhoEditsRecordsChangesThem()
    {
        var (app, owner, _, org) = await WorkspaceAsync();
        using var _app = app;
        var (viewer, viewerId) = await SignInAsync(app, "viewer@example.ch");
        await AddMemberAsync(app, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        (await Json(await viewer.GetAsync("/api/email-templates"))).GetProperty("templates").GetArrayLength().Should().Be(10);
        (await viewer.PostAsync("/api/email-templates", Body(new { name = "Mine", versions = new[] { Version("en", "Hi", "Hello") } })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OneWorkspacesPreparedEmailsAreNotAnothers()
    {
        var (app, owner, ownerId, _) = await WorkspaceAsync();
        using var _app = app;
        var mine = await Json(await owner.PostAsync("/api/email-templates", Body(new { name = "Ours", versions = new[] { Version("en", "Hi", "Hello") } })), HttpStatusCode.Created);

        var other = await CreateAsync(app, ownerId, "Another club");
        owner.In(other);
        var theirs = await Json(await owner.GetAsync("/api/email-templates"));
        theirs.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("name").GetString()).Should().NotContain("Ours");
        (await owner.PutAsync($"/api/email-templates/{mine.GetProperty("id").GetGuid()}", Body(new { name = "Taken", versions = new[] { Version("en", "Hi", "Hello") } })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheComposerStartsInTheWorkspacesWelcomeLanguage_ElseItsLocalesElseEnglish()
    {
        var (app, owner, _, org) = await WorkspaceAsync();
        using var _app = app;
        (await Json(await owner.GetAsync("/api/email-templates"))).GetProperty("defaultLanguage").GetString().Should().Be("en", "de-CH is neither");

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Organizations.FirstAsync(o => o.Id == org)).Locale = "fr-CH";
            await db.SaveChangesAsync();
        }
        (await Json(await owner.GetAsync("/api/email-templates"))).GetProperty("defaultLanguage").GetString().Should().Be("fr");

        await Json(await owner.PutAsync("/api/onboarding/settings", Body(new { defaultLanguage = "en" })));
        (await Json(await owner.GetAsync("/api/email-templates"))).GetProperty("defaultLanguage").GetString().Should().Be("en", "the welcome's language wins");
    }

    [Fact]
    public void EveryStarterUsesOnlyPlaceholdersTheOutboxFills()
    {
        foreach (var starter in ACI.Application.Common.StarterEmailTemplates.All)
            foreach (var v in starter.Versions)
                ACI.Application.Services.EmailTemplateService.UnknownPlaceholders(v.Subject + "\n" + v.Body)
                    .Should().BeEmpty($"{starter.Key} ({v.Language})");
    }
}
