using System.Net;
using System.Text.Json;
using ACI.Application.Configuration;
using ACI.Domain.Enums;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Filters;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.SiteAnalyticsWorld;

namespace ACI.WebApi.Tests;

/// <summary>
/// Platform admins (Platform:AdminEmails): the people who run CadenceE itself. Only they read
/// the site analytics, whatever they may do in any workspace; an address counts only once its
/// account has confirmed it.
/// </summary>
public class PlatformAdminTests
{
    private static readonly string[] Reads = ["/api/site-analytics/summary", "/api/site-analytics/live", "/api/site-analytics/sessions"];

    private static async Task<bool> IsPlatformAdminAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/platform/me");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("isPlatformAdmin");
        return doc.RootElement.GetProperty("isPlatformAdmin").GetBoolean();
    }

    [Fact]
    public async Task Only_a_platform_admin_reads_the_site_analytics()
    {
        await using var app = new AnalyticsApp();
        var anonymous = app.CreateClient();
        var (owner, ownerId) = await app.ConfirmedAsync("someone@example.ch");
        // The owner of a workspace, with every permission in it, is no platform admin.
        var org = await Workspaces.CreateAsync(app, ownerId, "Alpine");
        owner.In(org);
        var (admin, _) = await app.ConfirmedAsync(AnalyticsApp.AdminEmail);
        // Configured as Second.Admin@CadenceE.example: the case does not matter.
        var (second, _) = await app.ConfirmedAsync("second.admin@cadencee.example");

        foreach (var read in Reads)
        {
            (await anonymous.GetAsync(read)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, read);

            var refused = await owner.GetAsync(read);
            refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, read);
            using (var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()))
                problem.RootElement.GetProperty("errorCode").GetString().Should().Be(PlatformAdminAttribute.ErrorCode);

            (await admin.GetAsync(read)).StatusCode.Should().Be(HttpStatusCode.OK, read);
            (await second.GetAsync(read)).StatusCode.Should().Be(HttpStatusCode.OK, read);
        }

        (await IsPlatformAdminAsync(owner)).Should().BeFalse();
        (await IsPlatformAdminAsync(admin)).Should().BeTrue();
        (await IsPlatformAdminAsync(second)).Should().BeTrue();
        (await anonymous.GetAsync("/api/platform/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_admins_address_counts_only_once_the_account_has_confirmed_it()
    {
        await using var app = new AnalyticsApp();
        // Anyone can register with any address; only reading its mail makes it theirs.
        var (claimant, _) = await Workspaces.SignInAsync(app, AnalyticsApp.AdminEmail);

        (await IsPlatformAdminAsync(claimant)).Should().BeFalse();
        (await claimant.GetAsync("/api/site-analytics/summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task With_no_admins_configured_nobody_is_one()
    {
        await using var app = new AnalyticsApp(admins: "");
        var (client, _) = await app.ConfirmedAsync(AnalyticsApp.AdminEmail);

        (await IsPlatformAdminAsync(client)).Should().BeFalse();
        (await client.GetAsync("/api/site-analytics/live")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_platform_admin_who_is_a_member_somewhere_reads_them_from_any_workspace()
    {
        await using var app = new AnalyticsApp();
        var (admin, adminId) = await app.ConfirmedAsync(AnalyticsApp.AdminEmail);
        var (_, otherOwner) = await app.ConfirmedAsync("owner@alpine.example");
        var org = await Workspaces.CreateAsync(app, otherOwner, "Alpine");
        await Workspaces.AddMemberAsync(app, org, adminId, OrgMemberRole.Viewer);

        (await admin.In(org).GetAsync("/api/site-analytics/summary")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await IsPlatformAdminAsync(admin)).Should().BeTrue();
    }

    [Theory]
    [InlineData("a@x.example, B@Y.example", new[] { "a@x.example", "b@y.example" })]
    [InlineData("a@x.example;a@x.example\nc@z.example", new[] { "a@x.example", "c@z.example" })]
    [InlineData(" not-an-address , a@x.example ", new[] { "a@x.example" })]
    [InlineData("", new string[0])]
    public void The_list_is_read_as_addresses_without_regard_to_case(string configured, string[] emails)
    {
        PlatformAdmins.Parse(configured).Should().Equal(emails);
    }

    [Fact]
    public void The_setting_is_named_as_the_deploy_sets_it()
    {
        PlatformSettings.SectionName.Should().Be("Platform");
        nameof(PlatformSettings.AdminEmails).Should().Be("AdminEmails", "the host sets Platform__AdminEmails");
    }
}
