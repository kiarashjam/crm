namespace ACI.Application.DTOs;

/// <summary>A deal coming into a stage, as its history shows it.</summary>
/// <param name="FromStageName">The stage it left, as it was called then; null for where the deal started.</param>
/// <param name="ToStageName">The stage it came into, as it was called then.</param>
/// <param name="ChangedByName">Who moved it, or created it there.</param>
/// <param name="LeftAtUtc">When it moved on from that stage; null while it is still there.</param>
/// <param name="IsCreation">The stage the deal was created in, rather than moved to.</param>
public record DealStageChangeDto(
    Guid Id,
    Guid? FromStageId,
    string? FromStageName,
    Guid? ToStageId,
    string? ToStageName,
    Guid ChangedByUserId,
    string? ChangedByName,
    DateTime ChangedAtUtc,
    DateTime? LeftAtUtc,
    bool IsCreation);
