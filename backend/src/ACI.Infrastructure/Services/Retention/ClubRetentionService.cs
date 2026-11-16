using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Retention;

/// <summary>Members' at-risk scores, the save actions, concerns and the club's rules (see <see cref="IClubRetention"/>).</summary>
/// <remarks>
/// This part holds the rules and the concerns; the scoring and its daily run are in
/// ClubRetentionService.Scoring.cs, and what a crossing into at risk sets off in
/// ClubRetentionService.SaveActions.cs.
/// </remarks>
public sealed partial class ClubRetentionService : IClubRetention
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ITaskService _tasks;
    private readonly IOutbox _outbox;
    private readonly IOrganizationRepository _organizations;
    private readonly IClubRenewals _renewals;
    private readonly ILogger<ClubRetentionService> _logger;
    private readonly IAutomationEngine? _automations;

    public ClubRetentionService(AppDbContext db, ITaskService tasks, IOutbox outbox, IOrganizationRepository organizations, IClubRenewals renewals,
        ILogger<ClubRetentionService> logger, IAutomationEngine? automations = null)
    {
        _db = db;
        _tasks = tasks;
        _outbox = outbox;
        _organizations = organizations;
        _renewals = renewals;
        _logger = logger;
        _automations = automations;
    }

    // ── the club's rules ─────────────────────────────────────────────────────

    public async Task<RetentionSettingsDto> SettingsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await _db.ClubRetentionSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        var (currency, _, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        return ToDto(row ?? new ClubRetentionSettings { OrganizationId = organizationId }, row != null, currency);
    }

    public async Task<Result<RetentionSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, RetentionSettingsRequest r, Guid? expectedVersion,
        CancellationToken ct = default)
    {
        var row = await _db.ClubRetentionSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (row != null && expectedVersion is { } version && version != Guid.Empty && version != row.Version) return DomainErrors.Retention.Changed;
        // A field left out keeps what is saved (or the default before the first save), so a page
        // that sends only the referral settings does not turn the save actions back off.
        var next = row == null ? new ClubRetentionSettings { OrganizationId = organizationId } : Copy(row);
        next.VisitWeight = r.VisitWeight ?? next.VisitWeight;
        next.SpendWeight = r.SpendWeight ?? next.SpendWeight;
        next.BalanceWeight = r.BalanceWeight ?? next.BalanceWeight;
        next.EventWeight = r.EventWeight ?? next.EventWeight;
        next.ConcernWeight = r.ConcernWeight ?? next.ConcernWeight;
        next.TenureWeight = r.TenureWeight ?? next.TenureWeight;
        next.AtRiskThreshold = r.AtRiskThreshold ?? next.AtRiskThreshold;
        next.WatchThreshold = r.WatchThreshold ?? next.WatchThreshold;
        next.SaveTask = r.SaveTask ?? next.SaveTask;
        next.SaveTaskDueDays = r.SaveTaskDueDays ?? next.SaveTaskDueDays;
        next.SaveEmail = r.SaveEmail ?? next.SaveEmail;
        next.SaveEmailPurpose = r.SaveEmailPurpose?.Trim().ToLowerInvariant() ?? next.SaveEmailPurpose;
        // Sent empty, the club's own words go and the standard ones (in the member's language) are used.
        if (r.SaveEmailSubject != null) next.SaveEmailSubject = string.IsNullOrWhiteSpace(r.SaveEmailSubject) ? null : r.SaveEmailSubject.Trim();
        if (r.SaveEmailBody != null) next.SaveEmailBody = string.IsNullOrWhiteSpace(r.SaveEmailBody) ? null : r.SaveEmailBody.Trim();
        next.SaveCooldownDays = r.SaveCooldownDays ?? next.SaveCooldownDays;
        next.ReferralBonus = r.ReferralBonus?.Trim().ToLowerInvariant() ?? next.ReferralBonus;
        next.ReferralBonusPoints = r.ReferralBonusPoints ?? next.ReferralBonusPoints;
        next.ReferralBonusAmount = r.ReferralBonusAmount is { } amount ? Math.Round(amount, 2, MidpointRounding.AwayFromZero) : next.ReferralBonusAmount;
        next.SponsorRule = r.SponsorRule?.Trim().ToLowerInvariant() ?? next.SponsorRule;
        next.SponsorMinTenureMonths = r.SponsorMinTenureMonths ?? next.SponsorMinTenureMonths;
        next.SponsorNoOverdue = r.SponsorNoOverdue ?? next.SponsorNoOverdue;
        if (Validate(next) is { } invalid) return invalid;

        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = next;
            row.Id = Guid.NewGuid();
            row.CreatedAtUtc = now;
            _db.ClubRetentionSettings.Add(row);
        }
        else Apply(next, row);
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two first saves at once (one row per workspace), or a save over a newer one.
            _db.ChangeTracker.Clear();
            return DomainErrors.Retention.Changed;
        }
        var (currency, _, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        return ToDto(row, true, currency);
    }

    /// <summary>What the rules may be: null when they are fine, else the sentence that says what is wrong.</summary>
    internal static Error? Validate(ClubRetentionSettings s)
    {
        foreach (var (weight, what) in new[]
                 {
                     (s.VisitWeight, "visits"), (s.SpendWeight, "spending"), (s.BalanceWeight, "an unpaid balance"),
                     (s.EventWeight, "events"), (s.ConcernWeight, "complaints and concerns"), (s.TenureWeight, "the length of membership"),
                 })
            if (weight is < 0 or > 100) return DomainErrors.Retention.Invalid($"The weight of {what} is a whole number from 0 to 100.");
        if (s.VisitWeight + s.SpendWeight + s.BalanceWeight + s.EventWeight + s.ConcernWeight + s.TenureWeight == 0)
            return DomainErrors.Retention.Invalid("At least one sign has to count: give one of the weights more than 0.");
        if (s.AtRiskThreshold is < 1 or > 100) return DomainErrors.Retention.Invalid("The at-risk threshold is a score from 1 to 100.");
        if (s.WatchThreshold < 0 || s.WatchThreshold >= s.AtRiskThreshold)
            return DomainErrors.Retention.Invalid("The watch threshold is a score from 0 up to, but not including, the at-risk threshold.");
        if (s.SaveTaskDueDays is < 0 or > 60) return DomainErrors.Retention.Invalid("The task is due within 0 to 60 days.");
        if (s.SaveEmailPurpose is not (ConsentPurposes.Marketing or ConsentPurposes.Service))
            return DomainErrors.Retention.Invalid("The email is either marketing (sent only with the member's consent) or about their membership (service).");
        if (s.SaveEmailSubject?.Length > 200) return DomainErrors.Retention.Invalid("The email's subject can be at most 200 characters.");
        if (s.SaveEmailBody?.Length > 4000) return DomainErrors.Retention.Invalid("The email's text can be at most 4,000 characters.");
        if (s.SaveCooldownDays is < 1 or > 365) return DomainErrors.Retention.Invalid("Save actions wait 1 to 365 days before they can run for the same member again.");
        if (!ReferralBonusKinds.IsKind(s.ReferralBonus)) return DomainErrors.Retention.Invalid("The referral bonus is none, points or a credit.");
        if (s.ReferralBonus == ReferralBonusKinds.Points && s.ReferralBonusPoints is < 1 or > 100_000)
            return DomainErrors.Retention.Invalid("A referral bonus in points is a whole number from 1 to 100,000.");
        if (s.ReferralBonusPoints is < 0 or > 100_000) return DomainErrors.Retention.Invalid("A referral bonus in points is a whole number from 1 to 100,000.");
        if (s.ReferralBonus == ReferralBonusKinds.Credit && s.ReferralBonusAmount is < 0.01m or > 100_000m)
            return DomainErrors.Retention.Invalid("A referral credit is an amount from 0.01 to 100,000.");
        if (s.ReferralBonusAmount is < 0 or > 100_000m) return DomainErrors.Retention.Invalid("A referral credit is an amount from 0.01 to 100,000.");
        if (!SponsorRules.IsRule(s.SponsorRule)) return DomainErrors.Retention.Invalid("Proposers and seconders are off, optional or required.");
        if (s.SponsorMinTenureMonths is < 0 or > 240)
            return DomainErrors.Retention.Invalid("A proposer or seconder has been a member for 0 to 240 months.");
        return null;
    }

    private static ClubRetentionSettings Copy(ClubRetentionSettings s)
    {
        var copy = new ClubRetentionSettings { OrganizationId = s.OrganizationId };
        Apply(s, copy);
        return copy;
    }

    private static void Apply(ClubRetentionSettings from, ClubRetentionSettings to)
    {
        to.VisitWeight = from.VisitWeight;
        to.SpendWeight = from.SpendWeight;
        to.BalanceWeight = from.BalanceWeight;
        to.EventWeight = from.EventWeight;
        to.ConcernWeight = from.ConcernWeight;
        to.TenureWeight = from.TenureWeight;
        to.AtRiskThreshold = from.AtRiskThreshold;
        to.WatchThreshold = from.WatchThreshold;
        to.SaveTask = from.SaveTask;
        to.SaveTaskDueDays = from.SaveTaskDueDays;
        to.SaveEmail = from.SaveEmail;
        to.SaveEmailPurpose = from.SaveEmailPurpose;
        to.SaveEmailSubject = from.SaveEmailSubject;
        to.SaveEmailBody = from.SaveEmailBody;
        to.SaveCooldownDays = from.SaveCooldownDays;
        to.ReferralBonus = from.ReferralBonus;
        to.ReferralBonusPoints = from.ReferralBonusPoints;
        to.ReferralBonusAmount = from.ReferralBonusAmount;
        to.SponsorRule = from.SponsorRule;
        to.SponsorMinTenureMonths = from.SponsorMinTenureMonths;
        to.SponsorNoOverdue = from.SponsorNoOverdue;
    }

    private static RetentionSettingsDto ToDto(ClubRetentionSettings s, bool saved, string currency) => new()
    {
        VisitWeight = s.VisitWeight,
        SpendWeight = s.SpendWeight,
        BalanceWeight = s.BalanceWeight,
        EventWeight = s.EventWeight,
        ConcernWeight = s.ConcernWeight,
        TenureWeight = s.TenureWeight,
        AtRiskThreshold = s.AtRiskThreshold,
        WatchThreshold = s.WatchThreshold,
        SaveTask = s.SaveTask,
        SaveTaskDueDays = s.SaveTaskDueDays,
        SaveEmail = s.SaveEmail,
        SaveEmailPurpose = s.SaveEmailPurpose,
        SaveEmailSubject = s.SaveEmailSubject,
        SaveEmailBody = s.SaveEmailBody,
        SaveCooldownDays = s.SaveCooldownDays,
        ReferralBonus = s.ReferralBonus,
        ReferralBonusPoints = s.ReferralBonusPoints,
        ReferralBonusAmount = s.ReferralBonusAmount,
        Currency = currency,
        SponsorRule = s.SponsorRule,
        SponsorMinTenureMonths = s.SponsorMinTenureMonths,
        SponsorNoOverdue = s.SponsorNoOverdue,
        Version = saved ? s.Version : null,
        UpdatedAtUtc = s.UpdatedAtUtc,
        UpdatedByName = s.UpdatedByName,
    };

    // ── concerns ─────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<ConcernDto>>> ConcernsAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        if (!await MemberExistsAsync(organizationId, memberId, ct)) return DomainErrors.Club.MemberNotFound;
        var rows = await _db.ClubMemberConcerns.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId)
            .OrderByDescending(c => c.RecordedAtUtc).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<ConcernDto>> AddConcernAsync(Guid organizationId, Guid memberId, Guid userId, ConcernRequest request, CancellationToken ct = default)
    {
        if (!await MemberExistsAsync(organizationId, memberId, ct)) return DomainErrors.Club.MemberNotFound;
        var kind = (request.Kind ?? ConcernKinds.Concern).Trim().ToLowerInvariant();
        if (!ConcernKinds.IsKind(kind)) return DomainErrors.Retention.ConcernKindInvalid;
        var note = (request.Note ?? "").Trim();
        if (note.Length is 0 or > 1000) return DomainErrors.Retention.ConcernNoteRequired;
        var row = new ClubMemberConcern
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = memberId,
            Kind = kind,
            Note = note,
            RecordedAtUtc = DateTime.UtcNow,
            RecordedByUserId = userId,
            RecordedByName = await NameOfUserAsync(userId, ct),
        };
        _db.ClubMemberConcerns.Add(row);
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<Result<ConcernDto>> ResolveConcernAsync(Guid organizationId, Guid memberId, Guid concernId, Guid userId, ResolveConcernRequest request,
        CancellationToken ct = default)
    {
        var row = await _db.ClubMemberConcerns.FirstOrDefaultAsync(c => c.Id == concernId && c.MemberId == memberId && c.OrganizationId == organizationId, ct);
        if (row == null) return DomainErrors.Retention.ConcernNotFound;
        if (row.ResolvedAtUtc != null) return DomainErrors.Retention.ConcernResolved;
        var resolution = request.Resolution?.Trim();
        if (resolution?.Length > 1000) return DomainErrors.Retention.ConcernNoteRequired;
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ResolvedByUserId = userId;
        row.ResolvedByName = await NameOfUserAsync(userId, ct);
        row.Resolution = string.IsNullOrEmpty(resolution) ? null : resolution;
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<Result> DeleteConcernAsync(Guid organizationId, Guid memberId, Guid concernId, CancellationToken ct = default)
    {
        var row = await _db.ClubMemberConcerns.FirstOrDefaultAsync(c => c.Id == concernId && c.MemberId == memberId && c.OrganizationId == organizationId, ct);
        if (row == null) return DomainErrors.Retention.ConcernNotFound;
        _db.ClubMemberConcerns.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static ConcernDto ToDto(ClubMemberConcern c) =>
        new(c.Id, c.MemberId, c.Kind, c.Note, c.RecordedAtUtc, c.RecordedByName, c.ResolvedAtUtc, c.ResolvedByName, c.Resolution);

    // ── shared ───────────────────────────────────────────────────────────────

    /// <summary>A member of the workspace whose personal data has not been erased.</summary>
    private Task<bool> MemberExistsAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        _db.ClubMembers.AsNoTracking().AnyAsync(m => m.Id == memberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);

    private Task<string?> NameOfUserAsync(Guid userId, CancellationToken ct) =>
        _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
}
