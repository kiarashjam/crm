using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Lead and deal events posted to the workspace's own URL, through the outbox.
/// </summary>
/// <remarks>
/// Delivery used to be one POST awaited inside the save: a slow receiver held up every
/// lead save, public form and CSV row for up to five seconds, and any failure was a log
/// line with the event gone for good.
/// </remarks>
public class OutboundWebhookDeliveryTests
{
    /// <summary>A receiver that answers as told, or not at all.</summary>
    private sealed class Receiver : IWebhookTransport
    {
        public readonly List<(Uri Url, string Body, IReadOnlyDictionary<string, string> Headers)> Posts = new();
        public readonly Queue<int> Answers = new();
        public TaskCompletionSource? Hang { get; set; }

        public async Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            lock (Posts) Posts.Add((url, body, headers));
            if (Hang != null) await Hang.Task.WaitAsync(ct);
            var status = Answers.Count > 0 ? Answers.Dequeue() : 200;
            return new WebhookPostResult(status, status is >= 200 and < 300 ? null : $"The receiver answered {status}");
        }

        public int Count
        {
            get { lock (Posts) return Posts.Count; }
        }
    }

    private class App : CustomWebApplicationFactory
    {
        public readonly Receiver Hook = new();
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IWebhookTransport>(_ => Hook);
                services.AddSingleton(Mail.Mock.Object);
            });
        }
    }

    /// <summary>The same app with the background dispatcher running, as in production.</summary>
    private sealed class LiveApp : App
    {
        protected override bool RunsOutboxInBackground => true;
    }

    private static async Task<(HttpClient Owner, Guid Org)> WorkspaceAsync(App app, string secret = "s3cret")
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        (await owner.PutAsync($"/api/webhook/organizations/{org}/outbound", Body(new { url = "https://hooks.example.com/cadence", secret })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (owner, org);
    }

    private static Task<HttpResponseMessage> AddLeadAsync(HttpClient client) =>
        client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" }));

    [Fact]
    public async Task ALeadSaveDoesNotWaitForTheReceiver()
    {
        await using var app = new App();
        var (owner, org) = await WorkspaceAsync(app);
        app.Hook.Hang = new TaskCompletionSource();

        var clock = Stopwatch.StartNew();
        var res = await AddLeadAsync(owner);
        clock.Stop();

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4), "the save no longer waits on the receiver");
        app.Hook.Count.Should().Be(0, "nothing is posted inside the save");
        var queued = await app.Services.Read(db => db.OutboundMessages.Single());
        queued.OrganizationId.Should().Be(org);
        queued.EventName.Should().Be("lead.created");
        queued.Status.Should().Be("queued");
        app.Hook.Hang.SetResult();
    }

    [Fact]
    public async Task AnEventThatFailsIsTriedAgain_SignedAndWithTheSameIdEachTime()
    {
        await using var app = new App();
        var (owner, org) = await WorkspaceAsync(app);
        var leadId = JsonDocument.Parse(await (await AddLeadAsync(owner)).Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        app.Hook.Answers.Enqueue(503);

        await app.DispatchOutboxAsync();
        var afterFirst = await app.Services.Read(db => db.OutboundMessages.Single());
        afterFirst.Status.Should().Be("queued");
        afterFirst.LastStatusCode.Should().Be(503);
        afterFirst.LastError.Should().Contain("503");

        await app.MakeQueuedDueAsync();
        await app.DispatchOutboxAsync();

        (await app.Services.Read(db => db.OutboundMessages.Single().Status)).Should().Be("sent");
        app.Hook.Posts.Should().HaveCount(2);
        foreach (var (url, body, headers) in app.Hook.Posts)
        {
            url.Should().Be(new Uri("https://hooks.example.com/cadence"));
            headers["X-Cadence-Event-Id"].Should().Be(afterFirst.Id.ToString("D"));
            headers["X-Cadence-Signature"].Should().Be(Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes("s3cret"), Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("eventName").GetString().Should().Be("lead.created");
            doc.RootElement.GetProperty("payload").GetProperty("Id").GetGuid().Should().Be(leadId);
            doc.RootElement.GetProperty("organizationId").GetGuid().Should().Be(org);
        }
    }

    [Fact]
    public async Task AWonDealIsAnnounced()
    {
        await using var app = new App();
        var (owner, _) = await WorkspaceAsync(app);
        var deal = JsonDocument.Parse(await (await owner.PostAsync("/api/deals", Body(new { name = "Pavilion renewal", value = "48000" }))).Content.ReadAsStringAsync()).RootElement;
        var dealId = deal.GetProperty("id").GetGuid();

        (await owner.PutAsync($"/api/deals/{dealId}", Body(new { isWon = true, stage = "Closed Won" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        await app.DispatchOutboxAsync();

        app.Hook.Posts.Select(p => p.Headers["X-Cadence-Event"]).Should().Contain("deal.won");
    }

    [Fact]
    public async Task TheOwnerSeesWhatWasDelivered_ARepDoesNot()
    {
        await using var app = new App();
        var (owner, org) = await WorkspaceAsync(app);
        await AddLeadAsync(owner);
        app.Hook.Answers.Enqueue(500);
        await app.DispatchOutboxAsync();
        var (rep, repId) = await SignInAsync(app, "rep@example.ch");
        await AddMemberAsync(app, org, repId, OrgMemberRole.Member);

        (await rep.In(org).GetAsync($"/api/webhook/organizations/{org}/deliveries")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var res = await owner.GetAsync($"/api/webhook/organizations/{org}/deliveries");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var delivery = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.EnumerateArray().Single();
        delivery.GetProperty("eventName").GetString().Should().Be("lead.created");
        delivery.GetProperty("status").GetString().Should().Be("queued");
        delivery.GetProperty("lastStatusCode").GetInt32().Should().Be(500);
        delivery.GetProperty("attempts").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AReceiverThatDoesNotAnswerDoesNotHoldUpEveryoneElsesEmail()
    {
        // One outbox sends for every workspace. Each attempt at a receiver that never
        // answers waits for the timeout; taken one at a time, a workspace whose receiver
        // is down held every other workspace's email behind its events.
        await using var app = new App();
        var (owner, org) = await WorkspaceAsync(app);
        app.Hook.Hang = new TaskCompletionSource();
        (await AddLeadAsync(owner)).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            var eventDue = db.OutboundMessages.Single().NextAttemptAtUtc!.Value;
            db.OutboundMessages.Add(new ACI.Domain.Entities.OutboundMessage
            {
                Id = Guid.NewGuid(), OrganizationId = org, Channel = "email", Source = "automation", Status = "queued",
                // Due just after the event, so a dispatcher taking one at a time reaches it second.
                NextAttemptAtUtc = eventDue.AddMilliseconds(1), CreatedAtUtc = DateTime.UtcNow, To = "anne@example.ch", Subject = "Welcome",
                Body = "Hello", TextBody = "Hello", HtmlBody = "<p>Hello</p>", MessageIdHeader = $"<{Guid.NewGuid():N}@pavillon46.ch>",
            });
            await db.SaveChangesAsync();
        }

        var dispatching = app.DispatchOutboxAsync();
        try
        {
            var waited = Stopwatch.StartNew();
            while (app.Mail.Sent.Count == 0 && waited.Elapsed < TimeSpan.FromSeconds(10))
                await Task.Delay(50);

            app.Mail.Sent.Should().ContainSingle("the email goes while the receiver keeps the event waiting");
            app.Hook.Count.Should().Be(1, "the event is being posted at the same time");
        }
        finally
        {
            app.Hook.Hang.TrySetResult();
        }
        (await dispatching).Should().Be(2);
    }

    [Fact]
    public async Task TheBackgroundDispatcherDeliversWithoutBeingAsked()
    {
        await using var app = new LiveApp();
        var (owner, _) = await WorkspaceAsync(app);

        (await AddLeadAsync(owner)).StatusCode.Should().Be(HttpStatusCode.OK);

        var waited = Stopwatch.StartNew();
        while (app.Hook.Count == 0 && waited.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(100);
        app.Hook.Count.Should().Be(1, "the dispatcher wakes when something is queued");
        while (waited.Elapsed < TimeSpan.FromSeconds(30) && await app.Services.Read(db => db.OutboundMessages.Single().Status) != "sent")
            await Task.Delay(100);
        (await app.Services.Read(db => db.OutboundMessages.Single().Status)).Should().Be("sent");
    }
}
