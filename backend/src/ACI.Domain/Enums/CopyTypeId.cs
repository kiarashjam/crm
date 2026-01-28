namespace ACI.Domain.Enums;

/// <summary>
/// Supported copy types for AI generation (matches frontend CopyTypeId).
/// </summary>
public enum CopyTypeId
{
    SalesEmail = 0,
    FollowUp = 1,
    CrmNote = 2,
    DealMessage = 3,
    WorkflowMessage = 4,
}
