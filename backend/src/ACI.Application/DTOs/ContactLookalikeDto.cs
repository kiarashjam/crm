namespace ACI.Application.DTOs;

public record ContactFieldComparisonDto(string Field, string? Yours, string? Theirs, bool Same);

public record ContactLookalikeDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    IReadOnlyList<ContactFieldComparisonDto> Fields);

public record MergeContactsRequest(Guid OtherContactId);
