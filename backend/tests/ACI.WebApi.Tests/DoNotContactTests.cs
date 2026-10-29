using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Do not contact stops what is sent to a person, and nothing else.
/// </summary>
/// <remarks>
/// It used to be the other way round. The composer and automations sent to anyone
/// (leads had no flag at all, and the composer never looked), while logging a note
/// or an inbound call on the contact was refused — so the compliance trail the flag
/// exists for could not be written.
/// </remarks>
public class DoNotContactTests
{
    /// <summary>The app with a mail server that takes everything and remembers who it went to.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly List<string> SentTo = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
                    .Callback((ComposedEmail email, CancellationToken _) => SentTo.AddRange(email.To.Concat(email.Cc).Concat(email.Bcc)))
                    .ReturnsAsync(ComposedEmailOutcome.Sent);
                sender.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string to, string _, string _, string _, CancellationToken _) => SentTo.Add(to))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid OwnerId, Guid Org)> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (client, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        return (client.In(org), ownerId, org);
    }

    private static async Task<Guid> StoppedContactAsync(HttpClient client, string email = "eva@example.ch")
    {
        var id = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Eva Muster", email })), "add contact"))
            .GetProperty("id").GetGuid();
        await Json(await client.PutAsync($"/api/contacts/{id}", Body(new { doNotContact = true })), "mark do not contact");
        return id;
    }

    [Theory]
    [InlineData("note", "Asked to be removed from marketing, 12 Oct, by phone")]
    [InlineData("call", "She rang to complain about the newsletter")]
    public async Task NotesAndCallsCanStillBeLogged(string type, string subject)
    {
        await using var factory = new WithMailServer();
        var (client, _, _) = await WorkspaceAsync(factory);
        var contactId = await StoppedContactAsync(client);

        await Json(await client.PostAsync("/api/activities", Body(new { type, subject, contactId })), $"log a {type}");
    }

    [Fact]
    public async Task TheComposerWillNotSendToAnAddressThatSaidStop()
    {
        await using var factory = new WithMailServer();
        var (client, _, _) = await WorkspaceAsync(factory);
        await StoppedContactAsync(client);

        var res = await client.PostAsync("/api/email/send", Body(new { to = "Eva@Example.ch", subject = "Our autumn menu", body = "Hello Eva" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Eva@Example.ch is marked do not contact");
        factory.SentTo.Should().BeEmpty();
    }

    [Fact]
    public async Task TheComposerWillNotSendToALeadWhosePersonSaidStop_EvenAtAnotherAddress()
    {
        await using var factory = new WithMailServer();
        var (client, _, _) = await WorkspaceAsync(factory);
        var leadId = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "add lead"))
            .GetProperty("id").GetGuid();
        var contactId = await factory.Services.Read(db => db.Contacts.Single(c => c.ConvertedFromLeadId == leadId).Id);
        await Json(await client.PutAsync($"/api/contacts/{contactId}", Body(new { doNotContact = true })), "mark do not contact");

        var res = await client.PostAsync("/api/email/send", Body(new { to = "jean.private@example.ch", subject = "Hello", body = "Hi Jean", leadId }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.SentTo.Should().BeEmpty();
    }

    [Fact]
    public async Task TheComposerStillSendsToEveryoneElse()
    {
        await using var factory = new WithMailServer();
        var (client, _, _) = await WorkspaceAsync(factory);
        await StoppedContactAsync(client);

        await Json(await client.PostAsync("/api/email/send", Body(new { to = "anne@example.ch", subject = "Hello", body = "Hi Anne" })), "send");

        factory.SentTo.Should().Equal("anne@example.ch");
    }

    [Fact]
    public async Task ANewLeadWithTheAddressOfSomeoneWhoSaidStopGetsNoWelcomeEmail()
    {
        // She asked to be left alone last year; now her address comes in again through
        // the website form. The "new lead" email used to go to her regardless.
        await using var factory = new WithMailServer();
        var (client, _, _) = await WorkspaceAsync(factory);
        await StoppedContactAsync(client);
        await Json(await client.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "send_email", config = new { subject = "Welcome", body = "Hi {{firstName}}" } } },
        })), "add rule");

        await Json(await client.PostAsync("/api/leads", Body(new { name = "Eva Muster", email = "eva@example.ch", status = "New" })), "lead arrives");
        await Json(await client.PostAsync("/api/leads", Body(new { name = "Anne Meier", email = "anne@example.ch", status = "New" })), "another lead arrives");

        factory.SentTo.Should().Equal("anne@example.ch");
    }
}
