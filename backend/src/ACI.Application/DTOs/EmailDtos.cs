namespace ACI.Application.DTOs;

/// <summary>
/// An email written in the CRM's composer. Addresses may be separated by commas
/// or semicolons. Checked in <c>ComposedEmailService</c>, which says which address
/// is wrong rather than "one or more validation errors occurred".
/// </summary>
/// <param name="Source">"assistant" when the sales assistant drafted it; otherwise the composer.</param>
public record SendEmailRequest(
    string? To, string? Cc, string? Bcc, string? Subject, string? Body,
    Guid? LeadId = null, Guid? ContactId = null, Guid? DealId = null, string? Source = null);

/// <summary>
/// A message that was sent, or queued to be: the shape the composer shows. Status is
/// "sent" once the mail server took it, or "queued" when it did not take it yet and the
/// outbox will keep trying (the timeline shows it once it has gone).
/// </summary>
public record SentEmailDto(
    Guid Id,
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    string Body,
    string Status,
    string Direction,
    DateTime SentAtUtc,
    Guid? LeadId = null,
    Guid? ContactId = null,
    Guid? DealId = null);
