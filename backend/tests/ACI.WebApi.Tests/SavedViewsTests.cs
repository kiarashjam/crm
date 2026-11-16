using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Domain.Common;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>Saved views: a list's filters, order and columns under a name, private or shared, with one default per person.</summary>
public class SavedViewsTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid Org, HttpClient Rep)> TeamAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        return (owner, org, rep);
    }

    private static object View(string name, bool shared, string list = "contacts") => new
    {
        list, name, shared,
        query = new Dictionary<string, object> { ["status"] = "active", ["tag"] = new[] { Guid.NewGuid().ToString() }, ["field"] = new[] { "tier:is:Gold" } },
        columns = new[] { "name", "email", "cf:tier", "tags" },
    };

    [Fact]
    public async Task APrivateViewIsItsOwnersAlone_ASharedOneEveryones()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, rep) = await TeamAsync(factory);

        var mine = await Ok(await owner.PostAsync("/api/saved-views", Body(View("My golfers", shared: false))), "save a private view");
        mine.GetProperty("mine").GetBoolean().Should().BeTrue();
        mine.GetProperty("query").GetProperty("field").EnumerateArray().Single().GetString().Should().Be("tier:is:Gold");
        mine.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).Should().Contain("cf:tier");
        var shared = await Ok(await owner.PostAsync("/api/saved-views", Body(View("Gold members", shared: true))), "save a shared view");

        var seen = await Ok(await rep.GetAsync("/api/saved-views?list=contacts"), "the rep's views");
        seen.EnumerateArray().Select(v => v.GetProperty("name").GetString()).Should().Equal("Gold members");
        var theirs = seen.EnumerateArray().Single();
        theirs.GetProperty("mine").GetBoolean().Should().BeFalse();
        theirs.GetProperty("ownerName").GetString().Should().NotBeNullOrEmpty();

        var id = shared.GetProperty("id").GetGuid();
        (await rep.PutAsync($"/api/saved-views/{id}", Body(new { name = "Mine now" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rep.DeleteAsync($"/api/saved-views/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rep.PutAsync($"/api/saved-views/{mine.GetProperty("id").GetGuid()}", Body(new { name = "x" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "a private view of someone else's is not there for them");
        (await Ok(await rep.GetAsync("/api/saved-views?list=deals"), "another list")).EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task EachPersonOpensAListWithOneDefault_AndUnsharingTakesItAway()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, rep) = await TeamAsync(factory);
        var shared = (await Ok(await owner.PostAsync("/api/saved-views", Body(View("Gold members", shared: true))), "save")).GetProperty("id").GetGuid();
        var own = (await Ok(await rep.PostAsync("/api/saved-views", Body(View("My list", shared: false))), "the rep's own")).GetProperty("id").GetGuid();

        (await rep.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = shared }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await rep.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = own }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var views = await Ok(await rep.GetAsync("/api/saved-views?list=contacts"), "the rep's views");
        views.EnumerateArray().Single(v => v.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetGuid().Should().Be(own, "one default per person and list");
        (await factory.Services.Read(db => db.SavedViewDefaults.Count())).Should().Be(1);

        (await rep.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = shared }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Ok(await owner.GetAsync("/api/saved-views?list=contacts"), "the owner's views")).EnumerateArray()
            .Should().OnlyContain(v => !v.GetProperty("isDefault").GetBoolean(), "the rep's default is the rep's alone");

        var version = (await Ok(await owner.GetAsync("/api/saved-views?list=contacts"), "read")).EnumerateArray()
            .Single(v => v.GetProperty("id").GetGuid() == shared).GetProperty("version").GetGuid();
        (await Ok(await owner.PutAsync($"/api/saved-views/{shared}", Body(new { shared = false, version })), "unshare")).GetProperty("shared").GetBoolean().Should().BeFalse();
        (await Ok(await rep.GetAsync("/api/saved-views?list=contacts"), "after")).EnumerateArray()
            .Select(v => v.GetProperty("id").GetGuid()).Should().Equal(own);
        (await rep.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = shared }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await rep.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = (Guid?)null }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AChangeFromAnOlderReadIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await TeamAsync(factory);
        var view = await Ok(await owner.PostAsync("/api/saved-views", Body(View("Gold", shared: false))), "save");
        var id = view.GetProperty("id").GetGuid();
        var first = view.GetProperty("version").GetGuid();
        (await Ok(await owner.PutAsync($"/api/saved-views/{id}", Body(new { name = "Gold members", version = first })), "rename")).GetProperty("name").GetString()
            .Should().Be("Gold members");

        var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/saved-views/{id}") { Content = Body(new { name = "Silver" }) };
        stale.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{first}\""));
        var refused = await owner.SendAsync(stale);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("SavedView.Changed.Conflict");
        (await owner.PostAsync("/api/saved-views", Body(new { list = "invoices", name = "x" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/saved-views", Body(new { list = "contacts", name = "x", query = new { tag = 5 } }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AViewOnlyMemberKeepsViewsForThemselvesButCannotShareOne()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, _) = await TeamAsync(factory);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        var kept = await Ok(await viewer.PostAsync("/api/saved-views", Body(View("Mine", shared: false))), "a viewer keeps a view");
        (await viewer.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = kept.GetProperty("id").GetGuid() })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refused = await viewer.PostAsync("/api/saved-views", Body(View("For all", shared: true)));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // And reading a list's custom values and tags in a batch is no write.
        (await viewer.PostAsync("/api/custom-fields/values/batch", Body(new { entityType = "contact", recordIds = Array.Empty<Guid>() })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsync("/api/tags/records/batch", Body(new { entityType = "contact", recordIds = Array.Empty<Guid>() })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsync("/api/tags", Body(new { name = "Nope" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnotherWorkspacesViewsAreNotThere()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, _) = await TeamAsync(factory);
        var shared = (await Ok(await owner.PostAsync("/api/saved-views", Body(View("Gold", shared: true))), "save")).GetProperty("id").GetGuid();
        (await owner.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = shared }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (rival, rivalId) = await SignInAsync(factory, "rival@example.ch");
        var rivalOrg = await CreateAsync(factory, rivalId, "Rival");
        rival.In(rivalOrg);

        (await Ok(await rival.GetAsync("/api/saved-views?list=contacts"), "rival views")).EnumerateArray().Should().BeEmpty();
        (await rival.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = shared }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await rival.DeleteAsync($"/api/saved-views/{shared}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Read through the workspace filter: the rival workspace sees neither the view nor anyone's default.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>();
        using (tenant.ActFor(rivalOrg, rivalId))
        {
            db.SavedViews.Count().Should().Be(0);
            db.SavedViewDefaults.Count().Should().Be(0);
        }
        using (tenant.ActFor(org, null))
        {
            db.SavedViews.Count().Should().Be(1);
            db.SavedViewDefaults.Count().Should().Be(1);
        }
    }
}
