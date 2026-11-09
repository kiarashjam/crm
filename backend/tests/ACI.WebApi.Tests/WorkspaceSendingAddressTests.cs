using System.Net;
using System.Text.Json;
using ACI.Application.Configuration;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "info@pavillon46.ch",
            }));
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }

        /// <summary>
        /// What whoever runs Cadence lists in Email:VerifiedSendingDomains. Set once the
        /// workspaces exist, since each entry names the workspace it was verified for.
        /// </summary>
        public void VerifiedDomains(string list) =>
            Services.GetRequiredService<IOptions<EmailSettings>>().Value.VerifiedSendingDomains = list;
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
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Acme");
        owner.In(org);
        app.VerifiedDomains($"club.example={Guid.NewGuid()}, Acme.Example={org}");

        var saved = await Json(await owner.PutAsync($"/api/organizations/{org}/email", Body(new { sendingAddress = "hello@acme.example" })), "set");
        saved.GetProperty("verified").GetBoolean().Should().BeTrue();
        await Json(await owner.PostAsync("/api/email/send", Body(new { to = "jean@example.ch", subject = "Hello", body = "Hi" })), "send");

        var sent = app.Mail.Sent.Single();
        sent.FromAddress.Should().Be("hello@acme.example");
        sent.FromName.Should().Be("Acme");
        sent.MessageId.Should().EndWith("@acme.example>");
        sent.ReplyTo.Should().Equal(new[] { "owner@example.ch" }, "a reply still reaches the person who wrote it");
    }

    [Fact]
    public async Task AnotherWorkspaceCannotSendAsADomainVerifiedForSomeoneElse()
    {
        // Anyone can sign up and save any sending address. Acme's verified domain must
        // not let a stranger's workspace send its customers mail "from" Acme.
        await using var app = new App();
        var (acmeOwner, acmeOwnerId) = await SignInAsync(app, "owner@acme.example");
        var acme = await CreateAsync(app, acmeOwnerId, "Acme");
        acmeOwner.In(acme);
        var (stranger, strangerId) = await SignInAsync(app, "someone@elsewhere.example");
        var other = await CreateAsync(app, strangerId, "Not Acme");
        stranger.In(other);
        app.VerifiedDomains($"acme.example={acme}");

        var saved = await Json(await stranger.PutAsync($"/api/organizations/{other}/email", Body(new { sendingAddress = "ceo@acme.example" })), "stranger names Acme's domain");
        saved.GetProperty("verified").GetBoolean().Should().BeFalse("acme.example was verified for Acme, not for this workspace");
        await Json(await stranger.PostAsync("/api/email/send", Body(new { to = "client@example.ch", subject = "Invoice", body = "Please pay" })), "send");

        var sent = app.Mail.Sent.Single();
        sent.FromAddress.Should().BeNull("only Acme may send from acme.example");
        sent.MessageId.Should().EndWith("@pavillon46.ch>");
    }
}
