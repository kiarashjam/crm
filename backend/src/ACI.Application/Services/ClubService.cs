using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public record MemberDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone,
    string Tier, string Status, DateTime JoinedAtUtc, DateTime RenewsAtUtc,
    decimal DuesAmount, string DuesFrequency, decimal HouseAccountBalance, string? Notes);

public class MemberWriteRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Tier { get; set; }
    public string? Status { get; set; }
    public DateTime? JoinedAtUtc { get; set; }
    public DateTime? RenewsAtUtc { get; set; }
    public decimal? DuesAmount { get; set; }
    public string? DuesFrequency { get; set; }
    public decimal? HouseAccountBalance { get; set; }
    public string? Notes { get; set; }
}

/// <summary>The member to fold into the one named in the route.</summary>
public record MergeMembersRequest(Guid OtherMemberId);

public record VisitDto(
    Guid Id, Guid MemberId, string MemberName, string MemberTier, string Venue,
    DateTime VisitedAtUtc, int GuestCount, int PointsAwarded, string? Notes);

public class VisitWriteRequest
{
    public Guid MemberId { get; set; }
    public string? Venue { get; set; }
    public int GuestCount { get; set; }
    public int PointsAwarded { get; set; }
    public string? Notes { get; set; }
}

public record LoyaltyEntryDto(
    Guid Id, Guid MemberId, string MemberName, string Kind, string Reason, int Points, string? Note, DateTime CreatedAtUtc);

public class LoyaltyWriteRequest
{
    public Guid MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? Kind { get; set; }
    public string? Reason { get; set; }
    public int Points { get; set; }
    public string? Note { get; set; }
}

