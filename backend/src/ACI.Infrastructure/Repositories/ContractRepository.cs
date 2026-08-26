using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

/// <inheritdoc />
public class ContractRepository : IContractRepository
{
    private readonly AppDbContext _db;

    public ContractRepository(AppDbContext db) => _db = db;

    public async Task<Contract?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default)
        // Organisation is part of the predicate, not checked afterwards: a contract
        // in another organisation must be indistinguishable from one that does not
        // exist.
        => await _db.Contracts
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);

    public async Task<IReadOnlyList<Contract>> GetForLeadAsync(
        Guid leadId, Guid organizationId, CancellationToken ct = default)
        => await _db.Contracts
            .Where(c => c.LeadId == leadId && c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Contract?> GetBySigningTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;
        // No organisation filter, by design: the counterparty has no account and no
        // organisation. The token IS the authorisation, which is why only its hash
        // is stored and why a void clears it.
        return await _db.Contracts.FirstOrDefaultAsync(c => c.SigningTokenHash == tokenHash, ct);
    }

    public async Task<Contract> AddAsync(Contract contract, CancellationToken ct = default)
    {
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(ct);
        return contract;
    }

    /// <summary>
    /// Saves a contract, refusing the save if the row has moved underneath us.
    /// </summary>
    /// <remarks>
    /// Returns false rather than throwing, because a lost race is a NORMAL outcome
    /// here: two people can hold the same signing link, and a CRM user can void a
    /// contract in the instant somebody signs it. The caller re-reads and lets the
    /// state machine give its usual answer.
    ///
    /// <c>Update</c> marks every property modified, so a stale save writes the whole
    /// snapshot back — which is how a sign racing a void reinstated the signing
    /// token the void had just nulled. The concurrency token is what turns that into
    /// a refusal.
    /// </remarks>
    public async Task<bool> UpdateAsync(Contract contract, CancellationToken ct = default)
    {
        _db.Contracts.Update(contract);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Detach so the caller's next read comes from the database rather than
            // from this context's now-wrong snapshot.
            _db.Entry(contract).State = EntityState.Detached;
            return false;
        }
    }

    public async Task AddEventAsync(ContractEvent auditEvent, CancellationToken ct = default)
    {
        // Append only. Nothing in this repository updates or deletes an event,
        // because an audit trail that can be edited is not one.
        _db.ContractEvents.Add(auditEvent);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContractEvent>> GetEventsAsync(Guid contractId, CancellationToken ct = default)
        => await _db.ContractEvents
            .Where(e => e.ContractId == contractId)
            .OrderBy(e => e.AtUtc)
            .ToListAsync(ct);
}
