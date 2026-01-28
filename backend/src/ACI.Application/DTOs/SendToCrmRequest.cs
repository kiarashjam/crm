namespace ACI.Application.DTOs;

public record SendToCrmRequest(
    string ObjectType,
    string RecordId,
    string RecordName,
    string Copy,
    string CopyTypeLabel);
