using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A deal's line items, stored on the server.
/// </summary>
/// <remarks>
/// They used to live in each browser, so a deal's breakdown existed on one laptop
/// while the deal itself was shared. The bodies here are the ones the client sends
/// (src/app/api/products.ts). A line item is reached only through its deal, so
/// every test of who may see or change one is a test of the deal's walls.
/// </remarks>
public class DealLineItemsTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> SeedDealAsync(CustomWebApplicationFactory factory, Guid userId, Guid orgId, string value = "9000")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Name = "Founding membership", Value = value, CreatedAtUtc = DateTime.UtcNow };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal.Id;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid OrgId, Guid DealId)> DealAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org, await SeedDealAsync(factory, userId, org));
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid deal, object item) =>
        client.PostAsync($"/api/deals/{deal}/line-items", Body(item));

    [Fact]
    public async Task ADealsLinesAreAddedListedChangedAndRemoved()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, deal) = await DealAsync(factory);

        (await ReadJson(await client.GetAsync($"/api/deals/{deal}/line-items"), "empty list")).GetArrayLength().Should().Be(0);

        var plan = await ReadJson(await AddAsync(client, deal, new { productId = "prod-2", name = "Growth plan (annual)", quantity = 2, unitPrice = 4800 }), "add plan");
        plan.GetProperty("dealId").GetGuid().Should().Be(deal);
        plan.GetProperty("productId").GetString().Should().Be("prod-2", "the catalog is still in the browser, so its id is kept as given");
        var setup = await ReadJson(await AddAsync(client, deal, new { name = "Onboarding", quantity = 1.5, unitPrice = 99.99 }), "add setup");
        setup.GetProperty("quantity").GetDecimal().Should().Be(1.5m);
        setup.GetProperty("unitPrice").GetDecimal().Should().Be(99.99m);

        var listed = await ReadJson(await client.GetAsync($"/api/deals/{deal}/line-items"), "list");
        listed.EnumerateArray().Select(l => l.GetProperty("name").GetString()).Should().Equal("Growth plan (annual)", "Onboarding");

        var changed = await ReadJson(await client.PutAsync($"/api/deals/line-items/{plan.GetProperty("id").GetGuid()}",
            Body(new { quantity = 3 })), "change quantity");
        changed.GetProperty("quantity").GetDecimal().Should().Be(3);
        changed.GetProperty("name").GetString().Should().Be("Growth plan (annual)", "a partial update keeps the rest");

        (await client.DeleteAsync($"/api/deals/line-items/{plan.GetProperty("id").GetGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJson(await client.GetAsync($"/api/deals/{deal}/line-items"), "list after delete"))
            .EnumerateArray().Select(l => l.GetProperty("name").GetString()).Should().Equal("Onboarding");

        var dealNow = await ReadJson(await client.GetAsync($"/api/deals/{deal}"), "read deal");
        dealNow.GetProperty("value").GetString().Should().Be("9000", "lines never rewrite the deal's value; the person sets it from the total");
    }

    [Fact]
    public async Task ALineNeedsANameAQuantityAndAPriceThatMakeSense()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, deal) = await DealAsync(factory);

        async Task Refused(HttpResponseMessage res, string message)
        {
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await res.Content.ReadAsStringAsync()).Should().Contain(message);
        }
        await Refused(await AddAsync(client, deal, new { name = "  ", quantity = 1, unitPrice = 10 }), "needs a name");
        await Refused(await AddAsync(client, deal, new { name = "Plan", quantity = 0, unitPrice = 10 }), "more than 0");
        await Refused(await AddAsync(client, deal, new { name = "Plan", quantity = 1, unitPrice = -5 }), "can't be negative");

        // An edit that clears the quantity field sends 0; it must not stick.
        var item = await ReadJson(await AddAsync(client, deal, new { name = "Plan", quantity = 2, unitPrice = 10 }), "add");
        await Refused(await client.PutAsync($"/api/deals/line-items/{item.GetProperty("id").GetGuid()}", Body(new { quantity = 0 })), "more than 0");
        (await ReadJson(await client.GetAsync($"/api/deals/{deal}/line-items"), "list"))[0]
            .GetProperty("quantity").GetDecimal().Should().Be(2);
    }

    [Fact]
    public async Task AnotherWorkspaceCanNeitherSeeNorChangeADealsLines()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _, deal) = await DealAsync(factory);
        var item = await ReadJson(await AddAsync(owner, deal, new { name = "Plan", quantity = 1, unitPrice = 100 }), "add");
        var id = item.GetProperty("id").GetGuid();
        var (other, otherId) = await SignInAsync(factory, "other@example.ch");
        other.In(await CreateAsync(factory, otherId, "Another club"));

        (await other.GetAsync($"/api/deals/{deal}/line-items")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AddAsync(other, deal, new { name = "Mine", quantity = 1, unitPrice = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PutAsync($"/api/deals/line-items/{id}", Body(new { unitPrice = 1 }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/deals/line-items/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadJson(await owner.GetAsync($"/api/deals/{deal}/line-items"), "owner's list"))[0]
            .GetProperty("unitPrice").GetDecimal().Should().Be(100);
    }

    [Fact]
    public async Task AViewerSeesTheLinesButChangesNone()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org, deal) = await DealAsync(factory);
        var item = await ReadJson(await AddAsync(owner, deal, new { name = "Plan", quantity = 1, unitPrice = 100 }), "add");
        var (viewer, viewerId) = await SignInAsync(factory, "miguel@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        (await ReadJson(await viewer.GetAsync($"/api/deals/{deal}/line-items"), "viewer lists")).GetArrayLength().Should().Be(1);
        (await AddAsync(viewer, deal, new { name = "X", quantity = 1, unitPrice = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PutAsync($"/api/deals/line-items/{item.GetProperty("id").GetGuid()}", Body(new { quantity = 9 })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.DeleteAsync($"/api/deals/line-items/{item.GetProperty("id").GetGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletingTheDealDeletesItsLines()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId, org, deal) = await DealAsync(factory);
        var keep = await SeedDealAsync(factory, userId, org);
        await ReadJson(await AddAsync(client, deal, new { name = "Plan", quantity = 1, unitPrice = 100 }), "add to the doomed deal");
        await ReadJson(await AddAsync(client, keep, new { name = "Other plan", quantity = 1, unitPrice = 50 }), "add to the kept deal");

        (await client.DeleteAsync($"/api/deals/{deal}")).IsSuccessStatusCode.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DealLineItems.CountAsync(i => i.DealId == deal)).Should().Be(0);
        (await db.DealLineItems.CountAsync(i => i.DealId == keep)).Should().Be(1, "another deal's lines are untouched");
    }
}
