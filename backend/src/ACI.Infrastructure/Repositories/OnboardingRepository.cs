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

    public Task<ClubMember?> ClubMemberByEmailAsync(Guid organizationId, string email, CancellationToken ct = default)
    {
        var key = email.Trim().ToLower();
        return _db.ClubMembers
            .Where(m => m.OrganizationId == organizationId && m.Email.ToLower() == key)
            .OrderBy(m => m.JoinedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public void AddClubMember(ClubMember member) => _db.ClubMembers.Add(member);

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
