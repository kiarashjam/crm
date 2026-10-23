namespace ACI.Application.DTOs;

/// <summary>Who referred a lead. A null contact clears the referral.</summary>
public record SetLeadReferralRequest
{
    /// <summary>The referring contact, in the same workspace as the lead. Null to clear.</summary>
    public Guid? ContactId { get; init; }
}
