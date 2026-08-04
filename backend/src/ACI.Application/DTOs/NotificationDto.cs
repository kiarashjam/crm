namespace ACI.Application.DTOs;

/// <summary>
/// Shape consumed by the header bell. Property names match the frontend's
/// AppNotification exactly so no client-side mapping is needed.
/// </summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Message,
    string? Link,
    bool Read,
    DateTime CreatedAtUtc,
    string? SourceKey = null);

/// <summary>Response for the unread badge. An object rather than a bare int so the
/// endpoint can gain fields without breaking clients.</summary>
public record UnreadCountDto(int Count);

/// <summary>Request body for creating a notification.</summary>
public record CreateNotificationRequest(
    string Type,
    string Title,
    string? Message = null,
    string? Link = null,
    bool Read = false,
    DateTime? CreatedAtUtc = null,
    string? SourceKey = null);
