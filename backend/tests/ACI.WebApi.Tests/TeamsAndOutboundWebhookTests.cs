using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>Sales teams, and the address Cadence posts lead and deal events to.</summary>
public class TeamsAndOutboundWebhookTests
{
    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Owner, HttpClient Rep, Guid RepId, Guid Org)> WorkspaceAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        return (factory, owner, rep, repId, org);
    }

    [Fact]
    public async Task ATeamIsTheManagersToChange_AndHoldsOnlyPeopleInTheWorkspace()
    {
        var (factory, owner, rep, repId, _) = await WorkspaceAsync();
        using var _ = factory;
        var (_, outsiderId) = await SignInAsync(factory, "outsider@example.ch");

        (await rep.PostAsync("/api/teams", Body(new { name = "Rep's own" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var created = await owner.PostAsync("/api/teams", Body(new { name = "Members desk" }));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var teamId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var outsider = await owner.PostAsync($"/api/teams/{teamId}/members", Body(new { userId = outsiderId }));
        outsider.StatusCode.Should().Be(HttpStatusCode.BadRequest, "any user id used to be accepted");
        (await rep.PostAsync($"/api/teams/{teamId}/members", Body(new { userId = repId }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsync($"/api/teams/{teamId}/members", Body(new { userId = repId }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listed = JsonDocument.Parse(await (await rep.GetAsync("/api/teams")).Content.ReadAsStringAsync()).RootElement;
        listed[0].GetProperty("memberUserIds").EnumerateArray().Select(e => e.GetGuid()).Should().Equal(repId);
    }

    [Theory]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://10.1.2.3/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://192.168.0.10/hook")]
    [InlineData("https://[::1]/hook")]
    [InlineData("http://hooks.example.com/cadence")]
    public async Task AnOutboundAddressMustBePublicHttps(string url)
    {
        var (factory, owner, _, _, org) = await WorkspaceAsync();
        using var _ = factory;

        var res = await owner.PutAsync($"/api/webhook/organizations/{org}/outbound", Body(new { url, secret = "s3cret" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheOwnerSetsTheAddress_ARepCannot_AndTheSecretIsNeverReadBack()
    {
        var (factory, owner, rep, _, org) = await WorkspaceAsync();
        using var _ = factory;

        (await rep.PutAsync($"/api/webhook/organizations/{org}/outbound", Body(new { url = "https://hooks.example.com/cadence" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.PutAsync($"/api/webhook/organizations/{org}/outbound", Body(new { url = "https://hooks.example.com/cadence", secret = "s3cret" })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var body = await (await owner.GetAsync($"/api/webhook/organizations/{org}")).Content.ReadAsStringAsync();
        var info = JsonDocument.Parse(body).RootElement;
        info.GetProperty("outboundWebhookUrl").GetString().Should().Be("https://hooks.example.com/cadence");
        info.GetProperty("hasOutboundWebhookSecret").GetBoolean().Should().BeTrue();
        body.Should().NotContain("s3cret");
    }
}
