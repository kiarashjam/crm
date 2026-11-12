namespace ACI.Application.Interfaces;

/// <summary>Lead and deal events for the workspace's own URL.</summary>
public interface IOutboundWebhookPublisher
{
    /// <summary>
    /// Queues one event for the workspace's endpoint, when it has one. The outbox posts
    /// it, retrying with backoff, so the save that raised it never waits on the receiver.
    /// </summary>
    Task QueueAsync(Guid? organizationId, string eventName, object payload, CancellationToken ct = default);
}

/// <summary>What the receiver answered, or why it could not be reached.</summary>
/// <param name="StatusCode">The HTTP status, or null when there was no answer at all.</param>
/// <param name="Error">Why there was no answer: a timeout, a refused connection, an address that is not public.</param>
/// <param name="Body">The start of a successful answer's body (a few kilobytes at most), for a caller that reads it.</param>
public sealed record WebhookPostResult(int? StatusCode, string? Error = null, string? Body = null)
{
    public bool Succeeded => StatusCode is >= 200 and < 300;
}

/// <summary>The one HTTP post of an outbound webhook attempt.</summary>
public interface IWebhookTransport
{
    /// <summary>Posts <paramref name="body"/> as JSON. Never throws for a delivery problem; the answer says what happened.</summary>
    Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
}
