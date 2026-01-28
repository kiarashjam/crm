namespace ACI.Application.DTOs;

public record CopyHistoryItemDto(
    Guid Id,
    string Type,
    string Copy,
    string RecipientName,
    string RecipientType,
    string RecipientId,
    DateTime CreatedAt);
