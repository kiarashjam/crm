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

    public async Task UpdateAsync(Contract contract, CancellationToken ct = default)
    {
        _db.Contracts.Update(contract);
        await _db.SaveChangesAsync(ct);
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
