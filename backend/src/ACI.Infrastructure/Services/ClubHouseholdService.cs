using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>Households (see <see cref="IClubHouseholds"/> for what it offers and why).</summary>
/// <remarks>
/// Households were two kinds of JSON document the browser kept in step: a household, and one
/// "household-members" document per person, with nothing to stop a member being in two
/// households, a household having two primaries, or the roll-up switch doing anything. Here a
/// household and its people are rows, every change saves the household against the version it
/// was read at, and the unique indexes on SQL Server hold the rules whatever two requests do.
/// </remarks>
public sealed class ClubHouseholdService : IClubHouseholds
{
    private const int MaxName = 200;
    private const int MaxNotes = 2000;

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubBilling _billing;
    private readonly ICurrentUserService _user;
    private readonly IPermissionService _permissions;

    public ClubHouseholdService(AppDbContext db, IClubLedger ledger, IClubBilling billing, ICurrentUserService user, IPermissionService permissions)
    {
        _db = db;
        _ledger = ledger;
        _billing = billing;
        _user = user;
        _permissions = permissions;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HouseholdDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var households = await _db.ClubHouseholds.AsNoTracking().Where(h => h.OrganizationId == organizationId).ToListAsync(ct);
        var links = await _db.ClubHouseholdMembers.AsNoTracking().Where(l => l.OrganizationId == organizationId).ToListAsync(ct);
        return (await BuildAsync(organizationId, households, links, ct)).OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Result<HouseholdDto>> GetAsync(Guid organizationId, Guid householdId, CancellationToken ct = default)
    {
        var built = await BuildOneAsync(organizationId, householdId, ct);
        return built is null ? DomainErrors.Household.NotFound : built;
    }

    public async Task<HouseholdDto?> ForMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var householdId = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.MemberId == memberId)
            .Select(l => (Guid?)l.HouseholdId).FirstOrDefaultAsync(ct);
        return householdId is { } id ? await BuildOneAsync(organizationId, id, ct) : null;
    }

    public async Task<HouseholdDto?> ForContactAsync(Guid organizationId, Guid contactId, CancellationToken ct = default) =>
        (await ForContactsAsync(organizationId, [contactId], ct)).GetValueOrDefault(contactId);

    public async Task<IReadOnlyDictionary<Guid, HouseholdDto>> ForContactsAsync(Guid organizationId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
    {
        if (contactIds.Count == 0) return new Dictionary<Guid, HouseholdDto>();
        var ids = contactIds.Distinct().ToList();
        var direct = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.ContactId != null && ids.Contains(l.ContactId.Value))
            .Select(l => new { ContactId = l.ContactId!.Value, l.HouseholdId })
            .ToListAsync(ct);
        var throughMember = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.MemberId != null)
            .Join(_db.ClubMembers.Where(m => m.OrganizationId == organizationId && m.ContactId != null && ids.Contains(m.ContactId.Value)),
                l => l.MemberId, m => (Guid?)m.Id, (l, m) => new { ContactId = m.ContactId!.Value, l.HouseholdId })
            .ToListAsync(ct);
        var byContact = direct.Concat(throughMember)
            .GroupBy(x => x.ContactId)
            .ToDictionary(g => g.Key, g => g.First().HouseholdId);
        if (byContact.Count == 0) return new Dictionary<Guid, HouseholdDto>();
        var householdIds = byContact.Values.Distinct().ToList();
        var households = await _db.ClubHouseholds.AsNoTracking()
            .Where(h => h.OrganizationId == organizationId && householdIds.Contains(h.Id)).ToListAsync(ct);
        var links = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && householdIds.Contains(l.HouseholdId)).ToListAsync(ct);
        var built = (await BuildAsync(organizationId, households, links, ct)).ToDictionary(h => h.Id);
        return byContact.Where(p => built.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => built[p.Value]);
    }

    // ── changes ──────────────────────────────────────────────────────────────

    public async Task<Result<HouseholdDto>> CreateAsync(Guid organizationId, Guid userId, HouseholdWriteRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return DomainErrors.Household.NameRequired;
        if (name.Length > MaxName) return DomainErrors.Household.TooLong("A household name", MaxName);
        var notes = Trimmed(request.Notes);
        if (notes?.Length > MaxNotes) return DomainErrors.Household.TooLong("The notes", MaxNotes);

        var primary = await PersonAsync(organizationId, request.PrimaryMemberId, request.PrimaryContactId, ct);
        if (!primary.IsSuccess) return primary.Error;
        if (await HouseholdOfAsync(organizationId, primary.Value, ct) is { } taken)
            return DomainErrors.Household.AlreadyInHousehold(primary.Value.Name, taken.Name);

        var now = DateTime.UtcNow;
        var household = new ClubHousehold
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, Name = name, Notes = notes,
            CreatedAtUtc = now, CreatedByUserId = userId,
        };
        _db.ClubHouseholds.Add(household);
        _db.ClubHouseholdMembers.Add(Link(organizationId, household.Id, primary.Value, HouseholdRoles.Primary, isPrimary: true,
            rollUp: false, HouseholdLinkSources.Club, userId));
        var saved = await SaveAsync(ct, DomainErrors.Household.AlreadyInHousehold(primary.Value.Name, "another"));
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, household.Id, ct))!;
    }

    public async Task<Result<HouseholdDto>> UpdateAsync(Guid organizationId, Guid householdId, HouseholdWriteRequest request, CancellationToken ct = default)
    {
        var household = await TrackedAsync(organizationId, householdId, request.Version, ct);
        if (!household.IsSuccess) return household.Error;
        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0) return DomainErrors.Household.NameRequired;
            if (name.Length > MaxName) return DomainErrors.Household.TooLong("A household name", MaxName);
            household.Value.Name = name;
        }
        if (request.Notes != null)
        {
            var notes = Trimmed(request.Notes);
            if (notes?.Length > MaxNotes) return DomainErrors.Household.TooLong("The notes", MaxNotes);
            household.Value.Notes = notes;
        }
        household.Value.UpdatedAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(ct);
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result> DeleteAsync(Guid organizationId, Guid householdId, CancellationToken ct = default)
    {
        var household = await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
        if (household == null) return DomainErrors.Household.NotFound;
        // The grouping goes; the members, the contacts and every entry posted stay. Charges a
        // member ran up on the primary's account stay there, saying who ran them up.
        _db.ClubHouseholdMembers.RemoveRange(await _db.ClubHouseholdMembers
            .Where(l => l.HouseholdId == householdId && l.OrganizationId == organizationId).ToListAsync(ct));
        _db.ClubHouseholds.Remove(household);
        var saved = await SaveAsync(ct);
        return saved.IsSuccess ? Result.Success() : saved.Error;
    }

    public async Task<Result<HouseholdDto>> MergeAsync(Guid organizationId, Guid householdId, IReadOnlyCollection<Guid> duplicateIds, CancellationToken ct = default)
    {
        var keep = await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
        if (keep == null) return DomainErrors.Household.NotFound;
        var ids = duplicateIds.Where(id => id != householdId).Distinct().ToList();
        var duplicates = await _db.ClubHouseholds.Where(h => h.OrganizationId == organizationId && ids.Contains(h.Id)).ToListAsync(ct);
        if (duplicates.Count == 0) return (await BuildOneAsync(organizationId, householdId, ct))!;
        var kept = await LinksAsync(organizationId, householdId, ct, tracked: true);
        var keepPrimaryIsMember = kept.FirstOrDefault(l => l.IsPrimary)?.MemberId != null;
        var hasCouple = kept.Any(l => HouseholdRoles.IsCouple(l.Role));
        var notes = keep.Notes ?? "";
        foreach (var duplicate in duplicates.OrderBy(d => d.CreatedAtUtc))
        {
            foreach (var link in (await LinksAsync(organizationId, duplicate.Id, ct, tracked: true)).OrderByDescending(l => l.IsPrimary).ThenBy(l => l.CreatedAtUtc))
            {
                // A person is in one household: moving the link moves them, and nobody is in it twice.
                var already = kept.Any(k => (k.MemberId != null && k.MemberId == link.MemberId) || (k.ContactId != null && k.ContactId == link.ContactId));
                if (already)
                {
                    _db.ClubHouseholdMembers.Remove(link);
                    continue;
                }
                link.HouseholdId = householdId;
                if (link.IsPrimary)
                {
                    link.IsPrimary = false;
                    link.Role = HouseholdRoles.Other;
                }
                else if (HouseholdRoles.IsCouple(link.Role) && hasCouple) link.Role = HouseholdRoles.Other;
                if (HouseholdRoles.IsCouple(link.Role)) hasCouple = true;
                if (!keepPrimaryIsMember) link.RollUpBilling = false;
                link.UpdatedAtUtc = DateTime.UtcNow;
                kept.Add(link);
            }
            var text = duplicate.Notes?.Trim();
            if (!string.IsNullOrEmpty(text) && !notes.Contains(text, StringComparison.Ordinal))
                notes = string.IsNullOrWhiteSpace(notes) ? text : $"{notes.TrimEnd()}\n\n{text}";
            _db.ClubHouseholds.Remove(duplicate);
        }
        keep.Notes = notes.Length == 0 ? null : notes.Length > MaxNotes ? notes[..MaxNotes] : notes;
        keep.UpdatedAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(ct);
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result<HouseholdDto>> AddPersonAsync(Guid organizationId, Guid householdId, Guid userId, HouseholdPersonRequest request, CancellationToken ct = default)
    {
        var household = await TrackedAsync(organizationId, householdId, request.Version, ct);
        if (!household.IsSuccess) return household.Error;
        var person = await PersonAsync(organizationId, request.MemberId, request.ContactId, ct);
        if (!person.IsSuccess) return person.Error;
        var role = HouseholdRoles.Normalize(request.Role ?? HouseholdRoles.Other);
        if (role is null or HouseholdRoles.Primary) return DomainErrors.Household.RoleInvalid;
        if (await HouseholdOfAsync(organizationId, person.Value, ct) is { } taken)
            return DomainErrors.Household.AlreadyInHousehold(person.Value.Name, taken.Name);
        var links = await LinksAsync(organizationId, householdId, ct);
        if (HouseholdRoles.IsCouple(role) && await CoupleAsync(organizationId, links, null, ct) is { } partner)
            return DomainErrors.Household.CoupleTaken(household.Value.Name, partner);
        var rollUp = request.RollUpBilling == true;
        if (rollUp && await RollUpRefusalAsync(organizationId, person.Value, links, ct) is { } refused) return refused;

        _db.ClubHouseholdMembers.Add(Link(organizationId, householdId, person.Value, role, isPrimary: false, rollUp, HouseholdLinkSources.Club, userId));
        household.Value.UpdatedAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(ct, DomainErrors.Household.AlreadyInHousehold(person.Value.Name, "another"));
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result<HouseholdDto>> UpdatePersonAsync(Guid organizationId, Guid householdId, Guid linkId, HouseholdPersonRequest request, CancellationToken ct = default)
    {
        var household = await TrackedAsync(organizationId, householdId, request.Version, ct);
        if (!household.IsSuccess) return household.Error;
        var links = await LinksAsync(organizationId, householdId, ct, tracked: true);
        var link = links.FirstOrDefault(l => l.Id == linkId);
        if (link == null) return DomainErrors.Household.PersonNotFound;

        if (request.Role != null)
        {
            var role = HouseholdRoles.Normalize(request.Role);
            if (role == null) return DomainErrors.Household.RoleInvalid;
            if (link.IsPrimary && role != HouseholdRoles.Primary) return DomainErrors.Household.PrimaryCannotLeave;
            if (!link.IsPrimary && role == HouseholdRoles.Primary) return DomainErrors.Household.RoleInvalid;
            if (HouseholdRoles.IsCouple(role) && await CoupleAsync(organizationId, links, link.Id, ct) is { } partner)
                return DomainErrors.Household.CoupleTaken(household.Value.Name, partner);
            link.Role = role;
        }
        if (request.RollUpBilling is { } rollUp && rollUp != link.RollUpBilling)
        {
            if (link.IsPrimary) return DomainErrors.Household.RollUpForPrimary;
            var person = await PersonOfLinkAsync(organizationId, link, ct);
            // Switching it off is money too: from then on the member's bills are theirs again.
            if (await MayNotHandleMoneyAsync(organizationId, ct) is { } noMoney) return noMoney;
            if (rollUp && await RollUpRefusalAsync(organizationId, person, links, ct, checkPermission: false) is { } refused) return refused;
            link.RollUpBilling = rollUp;
        }
        link.UpdatedAtUtc = DateTime.UtcNow;
        household.Value.UpdatedAtUtc = link.UpdatedAtUtc;
        var saved = await SaveAsync(ct);
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result<HouseholdDto>> RemovePersonAsync(Guid organizationId, Guid householdId, Guid linkId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var household = await TrackedAsync(organizationId, householdId, expectedVersion, ct);
        if (!household.IsSuccess) return household.Error;
        var link = await _db.ClubHouseholdMembers.FirstOrDefaultAsync(l => l.Id == linkId && l.HouseholdId == householdId && l.OrganizationId == organizationId, ct);
        if (link == null) return DomainErrors.Household.PersonNotFound;
        if (link.IsPrimary) return DomainErrors.Household.PrimaryCannotLeave;
        _db.ClubHouseholdMembers.Remove(link);
        household.Value.UpdatedAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(ct);
        if (!saved.IsSuccess) return saved.Error;
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result<HouseholdDto>> SetPrimaryAsync(Guid organizationId, Guid householdId, Guid linkId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var household = await TrackedAsync(organizationId, householdId, expectedVersion, ct);
        if (!household.IsSuccess) return household.Error;
        var links = await LinksAsync(organizationId, householdId, ct, tracked: true);
        var next = links.FirstOrDefault(l => l.Id == linkId);
        if (next == null) return DomainErrors.Household.PersonNotFound;
        if (next.IsPrimary) return (await BuildOneAsync(organizationId, householdId, ct))!;
        var previous = links.FirstOrDefault(l => l.IsPrimary);
        var now = DateTime.UtcNow;

        // Roll-up bills the primary, so whose bills roll up changes with them: the new primary's
        // stop rolling up (they are theirs), and the old primary's start nothing on their own.
        var newRole = next.Role;
        next.UpdatedAtUtc = now;
        household.Value.UpdatedAtUtc = now;
        // Two saves in one transaction: the old primary steps down before the new one steps up,
        // so the one-primary index never sees two at once.
        var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            if (previous != null)
            {
                previous.IsPrimary = false;
                previous.Role = HouseholdRoles.InverseOf(newRole);
                previous.RollUpBilling = false;
                previous.UpdatedAtUtc = now;
            }
            var first = await SaveAsync(ct);
            if (!first.IsSuccess) return first.Error;
            next.IsPrimary = true;
            next.Role = HouseholdRoles.Primary;
            next.RollUpBilling = false;
            household.Value.UpdatedAtUtc = now.AddTicks(1);
            var second = await SaveAsync(ct);
            if (!second.IsSuccess) return second.Error;
            if (transaction != null) await transaction.CommitAsync(ct);
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
        return (await BuildOneAsync(organizationId, householdId, ct))!;
    }

    public async Task<Result<StatementFile>> StatementPdfAsync(Guid organizationId, Guid householdId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(5 * 366)) return DomainErrors.Household.PeriodInvalid;
        var household = await BuildOneAsync(organizationId, householdId, ct);
        if (household == null) return DomainErrors.Household.NotFound;
        if (household.PrimaryMemberId is not { } primaryId) return DomainErrors.Household.RollUpNeedsMemberPrimary;
        var others = household.People
            .Where(p => p is { IsPrimary: false, MemberId: not null })
            .Select(p => new HouseholdStatementMember(p.MemberId!.Value, p.Name, p.Role, p.RollUpBilling))
            .ToList();
        return await _billing.HouseholdStatementPdfAsync(organizationId, primaryId, household.Name, others, fromUtc, toUtc, ct);
    }

    // ── the contact pages' relationships card ─────────────────────────────────

    public async Task<Result<Guid>> RelateContactsAsync(Guid organizationId, Guid userId, Guid contactId, Guid relatedContactId, string type, CancellationToken ct = default)
    {
        var viewer = await ContactPersonAsync(organizationId, contactId, ct);
        var other = await ContactPersonAsync(organizationId, relatedContactId, ct);
        if (viewer == null || other == null) return DomainErrors.Link.ContactNotFound;
        var role = RoleForType(type);
        var couple = ContactRelationshipTypes.IsCouple(type);
        var mine = await HouseholdOfAsync(organizationId, viewer, ct);
        var theirs = await HouseholdOfAsync(organizationId, other, ct);
        var now = DateTime.UtcNow;

        if (mine == null && theirs == null)
        {
            // Only a couple makes a household: other kin who live apart stay a relationship.
            if (!couple || role == null) return DomainErrors.ContactRelationship.InvalidType;
            var household = new ClubHousehold
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, Name = Surname(viewer.Name),
                CreatedAtUtc = now, CreatedByUserId = userId,
            };
            _db.ClubHouseholds.Add(household);
            _db.ClubHouseholdMembers.Add(Link(organizationId, household.Id, viewer, HouseholdRoles.Primary, true, false, HouseholdLinkSources.Crm, userId));
            var tie = Link(organizationId, household.Id, other, role, false, false, HouseholdLinkSources.Crm, userId);
            _db.ClubHouseholdMembers.Add(tie);
            var saved = await SaveAsync(ct, DomainErrors.Household.AlreadyInHousehold(other.Name, "another"));
            return saved.IsSuccess ? tie.Id : saved.Error;
        }

        if (mine != null && theirs != null && mine.Id != theirs.Id)
            return DomainErrors.Household.OtherHousehold(other.Name, theirs.Name);

        if (mine == null || theirs == null)
        {
            // One of them joins the other's household, as the primary's spouse or partner.
            if (!couple || role == null) return DomainErrors.ContactRelationship.InvalidType;
            var (host, joiner) = mine != null ? (mine, other) : (theirs!, viewer);
            var hostPerson = mine != null ? viewer : other;
            var links = await LinksAsync(organizationId, host.Id, ct);
            var primary = links.FirstOrDefault(l => l.IsPrimary);
            if (primary == null || !await IsPersonAsync(organizationId, primary, hostPerson, ct))
                return DomainErrors.Household.PrimaryDecides(hostPerson.Name, host.Name, await PrimaryNameAsync(organizationId, primary, ct));
            if (await CoupleAsync(organizationId, links, null, ct) is { } partner)
                return DomainErrors.Household.CoupleTaken(host.Name, partner);
            var tracked = await TrackedAsync(organizationId, host.Id, null, ct);
            if (!tracked.IsSuccess) return tracked.Error;
            var tie = Link(organizationId, host.Id, joiner, role, false, false, HouseholdLinkSources.Crm, userId);
            _db.ClubHouseholdMembers.Add(tie);
            tracked.Value.UpdatedAtUtc = now;
            var saved = await SaveAsync(ct, DomainErrors.Household.AlreadyInHousehold(joiner.Name, "another"));
            return saved.IsSuccess ? tie.Id : saved.Error;
        }

        // Both in the same household: the tie is the role of whichever is not the primary.
        if (role == null) return DomainErrors.Household.SameHousehold(other.Name);
        var sameLinks = await LinksAsync(organizationId, mine.Id, ct, tracked: true);
        var viewerLink = await LinkOfAsync(organizationId, sameLinks, viewer, ct);
        var otherLink = await LinkOfAsync(organizationId, sameLinks, other, ct);
        if (viewerLink == null || otherLink == null) return DomainErrors.Household.PersonNotFound;
        ClubHouseholdMember holder;
        string newRole;
        if (viewerLink.IsPrimary) (holder, newRole) = (otherLink, role);
        else if (otherLink.IsPrimary) (holder, newRole) = (viewerLink, RoleForType(InverseType(type)) ?? HouseholdRoles.Other);
        else return DomainErrors.Household.PrimaryDecides(viewer.Name, mine.Name, await PrimaryNameAsync(organizationId, sameLinks.FirstOrDefault(l => l.IsPrimary), ct));
        if (HouseholdRoles.IsCouple(newRole) && await CoupleAsync(organizationId, sameLinks, holder.Id, ct) is { } taken)
            return DomainErrors.Household.CoupleTaken(mine.Name, taken);
        var household2 = await TrackedAsync(organizationId, mine.Id, null, ct);
        if (!household2.IsSuccess) return household2.Error;
        holder.Role = newRole;
        holder.UpdatedAtUtc = now;
        household2.Value.UpdatedAtUtc = now;
        var changed = await SaveAsync(ct);
        return changed.IsSuccess ? holder.Id : changed.Error;
    }

    public async Task<Result> UnrelateAsync(Guid organizationId, Guid contactId, Guid linkId, CancellationToken ct = default)
    {
        var viewer = await ContactPersonAsync(organizationId, contactId, ct);
        if (viewer == null) return DomainErrors.Link.ContactNotFound;
        var link = await _db.ClubHouseholdMembers.AsNoTracking().FirstOrDefaultAsync(l => l.Id == linkId && l.OrganizationId == organizationId, ct);
        if (link == null) return DomainErrors.ContactRelationship.NotFound;
        var links = await LinksAsync(organizationId, link.HouseholdId, ct);
        var viewerLink = await LinkOfAsync(organizationId, links, viewer, ct);
        if (viewerLink == null) return DomainErrors.ContactRelationship.NotFound;
        var household = await _db.ClubHouseholds.AsNoTracking().FirstAsync(h => h.Id == link.HouseholdId, ct);

        // Whichever of the two is not the primary leaves; a tie between two people neither of
        // whom is the primary is the primary's to change.
        Guid leaving;
        if (viewerLink.Id == link.Id)
        {
            if (viewerLink.IsPrimary) return DomainErrors.Household.PrimaryCannotLeave;
            leaving = viewerLink.Id;
        }
        else if (viewerLink.IsPrimary) leaving = link.Id;
        else if (link.IsPrimary) leaving = viewerLink.Id;
        else return DomainErrors.Household.PrimaryDecides(viewer.Name, household.Name, await PrimaryNameAsync(organizationId, links.FirstOrDefault(l => l.IsPrimary), ct));
        var removed = await RemovePersonAsync(organizationId, link.HouseholdId, leaving, null, ct);
        return removed.IsSuccess ? Result.Success() : removed.Error;
    }

    // ── building the answer ──────────────────────────────────────────────────

    private async Task<HouseholdDto?> BuildOneAsync(Guid organizationId, Guid householdId, CancellationToken ct)
    {
        var household = await _db.ClubHouseholds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
        if (household == null) return null;
        var links = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.HouseholdId == householdId && l.OrganizationId == organizationId).ToListAsync(ct);
        return (await BuildAsync(organizationId, [household], links, ct)).Single();
    }

    private async Task<List<HouseholdDto>> BuildAsync(Guid organizationId, IReadOnlyList<ClubHousehold> households, IReadOnlyList<ClubHouseholdMember> links, CancellationToken ct)
    {
        var memberIds = links.Where(l => l.MemberId != null).Select(l => l.MemberId!.Value).Distinct().ToList();
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && memberIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var contactIds = links.Where(l => l.ContactId != null).Select(l => l.ContactId!.Value)
            .Concat(members.Values.Where(m => m.ContactId != null).Select(m => m.ContactId!.Value)).Distinct().ToList();
        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && contactIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Email, c.IsArchived, c.ConvertedFromLeadId })
            .ToDictionaryAsync(c => c.Id, ct);
        var balances = memberIds.Count == 0 ? new Dictionary<Guid, decimal>() : await _ledger.BalancesAsync(organizationId, null, ct);
        var currency = await CurrencyAsync(organizationId, ct);

        var result = new List<HouseholdDto>(households.Count);
        foreach (var household in households)
        {
            var people = new List<HouseholdPersonDto>();
            foreach (var link in links.Where(l => l.HouseholdId == household.Id).OrderByDescending(l => l.IsPrimary).ThenBy(l => l.CreatedAtUtc).ThenBy(l => l.Id))
            {
                var member = link.MemberId is { } mid ? members.GetValueOrDefault(mid) : null;
                // A member's link reads the member's own contact link, so the two cannot disagree.
                var contactId = member?.ContactId ?? link.ContactId;
                var contact = contactId is { } cid ? contacts.GetValueOrDefault(cid) : null;
                if (member == null && contact == null) continue; // a person deleted since
                people.Add(new HouseholdPersonDto
                {
                    LinkId = link.Id,
                    MemberId = member?.Id,
                    ContactId = contact?.Id,
                    LeadId = member?.LeadId ?? contact?.ConvertedFromLeadId,
                    Name = member != null ? $"{member.FirstName} {member.LastName}".Trim() : contact!.Name,
                    Email = member != null ? NullIfEmpty(member.Email) : NullIfEmpty(contact!.Email),
                    Role = link.Role,
                    IsPrimary = link.IsPrimary,
                    RollUpBilling = link.RollUpBilling,
                    Tier = member?.Tier,
                    MemberStatus = member?.Status,
                    DuesAmount = member?.DuesAmount,
                    DuesFrequency = member?.DuesFrequency,
                    HouseBalance = member != null ? balances.GetValueOrDefault(member.Id) : null,
                    IsArchived = member == null && contact!.IsArchived,
                    Source = link.Source,
                    AddedAtUtc = link.CreatedAtUtc,
                });
            }
            var primary = people.FirstOrDefault(p => p.IsPrimary);
            result.Add(new HouseholdDto
            {
                Id = household.Id,
                Name = household.Name,
                Notes = household.Notes,
                CreatedAtUtc = household.CreatedAtUtc,
                PrimaryLinkId = primary?.LinkId,
                PrimaryMemberId = primary?.MemberId,
                PrimaryName = primary?.Name,
                People = people,
                Members = people.Count(p => p.MemberId != null),
                Currency = currency,
                Version = household.Version,
            });
        }
        return result;
    }

    // ── people ───────────────────────────────────────────────────────────────

    /// <summary>A person as a household holds them: a member (with their contact, when linked), or a contact who is not a member.</summary>
    private sealed record Person(Guid? MemberId, Guid? ContactId, string Name);

    /// <summary>The member or contact a request names, in the workspace. A contact who is a member is the member.</summary>
    private async Task<Result<Person>> PersonAsync(Guid organizationId, Guid? memberId, Guid? contactId, CancellationToken ct)
    {
        if (memberId is { } mid)
        {
            var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mid && m.OrganizationId == organizationId, ct);
            if (member == null) return DomainErrors.Club.MemberNotFound;
            return new Person(member.Id, member.ContactId, $"{member.FirstName} {member.LastName}".Trim());
        }
        if (contactId is { } cid)
        {
            var person = await ContactPersonAsync(organizationId, cid, ct);
            return person == null ? DomainErrors.Link.ContactNotFound : person;
        }
        return DomainErrors.Household.PersonRequired;
    }

    /// <summary>A contact as a household holds them: through their member when one is linked to them.</summary>
    private async Task<Person?> ContactPersonAsync(Guid organizationId, Guid contactId, CancellationToken ct)
    {
        var contact = await _db.Contacts.AsNoTracking()
            .Where(c => c.Id == contactId && c.OrganizationId == organizationId)
            .Select(c => new { c.Id, c.Name }).FirstOrDefaultAsync(ct);
        if (contact == null) return null;
        var member = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.ContactId == contactId)
            .OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.FirstName, m.LastName }).FirstOrDefaultAsync(ct);
        return member != null
            ? new Person(member.Id, contact.Id, $"{member.FirstName} {member.LastName}".Trim())
            : new Person(null, contact.Id, contact.Name);
    }

    private async Task<Person> PersonOfLinkAsync(Guid organizationId, ClubHouseholdMember link, CancellationToken ct)
    {
        var person = await PersonAsync(organizationId, link.MemberId, link.MemberId == null ? link.ContactId : null, ct);
        return person.IsSuccess ? person.Value : new Person(link.MemberId, link.ContactId, "This person");
    }

    /// <summary>The household the person is in: as their member, or as their contact (directly or through any member linked to it).</summary>
    private async Task<ClubHousehold?> HouseholdOfAsync(Guid organizationId, Person person, CancellationToken ct)
    {
        var links = _db.ClubHouseholdMembers.AsNoTracking().Where(l => l.OrganizationId == organizationId);
        Guid? householdId = null;
        if (person.MemberId is { } memberId)
            householdId = await links.Where(l => l.MemberId == memberId).Select(l => (Guid?)l.HouseholdId).FirstOrDefaultAsync(ct);
        if (householdId == null && person.ContactId is { } contactId)
        {
            householdId = await links.Where(l => l.ContactId == contactId).Select(l => (Guid?)l.HouseholdId).FirstOrDefaultAsync(ct)
                ?? await links.Join(_db.ClubMembers.Where(m => m.OrganizationId == organizationId && m.ContactId == contactId),
                        l => l.MemberId, m => (Guid?)m.Id, (l, _) => (Guid?)l.HouseholdId)
                    .FirstOrDefaultAsync(ct);
        }
        return householdId is { } id
            ? await _db.ClubHouseholds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id && h.OrganizationId == organizationId, ct)
            : null;
    }

    /// <summary>The link in <paramref name="links"/> that is <paramref name="person"/>.</summary>
    private async Task<ClubHouseholdMember?> LinkOfAsync(Guid organizationId, IReadOnlyList<ClubHouseholdMember> links, Person person, CancellationToken ct)
    {
        foreach (var link in links)
            if (await IsPersonAsync(organizationId, link, person, ct)) return link;
        return null;
    }

    private async Task<bool> IsPersonAsync(Guid organizationId, ClubHouseholdMember link, Person person, CancellationToken ct)
    {
        if (person.MemberId != null && link.MemberId == person.MemberId) return true;
        if (person.ContactId is not { } contactId) return false;
        if (link.ContactId == contactId) return true;
        return link.MemberId is { } memberId
            && await _db.ClubMembers.AsNoTracking().AnyAsync(m => m.Id == memberId && m.OrganizationId == organizationId && m.ContactId == contactId, ct);
    }

    private async Task<List<ClubHouseholdMember>> LinksAsync(Guid organizationId, Guid householdId, CancellationToken ct, bool tracked = false)
    {
        var query = _db.ClubHouseholdMembers.Where(l => l.HouseholdId == householdId && l.OrganizationId == organizationId);
        return tracked ? await query.ToListAsync(ct) : await query.AsNoTracking().ToListAsync(ct);
    }

    /// <summary>The name of the household's spouse or partner other than <paramref name="exceptLinkId"/>, or null when it has none.</summary>
    private async Task<string?> CoupleAsync(Guid organizationId, IReadOnlyList<ClubHouseholdMember> links, Guid? exceptLinkId, CancellationToken ct)
    {
        var couple = links.FirstOrDefault(l => HouseholdRoles.IsCouple(l.Role) && l.Id != exceptLinkId);
        return couple == null ? null : (await PersonOfLinkAsync(organizationId, couple, ct)).Name;
    }

    private async Task<string> PrimaryNameAsync(Guid organizationId, ClubHouseholdMember? primary, CancellationToken ct) =>
        primary == null ? "nobody" : (await PersonOfLinkAsync(organizationId, primary, ct)).Name;

    /// <summary>Why a person's bills cannot roll up to this household's primary, or null when they can.</summary>
    private async Task<Error?> RollUpRefusalAsync(Guid organizationId, Person person, IReadOnlyList<ClubHouseholdMember> links, CancellationToken ct,
        bool checkPermission = true)
    {
        if (checkPermission && await MayNotHandleMoneyAsync(organizationId, ct) is { } noMoney) return noMoney;
        if (person.MemberId == null) return DomainErrors.Household.RollUpNeedsMember(person.Name);
        if (links.FirstOrDefault(l => l.IsPrimary)?.MemberId == null) return DomainErrors.Household.RollUpNeedsMemberPrimary;
        return null;
    }

    private static ClubHouseholdMember Link(Guid organizationId, Guid householdId, Person person, string role, bool isPrimary, bool rollUp, string source, Guid? userId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        HouseholdId = householdId,
        MemberId = person.MemberId,
        // A member's contact is read from the member, so only someone who is not a member keeps one here.
        ContactId = person.MemberId == null ? person.ContactId : null,
        Role = role,
        IsPrimary = isPrimary,
        RollUpBilling = rollUp && !isPrimary,
        Source = source,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedByUserId = userId,
    };

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>The household, tracked, refused when the change was made from an older version than it has now.</summary>
    private async Task<Result<ClubHousehold>> TrackedAsync(Guid organizationId, Guid householdId, Guid? expectedVersion, CancellationToken ct)
    {
        var household = await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
        if (household == null) return DomainErrors.Household.NotFound;
        if (expectedVersion is { } version && version != Guid.Empty && version != household.Version) return DomainErrors.Household.Changed;
        return household;
    }

    /// <summary>
    /// Saves, answering <see cref="DomainErrors.Household.Changed"/> when the household moved on
    /// meanwhile and <paramref name="taken"/> when a unique index refused the change (a person
    /// put in another household, or a second primary, by someone else at the same moment).
    /// </summary>
    private async Task<Result> SaveAsync(CancellationToken ct, Error? taken = null)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return DomainErrors.Household.Changed;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            Forget();
            return taken ?? DomainErrors.Household.Changed;
        }
    }

    private void Forget()
    {
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            entry.State = EntityState.Detached;
    }

    /// <summary>Roll-up billing is money: who pays a member's bills. Null when the caller may handle money.</summary>
    private async Task<Error?> MayNotHandleMoneyAsync(Guid organizationId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return DomainErrors.Permission.Missing(MemberPermissions.ManageMoney);
        var held = await _permissions.PermissionsAsync(userId, organizationId, ct);
        return held.Contains(MemberPermissions.ManageMoney)
            ? null
            : DomainErrors.Permission.Missing(MemberPermissions.ManageMoney, "change whose account a member's bills go on");
    }

    private async Task<string> CurrencyAsync(Guid organizationId, CancellationToken ct)
    {
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    /// <summary>A household role for a relationship type recorded on a contact's page; null for a tie a household does not hold.</summary>
    internal static string? RoleForType(string type) => type switch
    {
        ContactRelationshipTypes.Spouse => HouseholdRoles.Spouse,
        ContactRelationshipTypes.Partner => HouseholdRoles.Partner,
        ContactRelationshipTypes.Child => HouseholdRoles.Child,
        ContactRelationshipTypes.Parent => HouseholdRoles.Parent,
        ContactRelationshipTypes.Sibling => HouseholdRoles.Sibling,
        ContactRelationshipTypes.Family => HouseholdRoles.Other,
        _ => null,
    };

    private static string InverseType(string type) => ContactRelationshipTypes.Inverse(type);

    /// <summary>A household's name from a person's: their surname, the last word of it.</summary>
    private static string Surname(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var surname = parts.Length == 0 ? "Household" : parts[^1];
        return surname.Length > MaxName ? surname[..MaxName] : surname;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
