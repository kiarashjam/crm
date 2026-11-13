using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Retention;

/// <summary>The renewals board (see <see cref="IClubRenewals"/>).</summary>
/// <remarks>
/// Renewal dates sat on each member (RenewsAtUtc) and on each subscription (its next period),
/// and nothing looked at them: nobody could see who was coming up, who had been spoken to, or
/// who quietly did not come back. The board reads them for a window, works each one's stage out
/// from the records, and keeps what staff decide by hand on a renewal record of its own.
/// </remarks>
public sealed class ClubRenewalService : IClubRenewals
{
    /// <summary>The renewal season: contact in these days before the end counts, and the daily run notes renewals this far ahead.</summary>
    public const int SeasonDays = 90;

    /// <summary>A membership's end moved on by at least this much was renewed; less is a correction of the date.</summary>
    private static readonly TimeSpan RenewedIfMovedBy = TimeSpan.FromDays(28);

    /// <summary>A subscription's period ending this close to the membership's end is the same renewal, shown once.</summary>
    private static readonly TimeSpan SameRenewalWithin = TimeSpan.FromDays(31);

    /// <summary>Activities on a member's contact that are someone talking to them.</summary>
    private static readonly string[] ContactTypes = ["call", "meeting", "email", "video", "demo"];

    /// <summary>
    /// A member who has left the club, as the privacy settings count former members: their
    /// renewal is lapsed whatever else the records say, and it is not marked renewed here.
    /// </summary>
    private static readonly string[] LeftStatuses = ["Lapsed", "Cancelled", "Resigned"];

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [RenewalStages.Upcoming] = "Upcoming",
        [RenewalStages.Contacted] = "Contacted",
        [RenewalStages.AtRisk] = "At risk",
        [RenewalStages.Renewed] = "Renewed",
        [RenewalStages.Lapsed] = "Lapsed",
    };

    private readonly AppDbContext _db;

    public ClubRenewalService(AppDbContext db) => _db = db;

    private sealed record MemberRow(
        Guid Id, string FirstName, string LastName, string Tier, string Status, DateTime RenewsAtUtc, decimal DuesAmount, string DuesFrequency,
        Guid OwnerUserId, Guid? ContactId)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
    }

    private sealed record SubscriptionRow(Guid Id, Guid MemberId, string Name, decimal Amount, string Currency, string Cadence, string Status, DateTime? NextChargeAtUtc);

    /// <summary>One renewal the board shows: what ends, and when.</summary>
    private sealed record Candidate(Guid MemberId, string Source, Guid? SubscriptionId, DateTime EndsAtUtc);

    // ── the board ────────────────────────────────────────────────────────────

    public async Task<Result<RenewalBoardDto>> BoardAsync(Guid organizationId, Guid userId, RenewalBoardQuery query, CancellationToken ct = default)
    {
        var days = query.Days ?? 60;
        var past = query.PastDays ?? 30;
        if (days is < 1 or > 365 || past is < 0 or > 365) return DomainErrors.Renewal.WindowInvalid;
        var now = DateTime.UtcNow;
        var from = now.AddDays(-past);
        var to = now.AddDays(days);
        Guid? owner = string.Equals(query.Owner?.Trim(), "me", StringComparison.OrdinalIgnoreCase) ? userId
            : Guid.TryParse(query.Owner, out var named) ? named : null;

        // A corporate nominee renews with their company's membership, which has renewals of its own
        // (ICorporateMemberships): a card for each nominee would ask about one renewal many times.
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null && m.Status != "Pending" && (owner == null || m.OwnerUserId == owner)
                && !_db.ClubCorporateNominees.Any(n => n.MemberId == m.Id && n.RemovedAtUtc == null))
            .Select(m => new MemberRow(m.Id, m.FirstName, m.LastName, m.Tier, m.Status, m.RenewsAtUtc, m.DuesAmount, m.DuesFrequency, m.OwnerUserId, m.ContactId))
            .ToListAsync(ct);
        var memberIds = members.Select(m => m.Id).ToHashSet();
        var subscriptions = (await _db.ClubSubscriptions.AsNoTracking()
                .Where(s => s.OrganizationId == organizationId)
                .Select(s => new SubscriptionRow(s.Id, s.MemberId, s.Name, s.Amount, s.Currency, s.Cadence, s.Status, s.NextChargeAtUtc))
                .ToListAsync(ct))
            .Where(s => memberIds.Contains(s.MemberId)).ToList();
        var rows = (await _db.ClubRenewals.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId && r.EndsAtUtc >= from && r.EndsAtUtc <= to)
                .ToListAsync(ct))
            .Where(r => memberIds.Contains(r.MemberId)).ToList();

        var candidates = Candidates(members, subscriptions, rows, from, to, query.Monthly == true);
        var cards = await CardsAsync(organizationId, candidates, members, subscriptions, rows, now, ct);
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        var (currency, _, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        var columns = RenewalStages.All.Select(stage =>
            {
                var inStage = cards.Where(c => c.Stage == stage).OrderBy(c => c.EndsAtUtc).ThenBy(c => c.Name).ToList();
                var totals = inStage.GroupBy(c => c.Currency)
                    .Select(g => new ClubCurrencyAmountDto(g.Key, g.Sum(c => c.AnnualValue)))
                    .OrderBy(t => t.Currency == currency ? 0 : 1).ThenBy(t => t.Currency).ToList();
                return new RenewalColumnDto(stage, Labels[stage], inStage.Count, totals, inStage);
            })
            .ToList();
        return new RenewalBoardDto(from, to, days, past, currency, settings.AtRiskThreshold, columns);
    }

    /// <summary>
    /// The renewals in the window: each member's membership end, each subscription period that
    /// ends (monthly ones only when asked), and each renewal recorded for the window (one renewed
    /// from the board has moved its member's date out of it). A subscription period ending close
    /// to its member's membership end is the same renewal and is shown once, as the membership.
    /// </summary>
    /// <remarks>
    /// A membership's end that is no longer the member's renewal date was renewed when the date
    /// moved on by four weeks or more. A smaller move, or one back, put the date right: the
    /// renewal is the one at the new date, which has its own card, so the daily run's note of
    /// the old date is not shown beside it (it turned "lapsed, ended without a renewal" once the
    /// old date passed, for a member renewing on the new one). A record staff set something on
    /// (a stage, a note) still shows: what they decided is not hidden.
    /// </remarks>
    private static List<Candidate> Candidates(IReadOnlyList<MemberRow> members, IReadOnlyList<SubscriptionRow> subscriptions, IReadOnlyList<ClubRenewal> rows,
        DateTime from, DateTime to, bool monthly)
    {
        var all = new List<Candidate>();
        foreach (var m in members.Where(m => m.RenewsAtUtc >= from && m.RenewsAtUtc <= to))
            all.Add(new Candidate(m.Id, RenewalSources.Membership, null, m.RenewsAtUtc));
        foreach (var s in subscriptions.Where(s => s.Status is "Active" or "PastDue" && s.NextChargeAtUtc is DateTime next && next >= from && next <= to
                     && (monthly || s.Cadence != "Monthly")))
            all.Add(new Candidate(s.MemberId, RenewalSources.Subscription, s.Id, s.NextChargeAtUtc!.Value));
        var renewsAt = members.ToDictionary(m => m.Id, m => m.RenewsAtUtc);
        foreach (var r in rows)
        {
            if (r.Source == RenewalSources.Membership && r.Stage == null && r.Note == null
                && renewsAt.TryGetValue(r.MemberId, out var current) && current != r.EndsAtUtc && current - r.EndsAtUtc < RenewedIfMovedBy)
                continue;
            all.Add(new Candidate(r.MemberId, r.Source, r.SubscriptionId, r.EndsAtUtc));
        }
        var distinct = all.DistinctBy(c => (c.MemberId, c.Source, c.SubscriptionId, c.EndsAtUtc)).ToList();
        var memberships = distinct.Where(c => c.Source == RenewalSources.Membership).ToLookup(c => c.MemberId);
        return distinct.Where(c => c.Source == RenewalSources.Membership
                || !memberships[c.MemberId].Any(m => (m.EndsAtUtc - c.EndsAtUtc).Duration() <= SameRenewalWithin))
            .ToList();
    }

    /// <summary>Each renewal as a card: its stage and why, its owner, what it is worth, the last contact and the score.</summary>
    private async Task<List<RenewalCardDto>> CardsAsync(Guid organizationId, IReadOnlyList<Candidate> candidates, IReadOnlyList<MemberRow> members,
        IReadOnlyList<SubscriptionRow> subscriptions, IReadOnlyList<ClubRenewal> rows, DateTime now, CancellationToken ct)
    {
        if (candidates.Count == 0) return [];
        var byMember = members.ToDictionary(m => m.Id);
        var subsById = subscriptions.ToDictionary(s => s.Id);
        var rowByKey = rows.GroupBy(r => (r.MemberId, r.Source, r.SubscriptionId, r.EndsAtUtc)).ToDictionary(g => g.Key, g => g.First());
        var cardMembers = candidates.Select(c => c.MemberId).Distinct().ToList();
        var lastContact = await LastContactAsync(organizationId, cardMembers.Select(id => byMember[id]).ToList(), ct);
        var risks = (await _db.ClubMemberRisks.AsNoTracking().Where(r => r.OrganizationId == organizationId)
                .Select(r => new { r.MemberId, r.Score, r.Level }).ToListAsync(ct))
            .Where(r => cardMembers.Contains(r.MemberId))
            .ToDictionary(r => r.MemberId, r => (r.Score, r.Level));
        var paidPeriods = await PaidPeriodsAsync(organizationId, candidates.Where(c => c.SubscriptionId != null).ToList(), ct);
        var (currency, _, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        var ownerIds = cardMembers.Select(id => byMember[id].OwnerUserId).Distinct().ToList();
        var owners = await _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var cards = new List<RenewalCardDto>(candidates.Count);
        foreach (var c in candidates)
        {
            var member = byMember[c.MemberId];
            var row = rowByKey.GetValueOrDefault((c.MemberId, c.Source, c.SubscriptionId, c.EndsAtUtc));
            var subscription = c.SubscriptionId is Guid sid ? subsById.GetValueOrDefault(sid) : null;
            // What the renewal brings in a year: the subscription's dues, else the dues on the member.
            var dues = subscription ?? subscriptions.Where(s => s.MemberId == member.Id && s.Status is "Active" or "PastDue" or "Paused")
                .OrderBy(s => s.Cadence == "Monthly" ? 1 : 0).FirstOrDefault();
            var (periodAmount, cadence, cardCurrency) = dues != null
                ? (dues.Amount, dues.Cadence, dues.Currency)
                : (member.DuesAmount, member.DuesFrequency, currency);
            (DateTime At, string Kind)? contact = lastContact.TryGetValue(member.Id, out var known) ? known : null;
            var risk = risks.TryGetValue(member.Id, out var r) ? r : ((int Score, string Level)?)null;
            var (stage, reason) = StageOf(c, member, subscription, row, risk, contact, paidPeriods, now);
            cards.Add(new RenewalCardDto
            {
                MemberId = member.Id,
                Name = member.Name,
                Tier = member.Tier,
                MemberStatus = member.Status,
                Source = c.Source,
                SubscriptionId = c.SubscriptionId,
                SubscriptionName = subscription?.Name,
                EndsAtUtc = c.EndsAtUtc,
                DaysLeft = (int)Math.Floor((c.EndsAtUtc - now).TotalDays),
                Stage = stage,
                StageReason = reason,
                StageSetByHand = row?.Stage != null,
                OwnerUserId = member.OwnerUserId,
                OwnerName = owners.GetValueOrDefault(member.OwnerUserId),
                AnnualValue = periodAmount * RetentionRules.PeriodsPerYear(cadence),
                PeriodAmount = periodAmount,
                Cadence = cadence,
                Currency = cardCurrency,
                LastContactAtUtc = contact?.At,
                LastContactKind = contact?.Kind,
                RiskScore = risk?.Score,
                RiskLevel = risk?.Level,
                Note = row?.Note,
                RenewedUntilUtc = row?.RenewedUntilUtc,
                Version = row?.Version,
            });
        }
        return cards;
    }

    /// <summary>
    /// The stage of one renewal and why, in a few words. A member leaving or renewing outranks
    /// what staff set; what staff set outranks the score and the last contact.
    /// </summary>
    private static (string Stage, string Reason) StageOf(Candidate c, MemberRow member, SubscriptionRow? subscription, ClubRenewal? row,
        (int Score, string Level)? risk, (DateTime At, string Kind)? contact, IReadOnlySet<(Guid, DateTime)> paidPeriods, DateTime now)
    {
        if (LeftStatuses.Contains(member.Status))
            return (RenewalStages.Lapsed, $"The member is {member.Status.ToLowerInvariant()}");
        if (c.Source == RenewalSources.Membership && member.RenewsAtUtc - c.EndsAtUtc >= RenewedIfMovedBy)
            return (RenewalStages.Renewed, row?.RenewedAtUtc != null ? $"Renewed by {row.StageSetByName ?? "staff"}" : "The renewal date has moved on");
        if (c.Source == RenewalSources.Subscription && c.SubscriptionId is Guid sid && paidPeriods.Contains((sid, c.EndsAtUtc)))
            return (RenewalStages.Renewed, "The next period is billed and paid");
        if (row?.Stage is { } set)
        {
            var by = row.StageSetByName is { } name ? $" by {name}" : "";
            return set switch
            {
                RenewalStages.Renewed => (RenewalStages.Renewed, $"Marked renewed{by}"),
                RenewalStages.Contacted => (RenewalStages.Contacted, $"Marked contacted{by}"),
                RenewalStages.AtRisk => (RenewalStages.AtRisk, $"Marked at risk{by}"),
                RenewalStages.Lapsed => (RenewalStages.Lapsed, $"Marked not renewing{by}"),
                _ => (RenewalStages.Upcoming, ""),
            };
        }
        if (c.Source == RenewalSources.Membership && c.EndsAtUtc < now)
            return (RenewalStages.Lapsed, "Ended without a renewal");
        if (c.Source == RenewalSources.Subscription && subscription?.Status is "Cancelled")
            return (RenewalStages.Lapsed, "The subscription was cancelled");
        if (risk is { Level: RiskLevels.AtRisk } atRisk)
            return (RenewalStages.AtRisk, $"At-risk score {atRisk.Score}");
        if (contact is { } last && last.At >= c.EndsAtUtc.AddDays(-SeasonDays))
            return (RenewalStages.Contacted, last.Kind switch
            {
                "activity" => "A call, meeting or email was logged",
                "marked" => "Marked contacted",
                _ => "Someone wrote to them",
            });
        return (RenewalStages.Upcoming, "Nobody has been in touch about it yet");
    }

    /// <summary>The subscription periods (by their start) that are billed and paid in full: renewed.</summary>
    private async Task<IReadOnlySet<(Guid, DateTime)>> PaidPeriodsAsync(Guid organizationId, IReadOnlyList<Candidate> periods, CancellationToken ct)
    {
        if (periods.Count == 0) return new HashSet<(Guid, DateTime)>();
        var subscriptionIds = periods.Select(p => p.SubscriptionId!.Value).Distinct().ToList();
        var charges = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.SubscriptionId != null && subscriptionIds.Contains(c.SubscriptionId.Value)
                && c.BillingPeriodStartUtc != null && c.ReversesChargeId == null && c.EntryType == ClubEntryTypes.Charge)
            .Select(c => new { c.Id, SubscriptionId = c.SubscriptionId!.Value, Start = c.BillingPeriodStartUtc!.Value, c.Amount })
            .ToListAsync(ct);
        var wanted = periods.Select(p => (p.SubscriptionId!.Value, p.EndsAtUtc)).ToHashSet();
        charges = charges.Where(c => wanted.Contains((c.SubscriptionId, c.Start))).ToList();
        if (charges.Count == 0) return new HashSet<(Guid, DateTime)>();
        var ids = charges.Select(c => c.Id).ToList();
        var voided = (await _db.ClubCharges.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.ReversesChargeId != null && ids.Contains(c.ReversesChargeId.Value))
                .Select(c => c.ReversesChargeId!.Value).ToListAsync(ct))
            .ToHashSet();
        var paid = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && ids.Contains(a.ChargeId))
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.ChargeId, g => g.Sum, ct);
        return charges.Where(c => !voided.Contains(c.Id) && paid.GetValueOrDefault(c.Id) >= c.Amount)
            .Select(c => (c.SubscriptionId, c.Start)).ToHashSet();
    }

    /// <summary>
    /// When someone last spoke to each member, and how: a message someone at the club wrote to
    /// them (sent from their page, or recorded as sent another way), a call, meeting or email
    /// logged on their contact, or a renewal marked contacted.
    /// </summary>
    /// <remarks>
    /// What the club sends on its own is no contact: statements, payment reminders, pay links,
    /// the welcome, event emails, portal invitations and the retention rules' own email go out
    /// whoever looks after the member, like a campaign's. Counted, the monthly statement alone
    /// put nearly every member in "contacted", with its date as the last contact.
    /// </remarks>
    private async Task<Dictionary<Guid, (DateTime At, string Kind)>> LastContactAsync(Guid organizationId, IReadOnlyList<MemberRow> members, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (DateTime At, string Kind)>();
        void Offer(Guid memberId, DateTime at, string kind)
        {
            if (!result.TryGetValue(memberId, out var known) || at > known.At) result[memberId] = (at, kind);
        }
        var ids = members.Select(m => m.Id).ToHashSet();
        var written = _db.OutboundMessages.Where(o => o.OrganizationId == organizationId && o.Source == OutboundSources.Member).Select(o => o.Id);
        var messages = await _db.MemberMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.CampaignId == null
                && (m.Status == MemberMessageStatuses.Logged
                    || (m.OutboundMessageId != null && written.Contains(m.OutboundMessageId.Value)
                        && m.Status != MemberMessageStatuses.Skipped && m.Status != MemberMessageStatuses.Failed
                        && m.Status != MemberMessageStatuses.Suppressed)))
            .GroupBy(m => m.MemberId).Select(g => new { MemberId = g.Key, At = g.Max(m => m.CreatedAtUtc) })
            .ToListAsync(ct);
        foreach (var m in messages.Where(m => ids.Contains(m.MemberId))) Offer(m.MemberId, m.At, "message");

        var byContact = members.Where(m => m.ContactId != null).GroupBy(m => m.ContactId!.Value).ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());
        foreach (var chunk in byContact.Keys.Chunk(1000))
        {
            var contactIds = chunk.ToList();
            var activities = await _db.Activities.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && a.ContactId != null && contactIds.Contains(a.ContactId.Value) && ContactTypes.Contains(a.Type))
                .GroupBy(a => a.ContactId!.Value).Select(g => new { ContactId = g.Key, At = g.Max(a => a.OccurredAtUtc) })
                .ToListAsync(ct);
            foreach (var a in activities)
                foreach (var memberId in byContact[a.ContactId]) Offer(memberId, a.At, "activity");
        }

        var marked = await _db.ClubRenewals.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.ContactedAtUtc != null)
            .GroupBy(r => r.MemberId).Select(g => new { MemberId = g.Key, At = g.Max(r => r.ContactedAtUtc!.Value) })
            .ToListAsync(ct);
        foreach (var m in marked.Where(m => ids.Contains(m.MemberId))) Offer(m.MemberId, m.At, "marked");
        return result;
    }

    // ── setting a stage ──────────────────────────────────────────────────────

    public async Task<Result<RenewalCardDto>> SetStageAsync(Guid organizationId, Guid userId, RenewalStageRequest request, CancellationToken ct = default)
    {
        var source = (request.Source ?? RenewalSources.Membership).Trim().ToLowerInvariant();
        if (source is not (RenewalSources.Membership or RenewalSources.Subscription)) return DomainErrors.Renewal.SourceInvalid;
        var stage = (request.Stage ?? "").Trim().ToLowerInvariant();
        if (stage != "auto" && !RenewalStages.Settable.Contains(stage)) return DomainErrors.Renewal.StageInvalid(request.Stage ?? "");
        var note = request.Note?.Trim();
        if (note?.Length > 1000) return DomainErrors.Renewal.NoteTooLong;
        var endsAt = AsUtc(request.EndsAtUtc);
        var until = request.RenewUntilUtc is DateTime asked ? AsUtc(asked) : endsAt.AddYears(1);
        if (stage == RenewalStages.Renewed && source == RenewalSources.Membership && (until <= endsAt || until > endsAt.AddYears(10)))
            return DomainErrors.Renewal.RenewUntilInvalid;

        var member = await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        // A member who has left stays lapsed on the board (their status outranks a mark), so
        // "renewed" would only have moved their renewal date a year on while they stayed lapsed.
        // Setting them active on their page (or the payment that puts back a member the billing
        // lapsed) is what renews them; the board then shows it.
        if (stage == RenewalStages.Renewed && LeftStatuses.Contains(member.Status))
            return DomainErrors.Renewal.MemberLeft($"{member.FirstName} {member.LastName}".Trim(), member.Status);
        Guid? subscriptionId = null;
        if (source == RenewalSources.Subscription)
        {
            if (request.SubscriptionId is not Guid sid
                || !await _db.ClubSubscriptions.AnyAsync(s => s.Id == sid && s.MemberId == member.Id && s.OrganizationId == organizationId, ct))
                return DomainErrors.Renewal.SubscriptionNotFound;
            subscriptionId = sid;
        }

        var now = DateTime.UtcNow;
        // The renewal of this subscription: a member's dues and their locker can end the same day.
        var row = await _db.ClubRenewals.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.MemberId == member.Id
            && r.Source == source && r.SubscriptionId == subscriptionId && r.EndsAtUtc == endsAt, ct);
        if (row != null && request.Version is { } version && version != Guid.Empty && version != row.Version) return DomainErrors.Renewal.Changed;
        if (row == null)
        {
            // Only a renewal the board shows: the membership's end, or the subscription's next
            // period, as they stand. A card loaded before the date moved (renewed on the member's
            // form, say) would otherwise leave a record for an end that no longer exists.
            var current = source == RenewalSources.Membership
                ? member.RenewsAtUtc == endsAt
                : await _db.ClubSubscriptions.AnyAsync(s => s.Id == subscriptionId && s.NextChargeAtUtc == endsAt, ct);
            if (!current) return DomainErrors.Renewal.NotFound;
            row = new ClubRenewal
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = member.Id, Source = source, SubscriptionId = subscriptionId,
                EndsAtUtc = endsAt, CreatedAtUtc = now,
            };
            _db.ClubRenewals.Add(row);
        }
        if (request.Note != null) row.Note = string.IsNullOrEmpty(note) ? null : note;
        var who = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        if (stage == "auto")
        {
            // Back to what the records say. A membership renewed from the board keeps its new
            // date: taking the mark back moves no dates (the member's form does that).
            row.Stage = null;
            row.StageSetAtUtc = null;
            row.StageSetByUserId = null;
            row.StageSetByName = null;
        }
        else
        {
            row.Stage = stage;
            row.StageSetAtUtc = now;
            row.StageSetByUserId = userId;
            row.StageSetByName = who;
            if (stage == RenewalStages.Contacted)
            {
                row.ContactedAtUtc = now;
                row.ContactedByUserId = userId;
            }
            if (stage == RenewalStages.Renewed)
            {
                row.RenewedAtUtc = now;
                if (source == RenewalSources.Membership)
                {
                    // Moved on once: a second press (or a renewal already made on the member's form)
                    // finds the date past this end and leaves it.
                    if (member.RenewsAtUtc - endsAt < RenewedIfMovedBy)
                    {
                        member.RenewsAtUtc = until;
                        row.RenewedUntilUtc = until;
                    }
                    else row.RenewedUntilUtc ??= member.RenewsAtUtc;
                }
            }
        }
        row.UpdatedAtUtc = now;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return DomainErrors.Renewal.Changed;
        }

        var card = (await CardsForMemberAsync(organizationId, member.Id, source, subscriptionId, endsAt, now, ct)).FirstOrDefault();
        return card is null ? DomainErrors.Club.MemberNotFound : card;
    }

    private async Task<List<RenewalCardDto>> CardsForMemberAsync(Guid organizationId, Guid memberId, string source, Guid? subscriptionId, DateTime endsAt,
        DateTime now, CancellationToken ct)
    {
        var members = await _db.ClubMembers.AsNoTracking().Where(m => m.Id == memberId && m.OrganizationId == organizationId)
            .Select(m => new MemberRow(m.Id, m.FirstName, m.LastName, m.Tier, m.Status, m.RenewsAtUtc, m.DuesAmount, m.DuesFrequency, m.OwnerUserId, m.ContactId))
            .ToListAsync(ct);
        var subscriptions = await _db.ClubSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.MemberId == memberId)
            .Select(s => new SubscriptionRow(s.Id, s.MemberId, s.Name, s.Amount, s.Currency, s.Cadence, s.Status, s.NextChargeAtUtc))
            .ToListAsync(ct);
        var rows = await _db.ClubRenewals.AsNoTracking().Where(r => r.OrganizationId == organizationId && r.MemberId == memberId).ToListAsync(ct);
        return await CardsAsync(organizationId, [new Candidate(memberId, source, subscriptionId, endsAt)], members, subscriptions, rows, now, ct);
    }

    // ── the daily run's notes ────────────────────────────────────────────────

    public async Task<int> TrackUpcomingAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct = default)
    {
        var until = nowUtc.AddDays(SeasonDays);
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == "Active" && m.ErasedAtUtc == null && m.RenewsAtUtc > nowUtc && m.RenewsAtUtc <= until)
            .Select(m => new { m.Id, m.RenewsAtUtc }).ToListAsync(ct);
        var active = (await _db.ClubMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.Status == "Active" && m.ErasedAtUtc == null)
                .Select(m => m.Id).ToListAsync(ct))
            .ToHashSet();
        var subscriptions = (await _db.ClubSubscriptions.AsNoTracking()
                .Where(s => s.OrganizationId == organizationId && (s.Status == "Active" || s.Status == "PastDue") && s.Cadence != "Monthly"
                    && s.NextChargeAtUtc != null && s.NextChargeAtUtc > nowUtc && s.NextChargeAtUtc <= until)
                .Select(s => new { s.Id, s.MemberId, Next = s.NextChargeAtUtc!.Value }).ToListAsync(ct))
            .Where(s => active.Contains(s.MemberId)).ToList();
        // Each subscription's renewal apart: two of a member's ending the same day are two renewals.
        var known = (await _db.ClubRenewals.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId && r.EndsAtUtc > nowUtc && r.EndsAtUtc <= until)
                .Select(r => new { r.MemberId, r.Source, r.SubscriptionId, r.EndsAtUtc }).ToListAsync(ct))
            .Select(r => (r.MemberId, r.Source, r.SubscriptionId, r.EndsAtUtc)).ToHashSet();

        var added = new List<ClubRenewal>();
        void Note(Guid memberId, string source, Guid? subscriptionId, DateTime endsAt)
        {
            if (!known.Add((memberId, source, subscriptionId, endsAt))) return;
            added.Add(new ClubRenewal
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = memberId, Source = source, SubscriptionId = subscriptionId,
                EndsAtUtc = endsAt, CreatedAtUtc = nowUtc,
            });
        }
        foreach (var m in members) Note(m.Id, RenewalSources.Membership, null, m.RenewsAtUtc);
        foreach (var s in subscriptions) Note(s.MemberId, RenewalSources.Subscription, s.Id, s.Next);
        if (added.Count == 0) return 0;

        _db.ClubRenewals.AddRange(added);
        try
        {
            await _db.SaveChangesAsync(ct);
            return added.Count;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Someone marked one of them at this moment: add the rest one by one, leaving theirs.
            foreach (var row in added) _db.Entry(row).State = EntityState.Detached;
            var count = 0;
            foreach (var row in added)
            {
                _db.ClubRenewals.Add(row);
                try
                {
                    await _db.SaveChangesAsync(ct);
                    count++;
                }
                catch (DbUpdateException again) when (IsUniqueViolation(again))
                {
                    _db.Entry(row).State = EntityState.Detached;
                }
            }
            return count;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    /// <summary>
    /// A date the request means as UTC. One sent with an offset arrives converted to the
    /// server's local time, and one without a zone arrives unspecified: both are turned to UTC,
    /// so the end matches the one the board showed.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
