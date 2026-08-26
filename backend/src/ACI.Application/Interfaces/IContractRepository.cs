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
