using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The companies list's filters, order, figures and export cover the whole workspace.
/// </summary>
/// <remarks>
/// The page filtered by industry and size, sorted by contacts and deals, and exported, over
/// the twenty companies it had been sent, and fetched the counts for every company in the
/// workspace to show twenty of them.
/// </remarks>
public class CompanyListOnServerTests
{
    private sealed record Seeded(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, Guid Org);

    private static async Task<Seeded> SeedAsync(Action<AppDbContext, Guid, Guid> seed)
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        owner.In(org);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db, ownerId, org);
        await db.SaveChangesAsync();
        return new Seeded(factory, owner, ownerId, org);
    }

    private static Company NewCompany(Guid user, Guid org, string name, string? industry = null, string? size = null, DateTime? created = null) => new()
    {
        Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Name = name, Industry = industry, Size = size,
        CreatedAtUtc = created ?? DateTime.UtcNow.AddDays(-60),
    };

    private static void People(AppDbContext db, Guid user, Guid org, Guid company, int count, bool archived = false)
    {
        for (var i = 0; i < count; i++)
            db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Name = $"Person {Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@example.ch",
                CompanyId = company, IsArchived = archived, CreatedAtUtc = DateTime.UtcNow,
            });
    }

    private static Deal NewDeal(Guid user, Guid org, Guid company, decimal amount, string currency, bool isPrivate = false) => new()
    {
        Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Name = "Deal", Amount = amount, Currency = currency,
        CompanyId = company, IsPrivate = isPrivate, CreatedAtUtc = DateTime.UtcNow,
    };

    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string[] Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task SortingByContactsOrDealsReachesEveryCompany_AndEachRowCarriesItsCounts()
    {
        var s = await SeedAsync((db, user, org) =>
        {
            // Twenty-five companies that fill the first page by name, and two that sort after them.
            for (var i = 0; i < 25; i++) db.Companies.Add(NewCompany(user, org, $"Alpha {i:00}"));
            var yankee = NewCompany(user, org, "Yankee Hotel");
            var zeta = NewCompany(user, org, "Zeta Bank");
            db.Companies.AddRange(yankee, zeta);
            People(db, user, org, zeta.Id, 3);
            People(db, user, org, zeta.Id, 2, archived: true);
            People(db, user, org, yankee.Id, 2);
            db.Deals.Add(NewDeal(user, org, zeta.Id, 1000, "CHF"));
            for (var i = 0; i < 3; i++) db.Deals.Add(NewDeal(user, org, yankee.Id, 40000, "CHF"));
            db.Deals.Add(NewDeal(user, org, yankee.Id, 25000, "EUR"));
        });
        using var _ = s.Factory;

        var byContacts = await GetJson(s.Owner, "/api/companies?pageSize=20&sortBy=contacts&sortDir=desc");
        Names(byContacts).Take(2).Should().Equal("Zeta Bank", "Yankee Hotel");
        var zetaRow = byContacts.GetProperty("items")[0];
        zetaRow.GetProperty("contactCount").GetInt32().Should().Be(3, "archived contacts are not counted");
        zetaRow.GetProperty("dealCount").GetInt32().Should().Be(1);

        var byDeals = await GetJson(s.Owner, "/api/companies?pageSize=20&sortBy=deals&sortDir=desc");
        Names(byDeals).First().Should().Be("Yankee Hotel");
        var totals = byDeals.GetProperty("items")[0].GetProperty("dealTotals").EnumerateArray()
            .ToDictionary(t => t.GetProperty("currency").GetString()!, t => t.GetProperty("value").GetDecimal());
        totals.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["CHF"] = 120000m, ["EUR"] = 25000m }, "each currency on its own");
    }

    [Fact]
    public async Task IndustryAndSizeFiltersReachEveryCompany()
    {
        var s = await SeedAsync((db, user, org) =>
        {
            for (var i = 0; i < 25; i++) db.Companies.Add(NewCompany(user, org, $"Alpha {i:00}"));
            db.Companies.Add(NewCompany(user, org, "Zeta Lodge", "Hospitality", "11-50"));
            db.Companies.Add(NewCompany(user, org, "Zeta Inn", "hospitality ", "1-10"));
            db.Companies.Add(NewCompany(user, org, "Zeta Bank", "Finance", "11-50"));
        });
        using var _ = s.Factory;

        Names(await GetJson(s.Owner, "/api/companies?pageSize=20&industry=Hospitality")).Should().BeEquivalentTo(new[] { "Zeta Inn", "Zeta Lodge" },
            "the industry matches however it was typed, on whichever page the company is");
        Names(await GetJson(s.Owner, "/api/companies?pageSize=20&size=11-50")).Should().BeEquivalentTo(new[] { "Zeta Bank", "Zeta Lodge" });
        (await GetJson(s.Owner, "/api/companies?pageSize=20&hasIndustry=false")).GetProperty("totalCount").GetInt32().Should().Be(25);
        Names(await GetJson(s.Owner, "/api/companies?pageSize=20&industry=hospitality&size=1-10")).Should().Equal("Zeta Inn");
    }

    [Fact]
    public async Task TheSummaryCountsTheWholeWorkspace()
    {
        var s = await SeedAsync((db, user, org) =>
        {
            for (var i = 0; i < 25; i++) db.Companies.Add(NewCompany(user, org, $"Alpha {i:00}"));
            var lodge = NewCompany(user, org, "Zeta Lodge", "Hospitality", created: DateTime.UtcNow.AddDays(-2));
            var inn = NewCompany(user, org, "Zeta Inn", "hospitality ");
            var bank = NewCompany(user, org, "Zeta Bank", "Finance");
            db.Companies.AddRange(lodge, inn, bank);
            People(db, user, org, lodge.Id, 1);
            People(db, user, org, inn.Id, 1, archived: true);
            db.Deals.Add(NewDeal(user, org, bank.Id, 40000, "CHF"));
            db.Deals.Add(NewDeal(user, org, lodge.Id, 25000, "EUR"));
            // A private deal of a colleague: the owner may see it, a member may not.
            var colleague = new User { Id = Guid.NewGuid(), Name = "Colleague", Email = "colleague@example.ch", CreatedAtUtc = DateTime.UtcNow };
            db.Users.Add(colleague);
            db.Deals.Add(NewDeal(colleague.Id, org, inn.Id, 5000, "CHF", isPrivate: true));
        });
        using var _ = s.Factory;

        var summary = await GetJson(s.Owner, "/api/companies/summary");
        summary.GetProperty("total").GetInt32().Should().Be(28);
        summary.GetProperty("withContacts").GetInt32().Should().Be(1, "an archived contact does not count");
        summary.GetProperty("withDeals").GetInt32().Should().Be(3);
        summary.GetProperty("addedLast7Days").GetInt32().Should().Be(1);
        summary.GetProperty("withoutIndustry").GetInt32().Should().Be(25);
        var industries = summary.GetProperty("industries").EnumerateArray()
            .ToDictionary(i => i.GetProperty("industry").GetString()!, i => i.GetProperty("count").GetInt32());
        industries.Should().HaveCount(2, "Hospitality and hospitality are one industry");
        industries.Should().Contain("Finance", 1);
        industries.Single(i => i.Key.Equals("Hospitality", StringComparison.OrdinalIgnoreCase)).Value.Should().Be(2);
        var totals = summary.GetProperty("dealTotals").EnumerateArray()
            .ToDictionary(t => t.GetProperty("currency").GetString()!, t => t.GetProperty("value").GetDecimal());
        totals.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["CHF"] = 45000m, ["EUR"] = 25000m });

        var (member, memberId) = await SignInAsync(s.Factory, "member@example.ch");
        await AddMemberAsync(s.Factory, s.Org, memberId, OrgMemberRole.Member);
        member.In(s.Org);
        var seenByMember = await GetJson(member, "/api/companies/summary");
        seenByMember.GetProperty("withDeals").GetInt32().Should().Be(2, "a private deal stays hidden from who may not open it");
        seenByMember.GetProperty("dealTotals").EnumerateArray().Single(t => t.GetProperty("currency").GetString() == "CHF")
            .GetProperty("value").GetDecimal().Should().Be(40000m);
    }

    [Fact]
    public async Task TheExportHoldsEveryMatchingCompany_AndNoCellRunsAsAFormula()
    {
        var s = await SeedAsync((db, user, org) =>
        {
            for (var i = 0; i < 25; i++) db.Companies.Add(NewCompany(user, org, $"Alpha {i:00}", "Retail"));
            var bank = NewCompany(user, org, "@SUM(1+1)", "Finance");
            db.Companies.Add(bank);
            db.Deals.Add(NewDeal(user, org, bank.Id, 40000, "CHF"));
            db.Deals.Add(NewDeal(user, org, bank.Id, 25000, "EUR"));
        });
        using var _ = s.Factory;

        var res = await s.Owner.GetAsync("/api/companies/export?industry=retail");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.GetValues("X-Total-Count").Single().Should().Be("25");
        var lines = Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync()).TrimStart('﻿')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(26, "a header and every retail company, not one page");
        lines[0].Should().Be("Name,Domain,Industry,Size,Website,Location,Contacts,Deals,Deal value,Added (UTC)");

        var all = Encoding.UTF8.GetString(await (await s.Owner.GetAsync("/api/companies/export?industry=finance")).Content.ReadAsByteArrayAsync());
        var row = all.Split("\r\n")[1];
        row.Should().StartWith("'@SUM(1+1)", "a cell that starts with @ is written as text");
        row.Should().Contain("CHF 40000").And.Contain("EUR 25000");
    }

    [SqlServerFact]
    public async Task OnSqlServer_TheListIsFilteredSortedAndCountedInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var lodge = NewCompany(ownerId, org, "Lodge", "Hospitality", "11-50");
                var inn = NewCompany(ownerId, org, "Inn", "hospitality", "1-10");
                var bank = NewCompany(ownerId, org, "Bank", null, "11-50");
                db.Companies.AddRange(lodge, inn, bank);
                People(db, ownerId, org, inn.Id, 2);
                People(db, ownerId, org, lodge.Id, 1);
                db.Deals.Add(NewDeal(ownerId, org, bank.Id, 1000, "CHF"));
                db.Deals.Add(NewDeal(ownerId, org, bank.Id, 2000, "EUR", isPrivate: true));
                await db.SaveChangesAsync();
            }

            Names(await GetJson(owner, "/api/companies?pageSize=20&sortBy=contacts&sortDir=desc")).Should().Equal("Inn", "Lodge", "Bank");
            Names(await GetJson(owner, "/api/companies?pageSize=20&sortBy=deals&sortDir=desc")).First().Should().Be("Bank");
            Names(await GetJson(owner, "/api/companies?pageSize=20&industry=HOSPITALITY")).Should().BeEquivalentTo(new[] { "Inn", "Lodge" });
            Names(await GetJson(owner, "/api/companies?pageSize=20&hasIndustry=false&size=11-50")).Should().Equal("Bank");
            var summary = await GetJson(owner, "/api/companies/summary");
            summary.GetProperty("total").GetInt32().Should().Be(3);
            summary.GetProperty("withContacts").GetInt32().Should().Be(2);
            summary.GetProperty("dealTotals").GetArrayLength().Should().Be(2);
            var export = await owner.GetAsync("/api/companies/export?sortBy=deals&sortDir=desc");
            export.StatusCode.Should().Be(HttpStatusCode.OK, await export.Content.ReadAsStringAsync());
            export.Headers.GetValues("X-Exported-Count").Single().Should().Be("3");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
