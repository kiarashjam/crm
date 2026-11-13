using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// Storage for contracts and their audit trail.
/// </summary>
/// <remarks>
/// Every method except <see cref="GetBySigningTokenHashAsync"/> takes an
/// organisation id and filters on it, so a contract cannot be reached across
/// organisations by guessing an id. The token lookup is the deliberate exception:
/// the counterparty has no account and no organisation, and the token itself is
/// the authorisation.
/// </remarks>
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    /// <summary>Contracts for one lead, newest first.</summary>
    Task<IReadOnlyList<Contract>> GetForLeadAsync(Guid leadId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Contracts for one deal, newest first: those drafted on it, and those of the lead that
    /// became it (drafted before the lead was converted, so they name only the lead).
    /// </summary>
    Task<IReadOnlyList<Contract>> GetForDealAsync(Guid dealId, Guid organizationId, CancellationToken ct = default);

    /// <summary>The lead that was converted into the deal, if one was (the latest, should there be several).</summary>
    Task<Guid?> LeadOfDealAsync(Guid dealId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The deal the lead was converted into, if it was: whoever can see the contract learns which
    /// deal it is for, as the workspace's fact, not through what they may see of the lead.
    /// </summary>
    Task<Guid?> DealOfLeadAsync(Guid leadId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Looked up by token hash alone — the public signing path.
    /// </summary>
    /// <remarks>
    /// Takes the HASH, never the raw token: the caller hashes first, so no secret
    /// is compared in application code and a leaked database row cannot be
    /// replayed as a link.
    /// </remarks>
    Task<Contract?> GetBySigningTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<Contract> AddAsync(Contract contract, CancellationToken ct = default);
    /// <returns>
    /// False when the row changed underneath us — a lost race, which is a normal
    /// outcome on a link two people may hold. The caller re-reads and re-checks.
    /// </returns>
    Task<bool> UpdateAsync(Contract contract, CancellationToken ct = default);

    /// <summary>Appends an audit entry. Nothing ever updates or deletes one.</summary>
    Task AddEventAsync(ContractEvent auditEvent, CancellationToken ct = default);

    Task<IReadOnlyList<ContractEvent>> GetEventsAsync(Guid contractId, CancellationToken ct = default);
}
