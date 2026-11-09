using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An edit made from an older read of a lead, deal, contact or company is refused with
/// 409, instead of writing the older copy over what someone saved since.
/// </summary>
/// <remarks>
/// Only contracts had a version. The deal edit dialog sent back every field it showed,
/// so a rep who left it open undid a colleague's stage move and new value when they
/// saved a change to the description, and the audit log blamed them for the move. Each
/// record now carries a version: the API returns it (in the body and as the ETag), an
/// edit that sends it back (in <c>version</c> or If-Match) is checked against it, and
/// the save itself only lands while the row still holds it.
/// </remarks>
public class EditConflictTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static async Task<JsonElement> Created(HttpResponseMessage res)
    {
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());
        return await ReadJson(res);
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client)> WorkspaceAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        client.In(org);
        return (factory, client);
    }

    private static async Task ShouldBeRefusedAsChanged(HttpResponseMessage res, string code)
    {
        res.StatusCode.Should().Be(HttpStatusCode.Conflict, await res.Content.ReadAsStringAsync());
        var problem = await ReadJson(res);
        problem.GetProperty("errorCode").GetString().Should().Be(code);
        problem.GetProperty("detail").GetString().Should().Contain("Reload", "the person is told what to do next");
    }

    [Fact]
    public async Task ADealEditFromAnOlderReadIsRefusedAndTheNewerSaveStands()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var deal = await Created(await client.PostAsync("/api/deals", Body(new { name = "Autumn gala", amount = 12000 })));
        var id = deal.GetProperty("id").GetGuid();
        var read = deal.GetProperty("version").GetGuid();
        read.Should().NotBe(Guid.Empty);

        // A colleague moves the deal on and raises its value first.
        var colleague = await client.PutAsync($"/api/deals/{id}", Body(new { amount = 15000, probability = 60, version = read }));
        colleague.StatusCode.Should().Be(HttpStatusCode.OK, await colleague.Content.ReadAsStringAsync());
        (await ReadJson(colleague)).GetProperty("version").GetGuid().Should().NotBe(read, "every save gives the deal a new version");

        // The dialog opened before that sends back everything it showed.
        var stale = await client.PutAsync($"/api/deals/{id}", Body(new
        {
            name = "Autumn gala", amount = 12000, probability = 20, description = "Called the venue", version = read,
        }));

        await ShouldBeRefusedAsChanged(stale, "Deal.Changed.Conflict");
        var stored = await factory.Services.Read(db => db.Deals.AsNoTracking().Single(d => d.Id == id));
        stored.Amount.Should().Be(15000m);
        stored.Probability.Should().Be(60);
        stored.Description.Should().BeNull();
    }

    [Fact]
    public async Task AChangeToOneFieldThatSendsNoVersionStillApplies()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var deal = await Created(await client.PostAsync("/api/deals", Body(new { name = "Autumn gala", amount = 12000 })));
        var id = deal.GetProperty("id").GetGuid();
        await Created(await client.PutAsync($"/api/deals/{id}", Body(new { probability = 60 })));

        // A move on the board changes only the stage, so it needs no version: it cannot
        // put anyone's other changes back.
        var move = await client.PutAsync($"/api/deals/{id}", Body(new { stage = "Proposal" }));

        move.StatusCode.Should().Be(HttpStatusCode.OK, await move.Content.ReadAsStringAsync());
        var stored = await factory.Services.Read(db => db.Deals.AsNoTracking().Single(d => d.Id == id));
        stored.Stage.Should().Be("Proposal");
        stored.Probability.Should().Be(60);
    }

    [Fact]
    public async Task TheVersionTravelsAsAnETagAndComesBackInIfMatch()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var deal = await Created(await client.PostAsync("/api/deals", Body(new { name = "Autumn gala", amount = 12000 })));
        var id = deal.GetProperty("id").GetGuid();

        var get = await client.GetAsync($"/api/deals/{id}");
        var etag = get.Headers.ETag;
        etag.Should().NotBeNull();
        etag!.Tag.Should().Be($"\"{deal.GetProperty("version").GetGuid()}\"");

        HttpRequestMessage Put(object body, string ifMatch)
        {
            var req = new HttpRequestMessage(HttpMethod.Put, $"/api/deals/{id}") { Content = Body(body) };
            req.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            return req;
        }

        var saved = await client.SendAsync(Put(new { probability = 40 }, etag.Tag));
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        saved.Headers.ETag!.Tag.Should().NotBe(etag.Tag, "the answer names the version the save made");

        await ShouldBeRefusedAsChanged(await client.SendAsync(Put(new { probability = 10 }, etag.Tag)), "Deal.Changed.Conflict");
        (await client.SendAsync(Put(new { probability = 10 }, "\"not-a-version\""))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "no tag this API hands out looks like that");
        (await client.SendAsync(Put(new { probability = 10, version = Guid.NewGuid() }, saved.Headers.ETag.Tag))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the body and the header name different versions");
        (await client.SendAsync(Put(new { probability = 10 }, "*"))).StatusCode
            .Should().Be(HttpStatusCode.OK, "\"*\" is satisfied by any version");
    }

    [Fact]
    public async Task ABoardCardCarriesTheVersionItsEditDialogSends()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var deal = await Created(await client.PostAsync("/api/deals", Body(new { name = "Autumn gala", amount = 12000 })));

        var board = await ReadJson(await client.GetAsync("/api/deals/board"));
        var card = board.GetProperty("columns").EnumerateArray()
            .SelectMany(c => c.GetProperty("deals").EnumerateArray())
            .Single(d => d.GetProperty("id").GetGuid() == deal.GetProperty("id").GetGuid());

        card.GetProperty("version").GetGuid().Should().Be(deal.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task ALeadEditFromAnOlderReadIsRefused()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var lead = await Created(await client.PostAsync("/api/leads", Body(new { name = "Jean Muster", email = "jean@example.ch", source = "Website", status = "New" })));
        var id = lead.GetProperty("id").GetGuid();
        var read = lead.GetProperty("version").GetGuid();
        await Created(await client.PutAsync($"/api/leads/{id}", Body(new { phone = "+41 79 111 22 33", version = read })));

        var stale = await client.PutAsync($"/api/leads/{id}", Body(new { name = "Jean Muster", phone = "+41 79 999 99 99", version = read }));

        await ShouldBeRefusedAsChanged(stale, "Lead.Changed.Conflict");
        (await factory.Services.Read(db => db.Leads.AsNoTracking().Single(l => l.Id == id))).Phone.Should().Be("+41 79 111 22 33");
    }

    [Fact]
    public async Task AContactEditFromAnOlderReadIsRefused()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var contact = await Created(await client.PostAsync("/api/contacts", Body(new { name = "Anna Keller", email = "anna@example.ch" })));
        var id = contact.GetProperty("id").GetGuid();
        var read = contact.GetProperty("version").GetGuid();
        await Created(await client.PutAsync($"/api/contacts/{id}", Body(new { jobTitle = "CFO", version = read })));

        var stale = await client.PutAsync($"/api/contacts/{id}", Body(new { name = "Anna Keller", jobTitle = "Analyst", version = read }));

        await ShouldBeRefusedAsChanged(stale, "Contact.Changed.Conflict");
        (await factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id))).JobTitle.Should().Be("CFO");
    }

    [Fact]
    public async Task ACompanyEditFromAnOlderReadIsRefused()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var company = await Created(await client.PostAsync("/api/companies", Body(new { name = "Hotel Bellevue" })));
        var id = company.GetProperty("id").GetGuid();
        var read = company.GetProperty("version").GetGuid();
        await Created(await client.PutAsync($"/api/companies/{id}", Body(new { industry = "Hospitality", version = read })));

        var stale = await client.PutAsync($"/api/companies/{id}", Body(new { name = "Hotel Bellevue", industry = "Retail", version = read }));

        await ShouldBeRefusedAsChanged(stale, "Company.Changed.Conflict");
        (await factory.Services.Read(db => db.Companies.AsNoTracking().Single(c => c.Id == id))).Industry.Should().Be("Hospitality");
    }

    [Fact]
    public async Task TheCompaniesListCarriesTheVersionItsEditDialogSends()
    {
        var (factory, client) = await WorkspaceAsync();
        using var _ = factory;
        var company = await Created(await client.PostAsync("/api/companies", Body(new { name = "Hotel Bellevue" })));

        var list = await ReadJson(await client.GetAsync("/api/companies?page=1&pageSize=20"));
        var row = list.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == company.GetProperty("id").GetGuid());

        row.GetProperty("version").GetGuid().Should().Be(company.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task AStaleSaveNoServicePutIntoWordsStillAnswers409WithAReloadCode()
    {
        // An assign, an archive or a referral that loses the race on the same row: no
        // service has a sentence for it, so the global handler answers, and it must not
        // be the 500 an unexpected exception gets.
        var handler = new ACI.WebApi.Middleware.GlobalExceptionHandler(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ACI.WebApi.Middleware.GlobalExceptionHandler>.Instance, new ProductionHost());
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await handler.TryHandleAsync(context, new DbUpdateConcurrencyException("stale"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(409);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("Record.Changed.Conflict");
        problem.RootElement.GetProperty("detail").GetString().Should().Contain("Reload");
    }

    private sealed class ProductionHost : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ACI.WebApi";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    // ---- The save itself: two requests that both read the same version.

    private static async Task<(Guid UserId, Guid LeadId)> SeedLeadAsync(Func<AppDbContext> open)
    {
        await using var db = open();
        var user = new User { Id = Guid.NewGuid(), Name = "Rep", Email = $"rep-{Guid.NewGuid():N}@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var lead = new Lead { Id = Guid.NewGuid(), UserId = user.Id, Name = "Jean Muster", Email = "jean@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(user, lead);
        await db.SaveChangesAsync();
        return (user.Id, lead.Id);
    }

    private static async Task ALeadSavedFromAnOlderCopyIsRefusedAsync(Func<AppDbContext> open)
    {
        var (userId, id) = await SeedLeadAsync(open);
        await using var first = open();
        await using var second = open();
        var firstRepo = new LeadRepository(first);
        var secondRepo = new LeadRepository(second);
        // Both read the lead before either writes, as two requests do.
        var mine = await firstRepo.GetByIdAsync(id, userId, null);
        var theirs = await secondRepo.GetByIdAsync(id, userId, null);

        mine!.Description = "Met at the open day";
        await firstRepo.UpdateAsync(mine, userId, null);
        // The repository copies every field from the copy it is given, so saving this one
        // used to put the description back to empty.
        theirs!.Phone = "+41 79 000 00 00";
        var late = () => secondRepo.UpdateAsync(theirs, userId, null);

        await late.Should().ThrowAsync<ConcurrencyConflictException>("the lead changed after this copy was read");
        await using var check = open();
        var stored = await check.Leads.AsNoTracking().SingleAsync(l => l.Id == id);
        stored.Description.Should().Be("Met at the open day");
        stored.Phone.Should().BeNull();
    }

    [Fact]
    public async Task ALeadSavedFromAnOlderCopyIsRefused_InMemory()
    {
        var name = $"edit-conflict-{Guid.NewGuid()}";
        await ALeadSavedFromAnOlderCopyIsRefusedAsync(() => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options));
    }

    [Fact]
    public async Task ALeadSavedFromAnOlderCopyIsRefused_Relational()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await ALeadSavedFromAnOlderCopyIsRefusedAsync(() => SqliteDb.Open(connection));
    }

    private static async Task<(Guid UserId, Guid DealId)> SeedDealAsync(Func<AppDbContext> open)
    {
        await using var db = open();
        var user = new User { Id = Guid.NewGuid(), Name = "Rep", Email = $"rep-{Guid.NewGuid():N}@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var deal = new Deal { Id = Guid.NewGuid(), UserId = user.Id, Name = "Autumn gala", Value = "12000", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(user, deal);
        await db.SaveChangesAsync();
        return (user.Id, deal.Id);
    }

    private static async Task TwoDealSavesFromTheSameReadAsync(Func<AppDbContext> open, Guid userId, Guid id, Guid? organizationId = null)
    {
        await using var first = open();
        await using var second = open();
        var firstRepo = new DealRepository(first);
        var secondRepo = new DealRepository(second);
        var mine = await firstRepo.GetByIdAsync(id, userId, organizationId);
        var theirs = await secondRepo.GetByIdAsync(id, userId, organizationId);
        // Both passed the version check: each read the same version, a moment apart.
        mine!.Version.Should().Be(theirs!.Version);

        mine.Probability = 60;
        await firstRepo.UpdateAsync(mine, userId, organizationId);
        theirs.Probability = 20;
        var late = () => secondRepo.UpdateAsync(theirs, userId, organizationId);

        await late.Should().ThrowAsync<ConcurrencyConflictException>("the row no longer holds the version the second save read");
        await using var check = open();
        (await check.Deals.AsNoTracking().SingleAsync(d => d.Id == id)).Probability.Should().Be(60);
    }

    [Fact]
    public async Task OfTwoDealSavesFromTheSameReadTheSecondIsRefused_InMemory()
    {
        var name = $"edit-conflict-{Guid.NewGuid()}";
        AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);
        var (userId, id) = await SeedDealAsync(Open);
        await TwoDealSavesFromTheSameReadAsync(Open, userId, id);
    }

    [Fact]
    public async Task OfTwoDealSavesFromTheSameReadTheSecondIsRefused_Relational()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        AppDbContext Open() => SqliteDb.Open(connection);
        var (userId, id) = await SeedDealAsync(Open);
        await TwoDealSavesFromTheSameReadAsync(Open, userId, id);
    }

    /// <summary>The same two saves on SQL Server, where the version is the column the start-up DDL adds.</summary>
    [SqlServerFact]
    public async Task OfTwoDealSavesFromTheSameReadTheSecondIsRefused_SqlServer()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Created(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" })))).GetProperty("id").GetGuid();
            owner.In(org);
            var deal = await Created(await owner.PostAsync("/api/deals", Body(new { name = "Autumn gala", amount = 12000 })));
            var id = deal.GetProperty("id").GetGuid();

            // Over HTTP: an edit sent with the version it read is refused once the deal moved on.
            var read = deal.GetProperty("version").GetGuid();
            await Created(await owner.PutAsync($"/api/deals/{id}", Body(new { probability = 40, version = read })));
            await ShouldBeRefusedAsChanged(await owner.PutAsync($"/api/deals/{id}", Body(new { probability = 10, version = read })), "Deal.Changed.Conflict");

            AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);
            await TwoDealSavesFromTheSameReadAsync(Open, ownerId, id, org);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
