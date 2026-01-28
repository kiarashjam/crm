namespace ACI.Application.DTOs;

public record TemplateDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string CopyTypeId,
    string Goal,
    int UseCount);
