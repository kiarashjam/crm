namespace ACI.Application.DTOs;

/// <summary>
/// An email written in the CRM's composer. Addresses may be separated by commas
/// or semicolons. Checked in <c>ComposedEmailService</c>, which says which address
/// is wrong rather than "one or more validation errors occurred".
/// </summary>
public record SendEmailRequest(string? To, string? Cc, string? Bcc, string? Subject, string? Body);

/// <summary>A message that went out — the shape the composer shows and logs.</summary>
public record SentEmailDto(
    Guid Id,
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    string Body,
    string Status,
    string Direction,
    DateTime SentAtUtc);
