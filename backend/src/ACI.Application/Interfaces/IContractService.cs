using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// The four automated steps of the contract lifecycle.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><see cref="CreateDraftAsync"/> — the draft is generated from the
/// organisation's template with the lead's details merged in, ready to read.</item>
/// <item><see cref="UpdateDraftAsync"/> — the CRM user changes whatever they
/// want, while it is still a draft.</item>
/// <item><see cref="SendAsync"/> — one button: mints a signing link, emails the
/// counterparty, freezes the text, and advances the lead's pipeline. The
/// counterparty signs through <see cref="SignByTokenAsync"/>.</item>
/// <item><see cref="CountersignAsync"/> — the CRM user adds their signature,
/// which executes the contract and emails the finished copy to both parties.</item>
/// </list>
/// </remarks>
public interface IContractService
{
    Task<Result<ContractDto>> CreateDraftAsync(
        Guid userId, Guid organizationId, CreateContractDraftRequest request, CancellationToken ct = default);

    Task<Result<ContractDto>> GetAsync(
        Guid contractId, Guid organizationId, CancellationToken ct = default);

    Task<IReadOnlyList<ContractDto>> ListForLeadAsync(
        Guid leadId, Guid organizationId, CancellationToken ct = default);

    Task<Result<ContractDto>> UpdateDraftAsync(
        Guid contractId, Guid userId, Guid organizationId, UpdateContractRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends the contract for signature. <c>resend</c> reuses the existing link
    /// rather than minting a second one, which would leave the first live.
    /// </summary>
    Task<Result<SendContractResult>> SendAsync(
        Guid contractId, Guid userId, Guid organizationId, bool resend, CancellationToken ct = default);

    /// <summary>
    /// The counterparty's view of a signing link. Records the first view against
    /// the audit trail.
    /// </summary>
    Task<Result<PublicContractDto>> GetByTokenAsync(
        string rawToken, string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>The counterparty signs. Notifies the CRM user that it is their turn.</summary>
    Task<Result<PublicContractDto>> SignByTokenAsync(
        string rawToken, SignContractRequest request, string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>The counterparty declines, which closes the contract.</summary>
    Task<Result<PublicContractDto>> DeclineByTokenAsync(
        string rawToken, DeclineContractRequest request, string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Our signature, which executes the contract and emails the finished copy to
    /// everyone. Only possible once the counterparty has signed.
    /// </summary>
    Task<Result<ContractDto>> CountersignAsync(
        Guid contractId, Guid userId, Guid organizationId, SignContractRequest request, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Re-sends the executed copy. Exists because email delivery is not
    /// guaranteed, and an executed contract nobody received is worth retrying
    /// without signing anything again.
    /// </summary>
    Task<Result<bool>> ResendExecutedCopyAsync(
        Guid contractId, Guid userId, Guid organizationId, CancellationToken ct = default);

    Task<Result<ContractDto>> VoidAsync(
        Guid contractId, Guid userId, Guid organizationId, string? reason, CancellationToken ct = default);
}
