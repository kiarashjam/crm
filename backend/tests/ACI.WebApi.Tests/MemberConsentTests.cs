using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A member's consent to be contacted, per channel (email, SMS, post) and purpose
/// (marketing, service messages), with where it came from and when, as nFADP and GDPR expect.
/// </summary>
/// <remarks>
/// There was none: a member could not be recorded as agreeing to news, or as having said
/// no, and a campaign went to everyone in its segment. Every answer is now kept, and the
/// latest for each channel and purpose stands.
/// </remarks>
public class MemberConsentTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "olga@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string? email = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    private static JsonElement State(JsonElement consent, string channel, string purpose) =>
        consent.GetProperty("current").EnumerateArray()
            .Single(s => s.GetProperty("channel").GetString() == channel && s.GetProperty("purpose").GetString() == purpose);

    [Fact]
    public async Task StaffRecordConsentPerChannelAndPurposeAndEveryAnswerIsKept()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client, email: "Jean.Dupont@Example.ch");

        var none = await Json(await client.GetAsync($"/api/members/{memberId}/consent"), "read consent");
        none.GetProperty("current").GetArrayLength().Should().Be(6, "three channels, two purposes");
        none.GetProperty("current").EnumerateArray().Should().OnlyContain(s => s.GetProperty("state").GetString() == "unknown",
            "nobody has asked yet, and no answer is not a yes");
        none.GetProperty("history").GetArrayLength().Should().Be(0);

        await Json(await client.PostAsync($"/api/members/{memberId}/consent", Body(new
        {
            channel = "email", purpose = "marketing", granted = true, source = "form", detail = "Ticked the newsletter box on the paper form",
        })), "record a yes");
        var after = await Json(await client.PostAsync($"/api/members/{memberId}/consent", Body(new
        {
            channel = "email", purpose = "marketing", granted = false, source = "member", detail = "Asked at the desk to stop the newsletter",
        })), "record a no");

        var email = State(after, "email", "marketing");
        email.GetProperty("state").GetString().Should().Be("withdrawn");
        email.GetProperty("source").GetString().Should().Be("member");
        email.GetProperty("recordedByName").GetString().Should().Be("olga@pavillon46.ch");
        State(after, "sms", "marketing").GetProperty("state").GetString().Should().Be("unknown");

        var history = after.GetProperty("history").EnumerateArray().ToList();
        history.Should().HaveCount(2, "the yes it replaced stays on record");
        history[0].GetProperty("granted").GetBoolean().Should().BeFalse("newest first");
        history[0].GetProperty("isCurrent").GetBoolean().Should().BeTrue();
        history[1].GetProperty("granted").GetBoolean().Should().BeTrue();
        history[1].GetProperty("isCurrent").GetBoolean().Should().BeFalse();
        history[1].GetProperty("detail").GetString().Should().Be("Ticked the newsletter box on the paper form");
        history[1].GetProperty("address").GetString().Should().Be("jean.dupont@example.ch", "the address it was given for, as it was then");

        (await factory.Services.Read(db => db.ConsentRecords.Count(c => c.SubjectId == memberId && c.IsCurrent)))
            .Should().Be(1, "one answer stands for a channel and purpose");
    }

    [Theory]
    [InlineData("fax", "marketing", true, "form", "Consent.ChannelInvalid")]
    [InlineData("email", "spam", true, "form", "Consent.PurposeInvalid")]
    [InlineData("email", "marketing", null, "form", "Consent.AnswerRequired")]
    [InlineData("email", "marketing", true, "unsubscribe", "Consent.SourceInvalid")]
    [InlineData("email", "marketing", true, "import", "Consent.SourceInvalid")]
    public async Task AnAnswerSaysWhatItIsAndHowItWasGiven(string channel, string purpose, bool? granted, string source, string code)
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);

        var res = await client.PostAsync($"/api/members/{memberId}/consent", Body(new { channel, purpose, granted, source }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain(code);
        (await factory.Services.Read(db => db.ConsentRecords.Count())).Should().Be(0);
    }

    [Fact]
    public async Task OnlyThoseWhoManageMembersRecordConsent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var memberId = await MemberAsync(owner);
        var (desk, deskId) = await SignInAsync(factory, "desk@pavillon46.ch");
        await AddMemberAsync(factory, org, deskId, OrgMemberRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrganizationMembers.Single(m => m.UserId == deskId && m.OrganizationId == org).PermissionPreset = "front_desk";
            await db.SaveChangesAsync();
        }
        desk.In(org);

        (await desk.GetAsync($"/api/members/{memberId}/consent")).StatusCode.Should().Be(HttpStatusCode.OK, "anyone in the club may see it");
        var refused = await desk.PostAsync($"/api/members/{memberId}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "member" }));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain(Domain.Common.MemberPermissions.ClubMembers);
    }

    [Fact]
    public async Task AnotherWorkspacesMemberConsentCanBeNeitherReadNorChanged()
    {
        using var factory = new CustomWebApplicationFactory();
        var (alpine, _, _) = await ClubAsync(factory, "owner@alpine.example");
        var (bernina, _, orgB) = await ClubAsync(factory, "owner@bernina.example");
        var theirs = await MemberAsync(bernina, "Zebrafish");
        await Json(await bernina.PostAsync($"/api/members/{theirs}/consent", Body(new
        {
            channel = "email", purpose = "marketing", granted = true, source = "form", detail = "Zebrafish form",
        })), "their own consent");

        var read = await alpine.GetAsync($"/api/members/{theirs}/consent");
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.Content.ReadAsStringAsync()).Should().NotContain("Zebrafish");
        var write = await alpine.PostAsync($"/api/members/{theirs}/consent", Body(new { channel = "email", purpose = "marketing", granted = false, source = "staff" }));
        write.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var rows = await factory.Services.Read(db => db.ConsentRecords.Where(c => c.SubjectId == theirs).ToList());
        rows.Should().ContainSingle().Which.Should().Match<ConsentRecord>(c => c.Granted && c.OrganizationId == orgB);
    }

    [Fact]
    public async Task MergingTwoRecordsOfOnePersonKeepsEveryAnswerAndTheLatestStands()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var keep = await MemberAsync(client, "Jean");
        var drop = await MemberAsync(client, "Jeannot");
        await Json(await client.PostAsync($"/api/members/{keep}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })), "yes on one record");
        await Json(await client.PostAsync($"/api/members/{drop}/consent", Body(new { channel = "email", purpose = "marketing", granted = false, source = "member" })), "no on the other, later");
        await Json(await client.PostAsync($"/api/members/{drop}/consent", Body(new { channel = "post", purpose = "marketing", granted = true, source = "form" })), "post on the other only");

        (await client.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var merged = await Json(await client.GetAsync($"/api/members/{keep}/consent"), "read the merged member");
        State(merged, "email", "marketing").GetProperty("state").GetString().Should().Be("withdrawn",
            "the person's latest answer, given on the record merged away, still stands: they are not emailed news again");
        State(merged, "post", "marketing").GetProperty("state").GetString().Should().Be("granted");
        merged.GetProperty("history").GetArrayLength().Should().Be(3, "every answer either record held");
        (await factory.Services.Read(db => db.ConsentRecords.Count(c => c.SubjectId == drop))).Should().Be(0,
            "nothing is left pointing at a member who no longer exists");
        (await factory.Services.Read(db => db.ConsentRecords.Count(c => c.SubjectId == keep && c.IsCurrent))).Should().Be(2);
    }
}
