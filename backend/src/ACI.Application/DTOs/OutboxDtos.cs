namespace ACI.Application.DTOs;

/// <summary>One event posted, or still to be posted, to the workspace's own URL.</summary>
/// <param name="Status">queued, sent, failed or cancelled.</param>
/// <param name="Attempts">Attempts made so far.</param>
/// <param name="LastStatusCode">What the receiver last answered, when it answered.</param>
/// <param name="LastError">Why the last attempt did not go through.</param>
/// <param name="NextAttemptAtUtc">When it is tried again, while it is queued.</param>
public record WebhookDeliveryDto(
    Guid Id,
    string EventName,
    string Status,
    int Attempts,
    int? LastStatusCode,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? NextAttemptAtUtc);
