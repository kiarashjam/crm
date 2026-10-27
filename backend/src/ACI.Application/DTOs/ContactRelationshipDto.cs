namespace ACI.Application.DTOs;

/// <summary>
/// One relationship, seen from the contact it was asked for: <c>RelatedContactName</c>
/// is this contact's <c>Type</c> (their spouse, their child, ...).
/// </summary>
public record ContactRelationshipDto(
    Guid Id,
    string Type,
    Guid RelatedContactId,
    string RelatedContactName,
    string? RelatedContactEmail,
    bool RelatedContactIsArchived,
    Guid? RelatedLeadId,
    DateTime CreatedAtUtc);

/// <summary>A contact's relationships. <c>ContactId</c> is null for a lead that has no contact yet.</summary>
public record ContactRelationshipListDto(
    Guid? ContactId,
    IReadOnlyList<ContactRelationshipDto> Items);

/// <summary>Records, or changes, how this contact is related to another one.</summary>
public record SetContactRelationshipRequest
{
    /// <summary>The other contact, in the same workspace.</summary>
    public Guid RelatedContactId { get; init; }

    /// <summary>
    /// What the other contact is to this one: spouse, partner, parent, child, sibling,
    /// family, friend, colleague or other.
    /// </summary>
    public string Type { get; init; } = string.Empty;
}
