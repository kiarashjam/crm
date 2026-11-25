using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <inheritdoc cref="ILeadToMemberCopy"/>
/// <remarks>
/// The member is marked first, by a statement that only finds them unmarked, inside the
/// transaction that then writes their tags and values: of two ways at once, the second waits for
/// the first and finds the member marked. If the copy fails, the mark goes with it.
/// </remarks>
public sealed class LeadToMemberCopyService : ILeadToMemberCopy
{
    private readonly AppDbContext _db;
    private readonly ILogger<LeadToMemberCopyService> _logger;

    public LeadToMemberCopyService(AppDbContext db, ILogger<LeadToMemberCopyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LeadToMemberCopyResult> CopyAsync(Guid organizationId, Guid memberId, Guid? leadId, Guid actorUserId, CancellationToken ct = default)
    {
        // Read and written as the workspace: the callers include a handoff and an approval made
        // by a countersignature, which may not act for it already.
        using var acting = _db.ActFor(organizationId, actorUserId);
        var member = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.Id == memberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null)
            .Select(m => new { m.LeadId, m.LeadDetailsCopiedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (member == null || member.LeadDetailsCopiedAtUtc != null) return LeadToMemberCopyResult.None;
        // The lead the caller names comes from a request: it counts only when it is this workspace's.
        if ((leadId ?? member.LeadId) is not Guid lead
            || !await _db.Leads.AnyAsync(l => l.Id == lead && l.OrganizationId == organizationId, ct))
            return LeadToMemberCopyResult.None;

        var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        // What this copy puts in the unit of work, taken out again if it fails: the context is the
        // caller's too, and their own tracked records must not go with it.
        var added = new List<object>();
        ClubMember? marked = null;
        try
        {
            var now = DateTime.UtcNow;
            var claim = await ClaimAsync(organizationId, memberId, now, ct);
            if (!claim.Taken)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                return LeadToMemberCopyResult.None;
            }
            marked = claim.Tracked;

            // The lead's tags the member does not carry yet.
            var leadTags = await _db.RecordTags.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.EntityType == RecordTypes.Lead && t.RecordId == lead)
                .Select(t => t.TagId).Distinct().ToListAsync(ct);
            var memberTags = await _db.RecordTags.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.EntityType == RecordTypes.Member && t.RecordId == memberId)
                .Select(t => t.TagId).ToListAsync(ct);
            var tags = leadTags.Except(memberTags).ToList();
            foreach (var tagId in tags)
            {
                var tag = new RecordTag
                {
                    Id = Guid.NewGuid(), OrganizationId = organizationId, OwnerUserId = actorUserId, TagId = tagId,
                    EntityType = RecordTypes.Member, RecordId = memberId, CreatedAtUtc = now,
                };
                _db.RecordTags.Add(tag);
                added.Add(tag);
            }

            // The lead's values, each to the member field with the same key, where the member has none.
            var definitions = await _db.CustomFieldDefinitions.AsNoTracking()
                .Where(d => d.OrganizationId == organizationId && (d.EntityType == RecordTypes.Lead || d.EntityType == RecordTypes.Member))
                .ToListAsync(ct);
            var memberFields = definitions.Where(d => d.EntityType == RecordTypes.Member)
                .GroupBy(d => d.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var leadFields = definitions.Where(d => d.EntityType == RecordTypes.Lead).ToDictionary(d => d.Id);
            var leadValues = await _db.CustomFieldEntries.AsNoTracking()
                .Where(e => e.OrganizationId == organizationId && e.EntityType == RecordTypes.Lead && e.RecordId == lead)
                .ToListAsync(ct);
            var memberHas = (await _db.CustomFieldEntries.AsNoTracking()
                    .Where(e => e.OrganizationId == organizationId && e.EntityType == RecordTypes.Member && e.RecordId == memberId)
                    .Select(e => e.FieldId).ToListAsync(ct))
                .ToHashSet();
            var fields = 0;
            foreach (var group in leadValues.GroupBy(e => e.FieldId))
            {
                if (!leadFields.TryGetValue(group.Key, out var from) || !memberFields.TryGetValue(from.Key, out var to) || memberHas.Contains(to.Id)) continue;
                var rows = LeadFieldCopy.Rows(from, group.ToList(), to);
                if (rows.Count == 0) continue;
                foreach (var row in rows)
                {
                    row.OrganizationId = organizationId;
                    row.OwnerUserId = actorUserId;
                    row.RecordId = memberId;
                    row.UpdatedAtUtc = now;
                    _db.CustomFieldEntries.Add(row);
                    added.Add(row);
                }
                fields++;
            }

            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            if (tags.Count > 0 || fields > 0)
                _logger.LogInformation("Member {MemberId} took {Tags} tags and {Fields} field values from lead {LeadId}", memberId, tags.Count, fields, lead);
            return new LeadToMemberCopyResult(true, tags.Count, fields);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(CancellationToken.None);
            foreach (var entity in added) _db.Entry(entity).State = EntityState.Detached;
            if (marked != null)
            {
                var mark = _db.Entry(marked).Property(m => m.LeadDetailsCopiedAtUtc);
                mark.CurrentValue = null;
                mark.IsModified = false;
            }
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Marks the member as having had their lead's details, unless they already have. On SQL
    /// Server by a statement inside the transaction; in the in-memory test host on the tracked
    /// member (returned, so a failed copy can take the mark back), saved with the copy.
    /// </summary>
    private async Task<(bool Taken, ClubMember? Tracked)> ClaimAsync(Guid organizationId, Guid memberId, DateTime nowUtc, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            var taken = await _db.ClubMembers
                .Where(m => m.Id == memberId && m.OrganizationId == organizationId && m.LeadDetailsCopiedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.LeadDetailsCopiedAtUtc, (DateTime?)nowUtc), ct) == 1;
            return (taken, null);
        }
        // One process, so no race.
        var member = await _db.ClubMembers.FirstAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member.LeadDetailsCopiedAtUtc != null) return (false, null);
        member.LeadDetailsCopiedAtUtc = nowUtc;
        return (true, member);
    }
}
