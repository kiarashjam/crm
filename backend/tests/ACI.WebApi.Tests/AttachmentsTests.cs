using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Files on leads, contacts, companies and deals, stored on the server.
/// </summary>
/// <remarks>
/// They used to live in each browser's localStorage, so a file attached to a lead
/// existed on one laptop while the lead itself was shared with the team. Two things
/// make the server version trustworthy and are pinned here: a download link is the
/// whole permission and nothing more, and a file never outlives its workspace's walls.
/// </remarks>
public class AttachmentsTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static MultipartFormDataContent Form(string entityType, Guid recordId, string fileName, string contentType, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(entityType), "entityType");
        form.Add(new StringContent(recordId.ToString()), "recordId");
        return form;
    }

    private static async Task<Guid> CreateLeadAsync(HttpClient client, string name) =>
        (await ReadJson(await client.PostAsync("/api/leads", Body(new { name, email = $"{Guid.NewGuid():N}@example.com", status = "New" })),
            "create lead")).GetProperty("id").GetGuid();

    private static async Task<(HttpClient Client, Guid UserId, Guid OrgId, Guid LeadId)> WorkspaceWithLeadAsync(
        CustomWebApplicationFactory factory, string email = "pierre@example.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, $"Club of {email}");
        client.In(org);
        return (client, userId, org, await CreateLeadAsync(client, "Ines Moreau"));
    }

    [Fact]
    public async Task AFileIsUploadedListedDownloadedAndRemoved()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, lead) = await WorkspaceWithLeadAsync(factory);
        var bytes = Encoding.UTF8.GetBytes("Signed membership terms, v3");

        var uploaded = await ReadJson(await client.PostAsync("/api/attachments",
            Form("lead", lead, "terms.txt", "text/plain", bytes)), "upload");
        uploaded.GetProperty("fileName").GetString().Should().Be("terms.txt");
        uploaded.GetProperty("size").GetInt64().Should().Be(bytes.Length);
        uploaded.GetProperty("uploadedByName").GetString().Should().Be("pierre@example.ch");
        var id = uploaded.GetProperty("id").GetGuid();

        var list = await ReadJson(await client.GetAsync($"/api/attachments?entityType=lead&recordId={lead}"), "list");
        list.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Equal(id);
        var url = list[0].GetProperty("url").GetString()!;

        // The link works as a plain link: no Authorization header at all.
        var download = await factory.CreateClient().GetAsync(url);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("inline");
        download.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        download.Headers.CacheControl!.NoStore.Should().BeTrue();

        (await client.DeleteAsync($"/api/attachments/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJson(await client.GetAsync($"/api/attachments?entityType=lead&recordId={lead}"), "list after delete"))
            .GetArrayLength().Should().Be(0);
        (await factory.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ADownloadLinkOpensItsOwnFileAndNothingElse()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, lead) = await WorkspaceWithLeadAsync(factory);
        var a = await ReadJson(await client.PostAsync("/api/attachments", Form("lead", lead, "a.txt", "text/plain", "A"u8.ToArray())), "upload a");
        var b = await ReadJson(await client.PostAsync("/api/attachments", Form("lead", lead, "b.txt", "text/plain", "B"u8.ToArray())), "upload b");
        var anonymous = factory.CreateClient();
        var linkA = a.GetProperty("url").GetString()!;
        var tokenA = linkA[(linkA.LastIndexOf('/') + 1)..];

        (await anonymous.GetAsync($"/api/attachments/{b.GetProperty("id").GetGuid()}/content/{tokenA}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "A's token must not open B");
        (await anonymous.GetAsync(linkA[..^4] + "XXXX")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a tampered token");
        (await anonymous.GetAsync($"/api/attachments/{a.GetProperty("id").GetGuid()}/content/not-a-token"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void ALinkStopsWorkingAfterTwoHours()
    {
        var clock = new SettableClock(DateTimeOffset.Parse("2026-10-18T09:00:00Z"));
        var signer = new AttachmentLinkSigner(new EphemeralDataProtectionProvider(), clock);
        var id = Guid.NewGuid();
        var token = signer.CreateToken(id);

        signer.IsValid(id, token).Should().BeTrue();
        clock.Now = clock.Now.AddHours(1).AddMinutes(59);
        signer.IsValid(id, token).Should().BeTrue("a page left open for most of two hours still downloads");
        clock.Now = clock.Now.AddMinutes(2);
        signer.IsValid(id, token).Should().BeFalse("a link copied out of the app stops working");
    }

    [Theory]
    [InlineData("page.html", "text/html")]
    [InlineData("drawing.svg", "image/svg+xml")]
    [InlineData("script.js", "application/javascript")]
    public async Task AnythingThatCouldRunScriptDownloadsInsteadOfOpening(string fileName, string contentType)
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, lead) = await WorkspaceWithLeadAsync(factory);
        var file = await ReadJson(await client.PostAsync("/api/attachments",
            Form("lead", lead, fileName, contentType, "<script>alert(1)</script>"u8.ToArray())), "upload");

        var download = await factory.CreateClient().GetAsync(file.GetProperty("url").GetString());
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        download.Headers.GetValues("Content-Security-Policy").Single().Should().StartWith("sandbox");
    }

    [Fact]
    public async Task EmptyAndOversizedFilesAreRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _, lead) = await WorkspaceWithLeadAsync(factory);

        (await client.PostAsync("/api/attachments", Form("lead", lead, "empty.txt", "text/plain", Array.Empty<byte>())))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var big = await client.PostAsync("/api/attachments",
            Form("lead", lead, "scan.pdf", "application/pdf", new byte[10 * 1024 * 1024 + 1]));
        big.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await big.Content.ReadAsStringAsync()).Should().Contain("up to 10 MB");
        (await client.PostAsync("/api/attachments", Form("spaceship", lead, "a.txt", "text/plain", "x"u8.ToArray())))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnotherWorkspaceCanNeitherAttachToNorSeeARecordsFiles()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _, lead) = await WorkspaceWithLeadAsync(factory);
        var file = await ReadJson(await owner.PostAsync("/api/attachments", Form("lead", lead, "a.txt", "text/plain", "A"u8.ToArray())), "upload");
        var (other, otherId) = await SignInAsync(factory, "other@example.ch");
        other.In(await CreateAsync(factory, otherId, "Another club"));

        (await other.PostAsync("/api/attachments", Form("lead", lead, "b.txt", "text/plain", "B"u8.ToArray())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "A's lead is not in B's workspace");
        (await ReadJson(await other.GetAsync($"/api/attachments?entityType=lead&recordId={lead}"), "list"))
            .GetArrayLength().Should().Be(0);
        (await other.DeleteAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AViewerCanOpenFilesButNotAddOrRemoveThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org, lead) = await WorkspaceWithLeadAsync(factory);
        var file = await ReadJson(await owner.PostAsync("/api/attachments", Form("lead", lead, "a.txt", "text/plain", "A"u8.ToArray())), "upload");
        var (viewer, viewerId) = await SignInAsync(factory, "miguel@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        (await ReadJson(await viewer.GetAsync($"/api/attachments?entityType=lead&recordId={lead}"), "viewer lists"))
            .GetArrayLength().Should().Be(1);
        (await viewer.PostAsync("/api/attachments", Form("lead", lead, "b.txt", "text/plain", "B"u8.ToArray())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.DeleteAsync($"/api/attachments/{file.GetProperty("id").GetGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletingARecordDeletesItsFilesAndFieldValues()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);

        // A deal: its delete runs on the in-memory test database (a lead's clears links
        // with ExecuteUpdateAsync, which that database cannot run). The cleanup is the same.
        var dealId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Deals.Add(new Deal { Id = dealId, UserId = userId, OrganizationId = org, Name = "Founding membership", Value = "12000", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await ReadJson(await client.PostAsync("/api/attachments", Form("deal", dealId, "terms.pdf", "application/pdf", "%PDF"u8.ToArray())), "upload");
        await ReadJson(await client.PostAsync("/api/custom-fields", Body(new { entityType = "deal", label = "Board seat", type = "checkbox" })), "define field");
        (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = "deal", recordId = dealId, values = new { board_seat = true } })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.DeleteAsync($"/api/deals/{dealId}")).IsSuccessStatusCode.Should().BeTrue();

        using var check = factory.Services.CreateScope();
        var after = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await after.Attachments.CountAsync(a => a.RecordId == dealId)).Should().Be(0, "the file goes with its deal");
        (await after.CustomFieldValues.CountAsync(v => v.RecordId == dealId)).Should().Be(0, "and so do its field values");
    }

    private sealed class SettableClock : TimeProvider
    {
        public SettableClock(DateTimeOffset now) => Now = now;
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
