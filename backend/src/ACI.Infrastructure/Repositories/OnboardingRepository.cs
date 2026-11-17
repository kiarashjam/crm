using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class OnboardingRepository : IOnboardingRepository
{
    private readonly AppDbContext _db;
    public OnboardingRepository(AppDbContext db) => _db = db;

    public Task<OnboardingSettings?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.OnboardingSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

    public void AddSettings(OnboardingSettings settings) => _db.OnboardingSettings.Add(settings);

    public void Add(DealOnboarding onboarding) => _db.DealOnboardings.Add(onboarding);

    public Task<DealOnboarding?> GetAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        _db.DealOnboardings.FirstOrDefaultAsync(o => o.Id == id && o.OrganizationId == organizationId, ct);

    // Read before anyone acts for the handoff's workspace: the caller is the receiving
    // system, known only by the token it presents, which is checked against this row's
    // own workspace before anything is read or written for it.
    public Task<DealOnboarding?> GetInAnyWorkspaceAsync(Guid id, CancellationToken ct = default) =>
        _db.DealOnboardings.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<DealOnboarding>> ForDealAsync(Guid dealId, Guid organizationId, CancellationToken ct = default) =>
        await _db.DealOnboardings.AsNoTracking()
            .Where(o => o.OrganizationId == organizationId && o.DealId == dealId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, OutboundMessage>> MessagesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, OutboundMessage>();
        return await _db.OutboundMessages.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
    }

    public async Task<ClubMember?> ClubMemberForAsync(Guid organizationId, string email, Guid? contactId, Guid? leadId, CancellationToken ct = default)
    {
        if (contactId != null || leadId != null)
        {
            var linked = await _db.ClubMembers
                .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null
                    && ((contactId != null && m.ContactId == contactId) || (leadId != null && m.LeadId == leadId)))
                .OrderBy(m => m.JoinedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (linked != null) return linked;
        }
        var key = email.Trim().ToLower();
        return await _db.ClubMembers
            .Where(m => m.OrganizationId == organizationId && m.Email.ToLower() == key)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DealOnboarding?> LatestHandoffForAsync(Guid organizationId, Guid? leadId, Guid? clubMemberId, CancellationToken ct = default)
    {
        if (leadId == null && clubMemberId == null) return null;
        var dealOfLead = leadId == null ? null
            : await _db.Leads.Where(l => l.Id == leadId && l.OrganizationId == organizationId).Select(l => l.ConvertedToDealId).FirstOrDefaultAsync(ct);
        return await _db.DealOnboardings.AsNoTracking()
            .Where(o => o.OrganizationId == organizationId
                && ((leadId != null && o.LeadId == leadId) || (dealOfLead != null && o.DealId == dealOfLead) || (clubMemberId != null && o.ClubMemberId == clubMemberId)))
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(string Name, string Email)?> ReferrerOfAsync(Guid organizationId, Guid dealId, Guid? leadId, Guid? clubMemberId, CancellationToken ct = default)
    {
        var referredBy = await _db.Leads
            .Where(l => l.OrganizationId == organizationId && (leadId != null ? l.Id == leadId : l.ConvertedToDealId == dealId) && l.ReferredByContactId != null)
            .OrderByDescending(l => l.UpdatedAtUtc ?? l.CreatedAtUtc)
            .Select(l => l.ReferredByContactId)
            .FirstOrDefaultAsync(ct);
        if (referredBy != null)
        {
            var contact = await _db.Contacts
                .Where(c => c.Id == referredBy && c.OrganizationId == organizationId)
                .Select(c => new { c.Name, c.Email })
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(contact?.Email)) return (contact.Name.Trim(), contact.Email.Trim());
        }

        if (clubMemberId == null) return null;
        var member = await _db.ClubMembers
            .Where(m => m.Id == clubMemberId && m.OrganizationId == organizationId && m.ReferredByMemberId != null)
            .Select(m => m.ReferredByMemberId)
            .FirstOrDefaultAsync(ct);
        if (member == null) return null;
        var referrer = await _db.ClubMembers
            .Where(m => m.Id == member && m.OrganizationId == organizationId && m.ErasedAtUtc == null)
            .Select(m => new { m.FirstName, m.LastName, m.Email })
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(referrer?.Email) ? null : ($"{referrer.FirstName} {referrer.LastName}".Trim(), referrer.Email.Trim());
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Forget the stale copy, so a second try reads what is stored now.
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException();
        }
    }

    public void Forget(DealOnboarding onboarding) => _db.Entry(onboarding).State = EntityState.Detached;
}