public interface IClubRepository
{
    Task<IReadOnlyList<ClubMember>> MembersAsync(Guid? organizationId, CancellationToken ct = default);
    Task<ClubMember?> MemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task SaveMemberAsync(ClubMember member, CancellationToken ct = default);
    Task<bool> DeleteMemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    /// <summary>
    /// Folds <paramref name="dropId"/> into <paramref name="keepId"/>: visits, loyalty points
    /// and every club record that names the member move to the member kept, the house
    /// accounts add up, empty fields are filled, and the duplicate is deleted.
    /// </summary>
    Task<bool> MergeMembersAsync(Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<ClubVisit>> VisitsAsync(Guid? organizationId, CancellationToken ct = default);
    Task AddVisitAsync(ClubVisit visit, CancellationToken ct = default);
    Task<bool> DeleteVisitAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyLedgerEntry>> LedgerAsync(Guid? organizationId, CancellationToken ct = default);
    Task AddLedgerAsync(LoyaltyLedgerEntry entry, CancellationToken ct = default);
    Task<bool> DeleteLedgerAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
}

public interface IClubService
{
    Task<IReadOnlyList<MemberDto>> MembersAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Result<MemberDto>> CreateMemberAsync(Guid userId, Guid? organizationId, MemberWriteRequest request, CancellationToken ct = default);
    Task<Result<MemberDto>> UpdateMemberAsync(Guid id, Guid? organizationId, MemberWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteMemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<Result> MergeMembersAsync(Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<VisitDto>> VisitsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<VisitDto>> CheckInAsync(Guid? organizationId, VisitWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteVisitAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<LoyaltyEntryDto>> AddLedgerAsync(Guid? organizationId, LoyaltyWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteLedgerAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
}

public sealed class ClubService : IClubService
{
    private readonly IClubRepository _club;
    public ClubService(IClubRepository club) => _club = club;

    public async Task<IReadOnlyList<MemberDto>> MembersAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _club.MembersAsync(organizationId, ct);
        return rows.Select(ToDto).OrderBy(m => m.LastName).ToList();
    }

    public async Task<Result<MemberDto>> CreateMemberAsync(Guid userId, Guid? organizationId, MemberWriteRequest request, CancellationToken ct = default)
    {
        var first = (request.FirstName ?? "").Trim();
        var last = (request.LastName ?? "").Trim();
        var email = (request.Email ?? "").Trim();
        if (first.Length == 0 || last.Length == 0 || email.Length == 0)
            return new Error("Member.NameRequired", "A member needs a first name, a last name, and an email.");
        if (MemberTooLong(request) is { } tooLong) return tooLong;
        if (request.DuesAmount < 0 || request.HouseAccountBalance is < -MaxMoney or > MaxMoney || request.DuesAmount > MaxMoney)
            return MoneyInvalid;
        var row = new ClubMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            FirstName = first,
            LastName = last,
            Email = email,
            Phone = request.Phone?.Trim(),
            Tier = string.IsNullOrWhiteSpace(request.Tier) ? "Bronze" : request.Tier.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim(),
            JoinedAtUtc = request.JoinedAtUtc ?? DateTime.UtcNow,
            RenewsAtUtc = request.RenewsAtUtc ?? DateTime.UtcNow.AddYears(1),
            DuesAmount = request.DuesAmount ?? 0,
            DuesFrequency = string.IsNullOrWhiteSpace(request.DuesFrequency) ? "Monthly" : request.DuesFrequency.Trim(),
            HouseAccountBalance = request.HouseAccountBalance ?? 0,
            Notes = request.Notes,
        };
        await _club.SaveMemberAsync(row, ct);
        return ToDto(row);
    }

    public async Task<Result<MemberDto>> UpdateMemberAsync(Guid id, Guid? organizationId, MemberWriteRequest request, CancellationToken ct = default)
    {
        var row = await _club.MemberAsync(id, organizationId, ct);
        if (row == null) return new Error("Member.NotFound", "That member was not found.");
        if (MemberTooLong(request) is { } tooLong) return tooLong;
        if (request.DuesAmount < 0 || request.HouseAccountBalance is < -MaxMoney or > MaxMoney || request.DuesAmount > MaxMoney)
            return MoneyInvalid;
        if (!string.IsNullOrWhiteSpace(request.FirstName)) row.FirstName = request.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LastName)) row.LastName = request.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email)) row.Email = request.Email.Trim();
        if (request.Phone != null) row.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.Tier)) row.Tier = request.Tier.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status)) row.Status = request.Status.Trim();
        if (request.JoinedAtUtc.HasValue) row.JoinedAtUtc = request.JoinedAtUtc.Value;
        if (request.RenewsAtUtc.HasValue) row.RenewsAtUtc = request.RenewsAtUtc.Value;
        if (request.DuesAmount.HasValue) row.DuesAmount = request.DuesAmount.Value;
        if (!string.IsNullOrWhiteSpace(request.DuesFrequency)) row.DuesFrequency = request.DuesFrequency.Trim();
        if (request.HouseAccountBalance.HasValue) row.HouseAccountBalance = request.HouseAccountBalance.Value;
        if (request.Notes != null) row.Notes = request.Notes;
        await _club.SaveMemberAsync(row, ct);
        return ToDto(row);
    }

    public async Task<Result> DeleteMemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        await _club.DeleteMemberAsync(id, organizationId, ct)
            ? Result.Success()
            : new Error("Member.NotFound", "That member was not found.");

    public async Task<Result> MergeMembersAsync(Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct = default) =>
        keepId != dropId && await _club.MergeMembersAsync(keepId, dropId, organizationId, ct)
            ? Result.Success()
            : new Error("Member.NotFound", "That member was not found.");

    public async Task<IReadOnlyList<VisitDto>> VisitsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _club.VisitsAsync(organizationId, ct);
        return rows.Select(v => new VisitDto(v.Id, v.MemberId, v.MemberName, v.MemberTier, v.Venue, v.VisitedAtUtc, v.GuestCount, v.PointsAwarded, v.Notes)).ToList();
    }

    public async Task<Result<VisitDto>> CheckInAsync(Guid? organizationId, VisitWriteRequest request, CancellationToken ct = default)
    {
        var member = await _club.MemberAsync(request.MemberId, organizationId, ct);
        if (member == null) return new Error("Member.NotFound", "That member was not found.");
        if (Longer(request.Venue, 32)) return TooLong("A venue", 32);
        var visit = new ClubVisit
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = $"{member.FirstName} {member.LastName}".Trim(),
            MemberTier = member.Tier,
            Venue = string.IsNullOrWhiteSpace(request.Venue) ? "Lounge" : request.Venue.Trim(),
            VisitedAtUtc = DateTime.UtcNow,
            GuestCount = Math.Max(0, request.GuestCount),
            PointsAwarded = Math.Max(0, request.PointsAwarded),
            Notes = request.Notes,
        };
        await _club.AddVisitAsync(visit, ct);
        return new VisitDto(visit.Id, visit.MemberId, visit.MemberName, visit.MemberTier, visit.Venue, visit.VisitedAtUtc, visit.GuestCount, visit.PointsAwarded, visit.Notes);
    }

    public async Task<Result> DeleteVisitAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        await _club.DeleteVisitAsync(id, organizationId, ct) ? Result.Success() : new Error("Visit.NotFound", "That visit was not found.");

    public async Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _club.LedgerAsync(organizationId, ct);
        return rows.Select(e => new LoyaltyEntryDto(e.Id, e.MemberId, e.MemberName, e.Kind, e.Reason, e.Points, e.Note, e.CreatedAtUtc)).ToList();
    }

    public async Task<Result<LoyaltyEntryDto>> AddLedgerAsync(Guid? organizationId, LoyaltyWriteRequest request, CancellationToken ct = default)
    {
        // Points go to a member of this workspace. A name alone used to be enough, which
        // let an entry point at any id at all.
        var member = await _club.MemberAsync(request.MemberId, organizationId, ct);
        if (member == null) return new Error("Member.NotFound", "That member was not found.");
        var name = $"{member.FirstName} {member.LastName}".Trim();
        if (Longer(request.Kind, 32)) return TooLong("A kind", 32);
        if (Longer(request.Reason, 40)) return TooLong("A reason", 40);
        var entry = new LoyaltyLedgerEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = request.MemberId,
            MemberName = name,
            Kind = string.IsNullOrWhiteSpace(request.Kind) ? "Earned" : request.Kind.Trim(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "ManualAdjustment" : request.Reason.Trim(),
            Points = request.Points,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _club.AddLedgerAsync(entry, ct);
        return new LoyaltyEntryDto(entry.Id, entry.MemberId, entry.MemberName, entry.Kind, entry.Reason, entry.Points, entry.Note, entry.CreatedAtUtc);
    }

    public async Task<Result> DeleteLedgerAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        await _club.DeleteLedgerAsync(id, organizationId, ct) ? Result.Success() : new Error("Loyalty.NotFound", "That entry was not found.");

    private const decimal MaxMoney = 1_000_000_000m;
    private static readonly Error MoneyInvalid = new("Member.MoneyInvalid", "Dues can't be negative, and amounts must be under 1,000,000,000.");

    private static bool Longer(string? value, int max) => (value?.Trim().Length ?? 0) > max;

    private static Error TooLong(string what, int max) => new("Club.TooLong", $"{what} can be at most {max} characters.");

    /// <summary>The column widths, checked here so a long value is a sentence, not a 500.</summary>
    private static Error? MemberTooLong(MemberWriteRequest r) =>
        Longer(r.FirstName, 100) ? TooLong("A first name", 100)
        : Longer(r.LastName, 100) ? TooLong("A last name", 100)
        : Longer(r.Email, 256) ? TooLong("An email", 256)
        : Longer(r.Phone, 64) ? TooLong("A phone number", 64)
        : Longer(r.Tier, 32) ? TooLong("A tier", 32)
        : Longer(r.Status, 32) ? TooLong("A status", 32)
        : Longer(r.DuesFrequency, 32) ? TooLong("A dues frequency", 32)
        : null;

    private static MemberDto ToDto(ClubMember m) => new(
        m.Id, m.FirstName, m.LastName, m.Email, m.Phone, m.Tier, m.Status,
        m.JoinedAtUtc, m.RenewsAtUtc, m.DuesAmount, m.DuesFrequency, m.HouseAccountBalance, m.Notes);
}
