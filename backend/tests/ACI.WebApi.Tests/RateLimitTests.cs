using System.Net;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The endpoints that send mail, spend money, or guard a public address with a
/// password, each with a ceiling. None had one.
/// </summary>
public class RateLimitTests
{
    [Fact]
    public async Task SignUpStopsAfterTenInTenMinutes()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var res = await client.PostAsync("/api/auth/register",
                Body(new { email = $"bulk{i}@example.ch", password = "correct horse battery staple", name = "Bulk" }));
            codes.Add(res.StatusCode);
        }

        codes.Take(10).Should().OnlyContain(c => c == HttpStatusCode.OK);
        codes[10].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InvitationsStopAtThirtyAnHourPerInviter()
    {
        await using var mail = new Mailbox();
        var (owner, ownerId) = await SignInAsync(mail, "owner@club.ch");
        var org = await CreateAsync(mail, ownerId, "Pavillon 46");
        var (manager, managerId) = await SignInAsync(mail, "manager@club.ch");
        await AddMemberAsync(mail, org, managerId, OrgMemberRole.Manager);

        for (var i = 0; i < 30; i++)
        {
            var sent = await owner.In(org).PostAsync($"/api/invites/{org}", Body(new { email = $"guest{i}@example.ch" }));
            sent.StatusCode.Should().Be(HttpStatusCode.OK, await sent.Content.ReadAsStringAsync());
        }
        (await owner.In(org).PostAsync($"/api/invites/{org}", Body(new { email = "one-more@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var invites = await mail.Services.Read(db => db.Invites.Select(i => i.Id).ToList());
        (await owner.In(org).PostAsync($"/api/invites/{invites[0]}/resend", null))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "re-sending is mail too, from the same allowance");

        // Per inviter: a colleague can still invite.
        (await manager.In(org).PostAsync($"/api/invites/{org}", Body(new { email = "colleague-guest@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongWebhookPasswordsCloseTheAddressForAWhile()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@club.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon 46");
        var other = await CreateAsync(factory, ownerId, "Other club");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            foreach (var o in db.Organizations) o.WebhookPassword = "the-real-password";
            await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();
        object Lead(string password) => new { name = "Jean Dupont", email = "jean@example.ch", webhookPassword = password };

        for (var i = 0; i < 10; i++)
            (await client.PostAsync($"/api/webhook/organizations/{org}/leads", Body(Lead($"guess-{i}"))))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsync($"/api/webhook/organizations/{org}/leads", Body(Lead("the-real-password"))))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "while blocked, the right password gets no different answer");

        // Per workspace: another workspace's address still works.
        (await client.PostAsync($"/api/webhook/organizations/{other}/leads", Body(Lead("the-real-password"))))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>The app with a copy generator that answers at once and costs nothing.</summary>
    private sealed class WithFreeCopy : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var generator = new Mock<ICopyGeneratorApplicationService>();
                generator.Setup(g => g.GenerateAsync(It.IsAny<Guid>(), It.IsAny<GenerateCopyRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("Bonjour");
                services.AddScoped(_ => generator.Object);
            });
        }
    }

    [Fact]
    public async Task AiDraftsStopAfterABurstOfTwentyPerPerson()
    {
        await using var factory = new WithFreeCopy();
        var (client, userId) = await SignInAsync(factory, "rep@club.ch");
        var org = await CreateAsync(factory, userId, "Pavillon 46");
        var (colleague, colleagueId) = await SignInAsync(factory, "colleague@club.ch");
        await AddMemberAsync(factory, org, colleagueId, OrgMemberRole.Member);
        var draft = new { copyTypeId = "sales-email", goal = "Book a visit", length = "short" };

        for (var i = 0; i < 20; i++)
            (await client.In(org).PostAsync("/api/copy/generate", Body(draft))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.In(org).PostAsync("/api/copy/generate", Body(draft))).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        (await colleague.In(org).PostAsync("/api/copy/generate", Body(draft))).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
