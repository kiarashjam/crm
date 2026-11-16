using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What calls to the language model may cost: each workspace has a monthly allowance a manager
/// sets below the server's ceiling, a team asking at once is slowed, and outside any
/// organization a person has a small allowance of their own. The AI writer was unlimited on the
/// server's key for anyone who signed up.
/// </summary>
public class AiBudgetTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> Draft(HttpClient client) =>
        client.PostAsync("/api/copy/generate", Body(new { copyTypeId = "sales-email", goal = "Book a call", length = "short" }));

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> WorkspaceAsync(AssistantFactory factory, string name)
    {
        var (client, userId) = await SignInAsync(factory, $"{name}@example.ch");
        var org = await CreateAsync(factory, userId, name);
        client.In(org);
        return (client, userId, org);
    }

    [Fact]
    public async Task EachPaidDraftIsTakenFromTheWorkspacesMonth_AndTheOneAfterTheLimitIsRefused()
    {
        using var factory = new AssistantFactory(new() { ["Assistant:DefaultMonthlyLimit"] = "2" }, paidCopy: true);
        var (owner, _, _) = await WorkspaceAsync(factory, "pavillon");

        await Json(await Draft(owner), "the first draft");
        await Json(await Draft(owner), "the second draft");
        var refused = await Json(await Draft(owner), "the third draft", (HttpStatusCode)429);
        refused.GetProperty("errorCode").GetString().Should().Be("Ai.MonthlyLimit");
        refused.GetProperty("detail").GetString().Should().Contain("2 AI calls").And.Contain("Settings › Assistant");

        var settings = await Json(await owner.GetAsync("/api/agent/settings"), "read the settings");
        settings.GetProperty("monthlyLimit").GetInt32().Should().Be(2);
        settings.GetProperty("usage").GetProperty("used").GetInt32().Should().Be(2);
        settings.GetProperty("usage").GetProperty("refused").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DraftsFromTemplatesCostNothingAndAreNotCounted()
    {
        // No paid model: the template writer answers, as on a server without an OpenAI key.
        using var factory = new AssistantFactory(new() { ["Assistant:DefaultMonthlyLimit"] = "1" });
        var (owner, _, _) = await WorkspaceAsync(factory, "pavillon");

        for (var i = 0; i < 3; i++) await Json(await Draft(owner), $"draft {i + 1} from templates");

        (await factory.Services.Read(db => db.AiUsage.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AnotherWorkspacesCallsAreItsOwn()
    {
        using var factory = new AssistantFactory(new() { ["Assistant:DefaultMonthlyLimit"] = "1" }, paidCopy: true);
        var (first, _, _) = await WorkspaceAsync(factory, "first");
        var (second, _, _) = await WorkspaceAsync(factory, "second");

        await Json(await Draft(first), "the first workspace's draft");
        await Json(await Draft(second), "the second workspace's draft: its own month is untouched");
        await Json(await Draft(first), "the first workspace's second draft", (HttpStatusCode)429);

        (await Json(await second.GetAsync("/api/agent/settings"), "the second's usage"))
            .GetProperty("usage").GetProperty("used").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AManagerSetsTheWorkspacesOwnLimit_NeverAboveTheServersCeiling_AndAStaleCopyIsRefused()
    {
        using var factory = new AssistantFactory(new()
        {
            ["Assistant:DefaultMonthlyLimit"] = "1",
            ["Assistant:MaxMonthlyLimit"] = "1000",
        }, paidCopy: true);
        var (owner, _, org) = await WorkspaceAsync(factory, "pavillon");
        var read = await Json(await owner.GetAsync("/api/agent/settings"), "read the settings");
        read.GetProperty("customLimit").ValueKind.Should().Be(JsonValueKind.Null, "nothing saved yet: the server's default applies");
        read.GetProperty("maxLimit").GetInt32().Should().Be(1000);

        var tooHigh = await Json(await owner.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 5000 })), "above the ceiling", HttpStatusCode.BadRequest);
        tooHigh.GetProperty("errorCode").GetString().Should().Be("Assistant.LimitInvalid");

        var saved = await Json(await owner.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 3 })), "a limit of 3");
        saved.GetProperty("monthlyLimit").GetInt32().Should().Be(3);
        var version = saved.GetProperty("version").GetGuid();

        for (var i = 0; i < 3; i++) await Json(await Draft(owner), $"draft {i + 1} of 3");
        await Json(await Draft(owner), "the fourth", (HttpStatusCode)429);

        var stale = await Json(await owner.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 5, version = Guid.NewGuid() })), "a stale copy", HttpStatusCode.Conflict);
        stale.GetProperty("errorCode").GetString().Should().Be("AssistantSettings.Changed.Conflict");
        (await Json(await owner.PutAsync("/api/agent/settings", Body(new { useDefaultLimit = true, version })), "back to the default"))
            .GetProperty("monthlyLimit").GetInt32().Should().Be(1);

        // A sales rep reads the settings (the usage is the team's), but cannot change them.
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrganizationMembers.Single(m => m.UserId == repId && m.OrganizationId == org).PermissionPreset = "sales_rep";
            await db.SaveChangesAsync();
        }
        rep.In(org);
        await Json(await rep.GetAsync("/api/agent/settings"), "the rep reads them");
        var forbidden = await Json(await rep.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 1000 })), "the rep changes them", HttpStatusCode.Forbidden);
        forbidden.GetProperty("errorCode").GetString().Should().Be("Permission.Required");
    }

    [Fact]
    public async Task ALimitOfZeroTurnsTheAiOffInTheWorkspace()
    {
        using var factory = new AssistantFactory(paidCopy: true);
        var (owner, _, _) = await WorkspaceAsync(factory, "pavillon");
        await Json(await owner.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 0 })), "turn it off");

        var refused = await Json(await Draft(owner), "a draft", (HttpStatusCode)429);
        refused.GetProperty("detail").GetString().Should().Contain("turned off");
    }

    [Fact]
    public async Task ATeamAskingAtOnceIsSlowed_BeforeTheMonthIsSpent()
    {
        using var factory = new AssistantFactory(new()
        {
            ["Assistant:DefaultMonthlyLimit"] = "100",
            ["Assistant:WorkspaceBurst"] = "2",
            ["Assistant:WorkspacePerMinute"] = "0",
        }, paidCopy: true);
        var (owner, _, org) = await WorkspaceAsync(factory, "pavillon");
        var (colleague, colleagueId) = await SignInAsync(factory, "colleague@example.ch");
        await AddMemberAsync(factory, org, colleagueId, OrgMemberRole.Member);
        colleague.In(org);

        await Json(await Draft(owner), "the owner's draft");
        await Json(await Draft(colleague), "a colleague's draft");
        var busy = await Json(await Draft(owner), "a third at once", (HttpStatusCode)429);
        busy.GetProperty("errorCode").GetString().Should().Be("Ai.Busy");

        (await Json(await owner.GetAsync("/api/agent/settings"), "the usage"))
            .GetProperty("usage").GetProperty("used").GetInt32().Should().Be(2, "a call the throttle refused is not taken from the month");
    }

    [Fact]
    public async Task OutsideAnyOrganizationAPersonHasASmallAllowanceOfTheirOwn()
    {
        using var factory = new AssistantFactory(new() { ["Assistant:PersonalMonthlyLimit"] = "1" }, paidCopy: true);
        var (alone, _) = await SignInAsync(factory, "alone@example.ch");
        var (other, _) = await SignInAsync(factory, "other@example.ch");

        await Json(await Draft(alone), "the first draft");
        var refused = await Json(await Draft(alone), "the second", (HttpStatusCode)429);
        refused.GetProperty("detail").GetString().Should().Contain("You have used your 1 AI calls");
        await Json(await Draft(other), "another person's allowance is their own");
    }

    [Fact]
    public async Task AnotherWorkspaceCannotReadOrChangeThisOnesSettings()
    {
        using var factory = new AssistantFactory();
        var (owner, _, org) = await WorkspaceAsync(factory, "pavillon");
        await Json(await owner.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 7, morningBriefs = false })), "this workspace's settings");
        var (stranger, _, _) = await WorkspaceAsync(factory, "elsewhere");

        // Naming this workspace does not make the stranger a member: the header is dropped, and
        // the endpoint wants a workspace.
        stranger.In(org);
        (await stranger.GetAsync("/api/agent/settings")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await stranger.PutAsync("/api/agent/settings", Body(new { monthlyLimit = 1000 }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mine = await Json(await owner.GetAsync("/api/agent/settings"), "read them again");
        mine.GetProperty("monthlyLimit").GetInt32().Should().Be(7);
        mine.GetProperty("morningBriefs").GetBoolean().Should().BeFalse();
    }
}
