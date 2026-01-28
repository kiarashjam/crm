namespace ACI.Application.DTOs;

public record GenerateCopyRequest(
    string CopyTypeId,
    string Goal,
    string? Context,
    string Length,
    string? CompanyName,
    string? BrandTone);
