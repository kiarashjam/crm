using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace's own sending address: saved by its owner or a manager, and used as From
/// only once the mail service has verified its domain.
/// </summary>
public class WorkspaceSendingAddressTests
{
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();
        public string VerifiedDomains { get; set; } = "";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:VerifiedSendingDomains"] = VerifiedDomains,
                ["Email:FromAddress"] = "info@pavillon46.ch",
            }));
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task TheOwnerOrAManagerSetsIt_EveryoneSeesIt_AndAMemberCannotChangeIt()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Acme");
        owner.In(org);
        var (member, memberId) = await SignInAsync(app, "member@example.ch");
        await AddMemberAsync(app, org, memberId, OrgMemberRole.Member);
        member.In(org);

        (await member.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "hello@acme.example" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var saved = await Json(await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = " hello@acme.example " })), "owner sets it");
        saved.GetProperty("sendingAddress").GetString().Should().Be("hello@acme.example");
        saved.GetProperty("verified").GetBoolean().Should().BeFalse("nobody has verified acme.example with the mail service");
        saved.GetProperty("sharedAddress").GetString().Should().Be("info@pavillon46.ch");

        (await Json(await member.GetAsync($"/api/organizations/{org}/email"), "member reads it")).GetProperty("sendingAddress").GetString()
            .Should().Be("hello@acme.example");
        (await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "not an address" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "" })), "owner clears it"))
            .GetProperty("sendingAddress").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UntilItsDomainIsVerifiedEmailGoesOutFromTheSharedAddress()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Acme");
        owner.In(org);
        await Json(await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "hello@acme.example" })), "set");

        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "send");

        var sent = app.Mail.Sent.Single();
        sent.FromAddress.Should().BeNull("the relay refuses a domain it has not verified, so the shared address is used");
        sent.MessageId.Should().EndWith("@pavillon46.ch>");
    }

    [Fact]
    public async Task OnceItsDomainIsVerifiedEmailGoesOutFromIt()
    {
        await using var app = new App { VerifiedDomains = "club.example, Acme.Example" };
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Acme");
        owner.In(org);

        var saved = await Json(await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "hello@acme.example" })), "set");
        saved.GetProperty("verified").GetBoolean().Should().BeTrue();
        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "send");

        var sent = app.Mail.Sent.Single();
        sent.FromAddress.Should().Be("hello@acme.example");
        sent.FromName.Should().Be("Acme");
        sent.MessageId.Should().EndWith("@acme.example>");
        sent.ReplyTo.Should().Equal(new[] { "owner@example.ch" }, "a reply still reaches the person who wrote it");
    }
}
