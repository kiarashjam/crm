using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A deal's money is a number in a currency: typed, rounded to cents, priced in the
/// workspace's currency unless it names another, with the old text written from it.
/// </summary>
public class DealMoneyTests
{
    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, Guid Org)> WorkspaceAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (factory, owner, ownerId, org);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Deal> Stored(CustomWebApplicationFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        return await db.Deals.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    [Fact]
    public async Task ADealWithNoCurrencyIsPricedInTheWorkspacesNotInDollars()
    {
        var (factory, owner, _, org) = await WorkspaceAsync();
        using var _ = factory;

        var chf = await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Fondation Léman", amount = 85500 })));
        chf.GetProperty("currency").GetString().Should().Be("CHF", "the New Deal dialog sent no currency, and the deal counted as USD");
        chf.GetProperty("amount").GetDecimal().Should().Be(85500m);
        chf.GetProperty("value").GetString().Should().Be("85500", "older clients read the text, written from the number");

        await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" })));
        var eur = await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Salon de Genève", amount = 1200 })));
        eur.GetProperty("currency").GetString().Should().Be("EUR");
        (await Stored(factory, chf.GetProperty("id").GetGuid())).Currency
            .Should().Be("CHF", "a deal keeps the currency it was priced in; nothing is converted");
    }

    [Fact]
    public async Task AnAmountIsKeptToTheCent()
    {
        var (factory, owner, _, _) = await WorkspaceAsync();
        using var _ = factory;

        // What line items add up to: 7 x 14.20 in floating point. The old text field
        // refused it, so "Set deal value" left the deal's value and its lines apart.
        var created = await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Canapés", amount = 99.39999999999999m, currency = "chf" })));
        created.GetProperty("amount").GetDecimal().Should().Be(99.40m);
        created.GetProperty("value").GetString().Should().Be("99.40");
        created.GetProperty("currency").GetString().Should().Be("CHF", "a code in another case is the same code");

        var id = created.GetProperty("id").GetGuid();
        var edited = await Ok(await owner.PutAsync($"/api/deals/{id}", Body(new { amount = 29.985m })));
        edited.GetProperty("amount").GetDecimal().Should().Be(29.99m);
        var stored = await Stored(factory, id);
        stored.Amount.Should().Be(29.99m);
        stored.Value.Should().Be("29.99", "the text follows the number on every write");
    }

    [Fact]
    public async Task OlderClientsStillSendTheAmountAsText()
    {
        var (factory, owner, _, _) = await WorkspaceAsync();
        using var _ = factory;

        var created = await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Gala", value = "1000.50" })));
        created.GetProperty("amount").GetDecimal().Should().Be(1000.50m);

        var id = created.GetProperty("id").GetGuid();
        var both = await Ok(await owner.PutAsync($"/api/deals/{id}", Body(new { amount = 2000, value = "3000" })));
        both.GetProperty("amount").GetDecimal().Should().Be(2000m, "the number counts over the text");
    }

    [Fact]
    public async Task ADealNeedsAnAmountAndARealCurrencyCode()
    {
        var (factory, owner, _, _) = await WorkspaceAsync();
        using var _ = factory;

        (await owner.PostAsync("/api/deals", Body(new { name = "No amount" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/deals", Body(new { name = "Symbol", amount = 10, currency = "€" }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a symbol is not a code a total can be kept under");
        (await owner.PostAsync("/api/deals", Body(new { name = "Too big", amount = 99999999999999999999m }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "more digits than the column holds would fail the whole save");

        var created = await Ok(await owner.PostAsync("/api/deals", Body(new { name = "Gala", amount = 10 })));
        var id = created.GetProperty("id").GetGuid();
        (await owner.PutAsync($"/api/deals/{id}", Body(new { currency = "Fr." }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Stored(factory, id)).Currency.Should().Be("CHF");
    }

    [Fact]
    public async Task AConvertedLeadsDealIsPricedInTheWorkspacesCurrency()
    {
        var (factory, owner, _, org) = await WorkspaceAsync();
        using var _ = factory;
        await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" })));
        var lead = await Ok(await owner.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", source = "Website", status = "New" })));

        var converted = await Ok(await owner.PostAsync($"/api/leads/{lead.GetProperty("id").GetGuid()}/convert",
            Body(new { createDeal = true, dealName = "Membership", dealAmount = 2400.5m })));

        var deal = await Stored(factory, converted.GetProperty("dealId").GetGuid());
        deal.Currency.Should().Be("EUR", "a converted deal had no currency at all, and counted as USD");
        deal.Amount.Should().Be(2400.50m);
        deal.Value.Should().Be("2400.50");
    }
}
