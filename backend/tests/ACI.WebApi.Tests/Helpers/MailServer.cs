using ACI.Application.Interfaces;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// A mail server for tests: it keeps every outbox email it is handed and answers as
/// told, so a test can make the relay take a message, refuse it for now or for good.
/// </summary>
public sealed class MailServer
{
    private readonly List<OutboxEmail> _attempts = new();
    private readonly List<OutboxEmail> _sent = new();

    public MailServer()
    {
        Mock = new Mock<IEmailSender>();
        Mock.Setup(s => s.SendOutboxAsync(It.IsAny<OutboxEmail>(), It.IsAny<CancellationToken>()))
            .Returns((OutboxEmail email, CancellationToken _) =>
            {
                lock (_attempts)
                {
                    _attempts.Add(email);
                    if (Answer.Outcome == OutboxSendOutcome.Sent) _sent.Add(email);
                    return Task.FromResult(Answer);
                }
            });
    }

    public Mock<IEmailSender> Mock { get; }

    /// <summary>What the server answers next; it takes everything unless told otherwise.</summary>
    public OutboxSendResult Answer { get; set; } = OutboxSendResult.Sent;

    /// <summary>Every email it was handed, taken or not.</summary>
    public IReadOnlyList<OutboxEmail> Attempts
    {
        get { lock (_attempts) return _attempts.ToList(); }
    }

    /// <summary>The emails it took.</summary>
    public IReadOnlyList<OutboxEmail> Sent
    {
        get { lock (_attempts) return _sent.ToList(); }
    }

    /// <summary>Who the emails it took went to: To, Cc and Bcc.</summary>
    public IReadOnlyList<string> SentTo => Sent.SelectMany(e => e.To.Concat(e.Cc).Concat(e.Bcc)).ToList();

    public static OutboxSendResult NotNow(string why = "421 Service not available") => new(OutboxSendOutcome.Retry, why, 421);
    public static OutboxSendResult Never(string why = "550 5.1.1 No such user") => new(OutboxSendOutcome.Rejected, why, 550);
}

public static class OutboxTesting
{
    /// <summary>Sends what is due in the outbox now, as the background dispatcher would.</summary>
    public static Task<int> DispatchOutboxAsync(this WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<OutboxDispatcher>().DispatchDueAsync();

    /// <summary>Makes every queued message due now, as if its retry delay had passed.</summary>
    public static async Task MakeQueuedDueAsync(this WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        using var db = scope.ServiceProvider.EveryWorkspace();
        foreach (var message in db.OutboundMessages.Where(m => m.Status == "queued"))
        {
            message.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1);
            message.ClaimedUntilUtc = null;
        }
        await db.SaveChangesAsync();
    }
}
