using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Common.Pdf;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>The club's billing and receivables (see <see cref="IClubBilling"/>).</summary>
/// <remarks>
/// <para>
/// Every run acts for one workspace at a time (AppDbContext.ActFor): the subscriptions it
/// bills, the charges it chases, the members it lapses and the emails it queues are that
/// workspace's alone. Only finding which workspaces have something switched on, and whether
/// today's run has been taken, reads across workspaces, and says so where it does.
/// </para>
/// <para>
/// Nothing here posts money itself: periods are billed through the ledger
/// (<see cref="IClubLedger.BillDueAsync"/>), which holds to one charge per subscription and
/// period. What the run adds is recorded with keys of its own: a reminder per charge and
/// step, a monthly statement per member and month, the daily run per workspace and day.
/// </para>
/// </remarks>
public sealed class ClubBillingService : IClubBilling
{
    /// <summary>The periods one subscription is billed in one run; a subscription further behind is caught up over the next nights.</summary>
    public const int MaxPeriodsPerSubscription = 24;

    /// <summary>Monthly statements one run sends; the rest go the next day, as they are still not sent.</summary>
    public const int MaxStatementsPerRun = 500;

    /// <summary>The local time from which a workspace's daily run goes: after the evening's payments are in.</summary>
    public static readonly TimeSpan RunsFromLocalTime = TimeSpan.FromHours(3);

    /// <summary>A run still marked running this long after it began has stopped (its server went away).</summary>
    private static readonly TimeSpan StoppedAfter = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubRules _rules;
    private readonly IOutbox _outbox;
    private readonly ICurrentUserService _user;
    private readonly IClubQrBills _qrBills;
    private readonly ILogger<ClubBillingService> _logger;

    public ClubBillingService(AppDbContext db, IClubLedger ledger, IClubRules rules, IOutbox outbox, ICurrentUserService user,
        IClubQrBills qrBills, ILogger<ClubBillingService> logger)
    {
        _db = db;
        _ledger = ledger;
        _rules = rules;
        _outbox = outbox;
        _user = user;
        _qrBills = qrBills;
        _logger = logger;
    }

    // ── runs ─────────────────────────────────────────────────────────────────

    public async Task<Result<BillingRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var run = new ClubBillingRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Trigger = BillingRunTriggers.Manual,
            Status = BillingRunStatuses.Running,
            StartedAtUtc = now,
            StartedByUserId = userId,
            StartedByName = Clip(_user.UserName, 200),
            Currency = (await WorkspaceAsync(organizationId, ct)).Currency,
        };
        _db.ClubBillingRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        // Started by a person: it bills what is due, whatever the automatic switch says, and
        // marks dues past due. Reminders, lapses and statements stay with the daily run.
        await ExecuteAsync(organizationId, run, scheduled: false, now, ct);
        return ToDto(run, now);
    }

    public async Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Across every workspace, as only this job reads: which ones have switched something
        // on. Each is then run acting for that workspace alone.
        var settings = await _db.ClubDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Kind == ClubSettings.Kind && d.OrganizationId != null)
            .Select(d => new { OrganizationId = d.OrganizationId!.Value, d.Payload, d.UpdatedAtUtc })
            .ToListAsync(ct);
        var due = settings.GroupBy(s => s.OrganizationId)
            .Select(g => g.OrderByDescending(s => s.UpdatedAtUtc).First())
            .Where(s => ClubSettings.Read(ParseObject(s.Payload)).Billing.AnythingAutomatic)
            .Select(s => s.OrganizationId)
            .ToList();

        var ran = 0;
        foreach (var organizationId in due)
        {
            ct.ThrowIfCancellationRequested();
            var workspace = await WorkspaceAsync(organizationId, ct);
            var zone = RegionalSettings.ZoneOf(workspace.TimeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
            if (local.TimeOfDay < RunsFromLocalTime) continue;
            var day = DateOnly.FromDateTime(local);
            // Across workspaces, by this one's id: whether its day was taken by another server.
            if (await _db.ClubBillingRuns.IgnoreQueryFilters().AnyAsync(r => r.OrganizationId == organizationId && r.ScheduledFor == day, ct)) continue;

            using var acting = _db.ActFor(organizationId, null);
            var run = new ClubBillingRun
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Trigger = BillingRunTriggers.Schedule,
                ScheduledFor = day,
                Status = BillingRunStatuses.Running,
                StartedAtUtc = utcNow,
                Currency = workspace.Currency,
            };
            _db.ClubBillingRuns.Add(run);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Another server took the day between the look and the claim.
                _db.Entry(run).State = EntityState.Detached;
                continue;
            }
            ran++;
            await ExecuteAsync(organizationId, run, scheduled: true, utcNow, ct);
            // Nothing one workspace's run tracked is carried into the next one's.
            _db.ChangeTracker.Clear();
        }
        return ran;
    }

    /// <summary>
    /// Does a claimed run's steps, recording what each did and what failed; one failure
    /// does not stop the rest. The run row is written once, at the end, so a step that has
    /// to let go of what it was saving never takes the run's own record with it.
    /// </summary>
    private async Task ExecuteAsync(Guid organizationId, ClubBillingRun run, bool scheduled, DateTime now, CancellationToken ct)
    {
        var tally = new Tally();
        var steps = new List<string>();
        var status = BillingRunStatuses.Succeeded;
        try
        {
            var rules = await _rules.ReadAsync(organizationId, ct);
            if (!scheduled || rules.Billing.AutomaticBilling)
            {
                steps.Add("billing");
                await BillDueAsync(organizationId, now, tally, ct);
            }
            var overdue = await OverdueAsync(organizationId, now, null, ct);
            tally.OverdueCharges = overdue.Count;
            steps.Add("past_due");
            await MarkPastDueAsync(organizationId, overdue, tally, ct);
            if (scheduled && rules.Billing.RemindersEnabled)
            {
                steps.Add("reminders");
                await RemindAsync(organizationId, run.Id, rules, overdue, now, tally, ct);
            }
            if (scheduled)
            {
                // Lapsing only when switched on; putting back a member the billing lapsed, always.
                if (rules.Billing.LapseEnabled) steps.Add("lapses");
                await LapseAsync(organizationId, rules, overdue, now, tally, ct);
            }
            if (scheduled && rules.Billing.MonthlyStatements)
            {
                steps.Add("statements");
                await SendMonthlyStatementsAsync(organizationId, run.Id, rules, now, tally, ct);
            }
            if (tally.Failures.Count > 0) status = BillingRunStatuses.PartlyFailed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The billing run {RunId} stopped part way", run.Id);
            Forget();
            tally.Failures.Add(new BillingRunFailureDto("run", null, "The run", $"It stopped part way: {ex.Message}"));
            status = BillingRunStatuses.Failed;
        }

        run.Status = status;
        run.FinishedAtUtc = DateTime.UtcNow;
        run.Steps = string.Join(',', steps);
        run.SubscriptionsBilled = tally.SubscriptionsBilled;
        run.ChargesPosted = tally.ChargesPosted;
        run.AmountBilled = tally.AmountBilled;
        run.OverdueCharges = tally.OverdueCharges;
        run.SubscriptionsPastDue = tally.SubscriptionsPastDue;
        run.RemindersQueued = tally.RemindersQueued;
        run.MembersLapsed = tally.MembersLapsed;
        run.MembersReinstated = tally.MembersReinstated;
        run.StatementsQueued = tally.StatementsQueued;
        run.FailureCount = tally.Failures.Count;
        // The first hundred are enough to act on; the count says how many there were.
        run.Failures = tally.Failures.Count == 0 ? null : JsonSerializer.Serialize(tally.Failures.Take(100), Json);
        if (_db.Entry(run).State == EntityState.Detached) _db.ClubBillingRuns.Update(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BillingRunDto>> RunsAsync(Guid organizationId, int take = 20, CancellationToken ct = default)
    {
        var rows = await _db.ClubBillingRuns.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        return rows.Select(r => ToDto(r, now)).ToList();
    }

    public async Task<BillingPreviewDto> PreviewAsync(Guid organizationId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var subs = await DueSubscriptionsAsync(organizationId, now, ct);
        var items = new List<BillingPreviewItemDto>();
        foreach (var s in subs)
        {
            var periods = new List<DateTime>();
            var period = s.NextChargeAtUtc ?? now;
            var anchor = s.BillingAnchorDay ?? period.Day;
            while (period <= now && periods.Count < MaxPeriodsPerSubscription)
            {
                periods.Add(period);
                period = BillingPeriods.Next(period, s.Cadence, anchor);
            }
            if (periods.Count > 0)
                items.Add(new BillingPreviewItemDto(s.Id, s.MemberId, s.MemberName, s.Name, s.Amount, s.Currency, s.Cadence, periods));
        }
        var byCurrency = items.GroupBy(i => i.Currency).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount * i.Periods.Count));
        return new BillingPreviewDto(items.Count, items.Sum(i => i.Periods.Count), byCurrency,
            items.OrderBy(i => i.Periods[0]).ThenBy(i => i.MemberName, StringComparer.OrdinalIgnoreCase).Take(200).ToList());
    }

    // ── billing ──────────────────────────────────────────────────────────────

    private async Task<List<ClubSubscription>> DueSubscriptionsAsync(Guid organizationId, DateTime now, CancellationToken ct) =>
        await _db.ClubSubscriptions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && (s.Status == "Active" || s.Status == "PastDue")
                && (s.NextChargeAtUtc == null || s.NextChargeAtUtc <= now))
            .OrderBy(s => s.NextChargeAtUtc)
            .ToListAsync(ct);

    private async Task BillDueAsync(Guid organizationId, DateTime now, Tally tally, CancellationToken ct)
    {
        foreach (var sub in await DueSubscriptionsAsync(organizationId, now, ct))
        {
            try
            {
                var billed = await _ledger.BillDueAsync(organizationId, sub.Id, now, MaxPeriodsPerSubscription, ct);
                if (!billed.IsSuccess)
                {
                    tally.Failures.Add(new BillingRunFailureDto("subscription", sub.Id, $"{sub.Name}, {sub.MemberName}", billed.Error.Description));
                    continue;
                }
                if (billed.Value.Count == 0) continue;
                tally.SubscriptionsBilled++;
                tally.ChargesPosted += billed.Value.Count;
                tally.AmountBilled += billed.Value.Sum(c => c.Amount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Subscription {SubscriptionId} could not be billed", sub.Id);
                Forget();
                tally.Failures.Add(new BillingRunFailureDto("subscription", sub.Id, $"{sub.Name}, {sub.MemberName}", ex.Message));
            }
        }
    }

    // ── overdue ──────────────────────────────────────────────────────────────

    /// <summary>A charge past its due date and not paid in full.</summary>
    private sealed record Overdue(Guid Id, Guid MemberId, string Description, decimal Owed, string Currency, DateTime DueAtUtc, Guid? SubscriptionId);

    /// <summary>The workspace's charges past their due date with something still to pay, oldest first; one member's with <paramref name="memberId"/>.</summary>
    private async Task<List<Overdue>> OverdueAsync(Guid organizationId, DateTime now, Guid? memberId, CancellationToken ct)
    {
        var candidates = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && (memberId == null || c.MemberId == memberId)
                && c.ReversesChargeId == null && c.DueAtUtc != null && c.DueAtUtc < now && c.Amount > 0)
            .Select(c => new { c.Id, c.MemberId, c.Description, c.Amount, c.Currency, Due = c.DueAtUtc!.Value, c.SubscriptionId })
            .ToListAsync(ct);
        if (candidates.Count == 0) return [];
        var ids = candidates.Select(c => c.Id).ToList();
        var voided = (await _db.ClubCharges.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.ReversesChargeId != null && ids.Contains(c.ReversesChargeId.Value))
                .Select(c => c.ReversesChargeId!.Value).ToListAsync(ct))
            .ToHashSet();
        var paid = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && ids.Contains(a.ChargeId))
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.ChargeId, g => g.Sum, ct);
        var owing = candidates.Where(c => !voided.Contains(c.Id))
            .Select(c => new Overdue(c.Id, c.MemberId, c.Description, c.Amount - paid.GetValueOrDefault(c.Id), c.Currency, c.Due, c.SubscriptionId))
            .Where(c => c.Owed > 0)
            .OrderBy(c => c.DueAtUtc)
            .ToList();
        return await LessCreditHeldAsync(organizationId, owing, ct);
    }

    /// <summary>
    /// The overdue charges less what credit their members already hold, the credit set
    /// against the charges that fell due first.
    /// </summary>
    /// <remarks>
    /// Credit on an account (an opening balance in the member's favour, a correction by hand)
    /// pays no charge until someone applies it to one, so the ledger shows the charge as
    /// owing; but the member owes the club nothing for the part the credit covers. Chased as
    /// it stands, a member in credit was sent payment reminders and lapsed. The credit is
    /// worked out as the ledger works it out when it is applied (each payment or credit, less
    /// what was taken back from it and what it has paid already).
    /// </remarks>
    private async Task<List<Overdue>> LessCreditHeldAsync(Guid organizationId, List<Overdue> overdue, CancellationToken ct)
    {
        if (overdue.Count == 0) return overdue;
        var memberIds = overdue.Select(c => c.MemberId).Distinct().ToList();
        var payments = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && memberIds.Contains(p.MemberId))
            .Select(p => new { p.Id, p.MemberId, p.Amount, p.ReversesPaymentId })
            .ToListAsync(ct);
        var originals = payments.Where(p => p.ReversesPaymentId == null).Select(p => p.Id).ToList();
        var allocated = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && originals.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId).Select(g => new { PaymentId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.PaymentId, g => g.Sum, ct);
        var reversed = payments.Where(p => p.ReversesPaymentId != null)
            .GroupBy(p => p.ReversesPaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var credit = payments.Where(p => p.ReversesPaymentId == null)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(p => Math.Max(0, p.Amount + reversed.GetValueOrDefault(p.Id) - allocated.GetValueOrDefault(p.Id))));

        var left = new List<Overdue>(overdue.Count);
        foreach (var charge in overdue)
        {
            var held = credit.GetValueOrDefault(charge.MemberId);
            if (held <= 0)
            {
                left.Add(charge);
                continue;
            }
            var covered = Math.Min(held, charge.Owed);
            credit[charge.MemberId] = held - covered;
            if (charge.Owed > covered) left.Add(charge with { Owed = charge.Owed - covered });
        }
        return left;
    }

    /// <summary>A subscription with dues overdue is past due; one whose dues are all paid is active again.</summary>
    private async Task MarkPastDueAsync(Guid organizationId, IReadOnlyList<Overdue> overdue, Tally tally, CancellationToken ct)
    {
        var behind = overdue.Where(c => c.SubscriptionId != null).Select(c => c.SubscriptionId!.Value).ToHashSet();
        var subs = await _db.ClubSubscriptions
            .Where(s => s.OrganizationId == organizationId && (s.Status == "Active" || s.Status == "PastDue"))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var sub in subs)
        {
            if (sub.Status == "Active" && behind.Contains(sub.Id))
            {
                sub.Status = "PastDue";
                sub.UpdatedAtUtc = now;
                tally.SubscriptionsPastDue++;
            }
            else if (sub.Status == "PastDue" && !behind.Contains(sub.Id))
            {
                sub.Status = "Active";
                sub.UpdatedAtUtc = now;
            }
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone changed a subscription at the same moment: theirs stands, and the
            // next run sets the status again from what is overdue then.
            Forget();
        }
    }

    // ── reminders ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reminds each member of the charges that have reached a new reminder step since the
    /// last run: one email per member, listing them, and one record per charge and step.
    /// </summary>
    /// <remarks>
    /// A charge already past several steps when reminders begin (the club turned them on
    /// with old debts on the books) is reminded at the latest step it has reached, once; the
    /// earlier steps are recorded as skipped, so nobody gets three reminders in one morning.
    /// </remarks>
    private async Task RemindAsync(Guid organizationId, Guid runId, ClubRulesSnapshot rules, IReadOnlyList<Overdue> overdue, DateTime now, Tally tally, CancellationToken ct)
    {
        if (overdue.Count == 0) return;
        var steps = rules.Billing.ReminderDays;
        var ids = overdue.Select(c => c.Id).ToList();
        var sent = (await _db.ClubPaymentReminders.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId && ids.Contains(r.ChargeId))
                .Select(r => new { r.ChargeId, r.Step }).ToListAsync(ct))
            .GroupBy(r => r.ChargeId).ToDictionary(g => g.Key, g => g.Max(r => r.Step));
        var memberIds = overdue.Select(c => c.MemberId).Distinct().ToList();
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && memberIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);
        var workspace = await WorkspaceAsync(organizationId, ct);

        foreach (var group in overdue.GroupBy(c => c.MemberId))
        {
            var reached = new List<(Overdue Charge, int Step)>();
            foreach (var charge in group)
            {
                var days = (int)Math.Floor((now - charge.DueAtUtc).TotalDays);
                var step = 0;
                for (var i = 0; i < steps.Count; i++)
                    if (steps[i] <= days) step = i + 1;
                if (step == 0 || sent.GetValueOrDefault(charge.Id) >= step) continue;
                reached.Add((charge, step));
            }
            if (reached.Count == 0 || !members.TryGetValue(group.Key, out var member)) continue;
            try
            {
                if (await RemindMemberAsync(organizationId, runId, member, reached, sent, rules, workspace, ct)) tally.RemindersQueued++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Member {MemberId} could not be reminded", member.Id);
                Forget();
                tally.Failures.Add(new BillingRunFailureDto("reminder", member.Id, NameOf(member), ex.Message));
            }
        }
    }

    /// <returns>Whether an email was queued.</returns>
    private async Task<bool> RemindMemberAsync(Guid organizationId, Guid runId, ClubMember member, List<(Overdue Charge, int Step)> reached,
        IReadOnlyDictionary<Guid, int> sent, ClubRulesSnapshot rules, Workspace workspace, CancellationToken ct)
    {
        var steps = rules.Billing.ReminderDays;
        var now = DateTime.UtcNow;
        var address = (member.Email ?? "").Trim();
        var reachable = ValidationHelper.IsValidEmail(address);
        var rows = new List<ClubPaymentReminder>();
        foreach (var (charge, step) in reached)
        {
            for (var earlier = sent.GetValueOrDefault(charge.Id) + 1; earlier < step; earlier++)
                rows.Add(Reminder(organizationId, runId, charge, earlier, steps[earlier - 1], ReminderStatuses.Skipped,
                    "A later reminder was due by the time this charge was first reminded of, so only that one went.", now));
            rows.Add(Reminder(organizationId, runId, charge, step, steps[step - 1],
                reachable ? ReminderStatuses.Queued : ReminderStatuses.Skipped,
                reachable ? null : "The member has no valid email address on their record.", now));
        }

        var last = reached.Max(r => r.Step);
        var subject = last == steps.Count && steps.Count > 1 ? "Final payment reminder"
            : last == 1 ? "Payment reminder"
            : last == 2 ? "Second payment reminder"
            : $"Payment reminder ({last})";
        var culture = Culture(workspace.Locale);
        var zone = RegionalSettings.ZoneOf(workspace.TimeZone);
        var body = new StringBuilder();
        body.Append("Dear ").Append(string.IsNullOrWhiteSpace(member.FirstName) ? NameOf(member) : member.FirstName.Trim()).Append(",\n\n");
        body.Append(last == steps.Count && steps.Count > 1
            ? "This is a final reminder. The following on your account is still unpaid:\n\n"
            : last == 1
                ? "This is a reminder that the following on your account has not been paid by its due date:\n\n"
                : "We have still not received payment for the following on your account:\n\n");
        foreach (var (charge, _) in reached.OrderBy(r => r.Charge.DueAtUtc))
            body.Append("- ").Append(charge.Description).Append(": ").Append(Money(charge.Owed, charge.Currency, culture))
                .Append(", due ").Append(Date(charge.DueAtUtc, zone)).Append('\n');
        var currency = reached[0].Charge.Currency;
        if (reached.All(r => r.Charge.Currency == currency))
            body.Append("\nTotal overdue: ").Append(Money(reached.Sum(r => r.Charge.Owed), currency, culture)).Append('\n');
        if (rules.Billing.LapseEnabled)
            body.Append("\nA membership is suspended when a charge is still unpaid ").Append(rules.Billing.GracePeriodDays)
                .Append(" days after its due date, until it is paid.\n");
        body.Append("\nIf you have paid in the last few days, thank you, and please ignore this reminder.\n\n").Append(workspace.Name);

        var log = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Email = reachable ? address : null,
            Channel = CampaignChannels.Email,
            Category = "Dues",
            Purpose = ConsentPurposes.Service,
            Subject = subject,
            Preview = Clip(body.ToString(), 500),
            Status = reachable ? MemberMessageStatuses.Queued : MemberMessageStatuses.Skipped,
            Reason = reachable ? null : "This member has no valid email address on their record.",
            CreatedAtUtc = now,
        };

        // The records first, in the same transaction as the email: the unique (charge, step)
        // is what stops a second server reminding the member again, and it must stop it
        // before the email exists.
        return await InTransactionAsync(async () =>
        {
            _db.ClubPaymentReminders.AddRange(rows);
            _db.MemberMessages.Add(log);
            await _db.SaveChangesAsync(ct);
            if (!reachable) return false;
            var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.PaymentReminder,
                SourceId = runId,
                To = [address],
                RecipientName = NameOf(member),
                Subject = subject,
                Body = body.ToString(),
                MemberId = member.Id,
                Purpose = ConsentPurposes.Service,
            }, ct);
            var went = queued.IsSuccess;
            foreach (var row in rows.Where(r => r.Status == ReminderStatuses.Queued))
            {
                if (went) row.OutboundMessageId = queued.Value.Id;
                else
                {
                    row.Status = ReminderStatuses.Suppressed;
                    row.Reason = Clip(queued.Error.Description, 500);
                }
            }
            if (went) log.OutboundMessageId = queued.Value.Id;
            else
            {
                log.Status = MemberMessageStatuses.Suppressed;
                log.Reason = Clip(queued.Error.Description, 500);
            }
            await _db.SaveChangesAsync(ct);
            return went;
        }, ct);
    }

    private static ClubPaymentReminder Reminder(Guid organizationId, Guid runId, Overdue charge, int step, int days, string status, string? reason, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        ChargeId = charge.Id,
        MemberId = charge.MemberId,
        Step = step,
        DaysAfterDue = days,
        Status = status,
        Reason = reason,
        RunId = runId,
        CreatedAtUtc = now,
    };

    // ── lapse and reinstatement ──────────────────────────────────────────────

    /// <summary>
    /// Lapses an active member with a charge overdue longer than the grace period (when
    /// lapsing is on), and puts back a member the billing lapsed once nothing of theirs is
    /// overdue. A member whose status someone has changed since is left as they set it.
    /// </summary>
    private async Task LapseAsync(Guid organizationId, ClubRulesSnapshot rules, IReadOnlyList<Overdue> overdue, DateTime now, Tally tally, CancellationToken ct)
    {
        var grace = rules.Billing.GracePeriodDays;
        var lapse = rules.Billing.LapseEnabled
            ? overdue.Where(c => (now - c.DueAtUtc).TotalDays > grace).Select(c => c.MemberId).ToHashSet()
            : [];
        var behind = overdue.Select(c => c.MemberId).ToHashSet();
        var lapseIds = lapse.ToList();
        var members = await _db.ClubMembers
            .Where(m => m.OrganizationId == organizationId && (lapseIds.Contains(m.Id) || m.BillingLapsedAtUtc != null))
            .ToListAsync(ct);
        foreach (var member in members)
        {
            if (member.BillingLapsedAtUtc != null)
            {
                if (member.Status != "Lapsed")
                {
                    // Someone set the status since: it is theirs now.
                    member.BillingLapsedAtUtc = null;
                    member.StatusBeforeLapse = null;
                }
                else if (!behind.Contains(member.Id))
                {
                    Reinstate(member);
                    tally.MembersReinstated++;
                }
                continue;
            }
            // Only an active member is lapsed: a pending, resigned or already lapsed one is
            // a decision someone made, and stays as it is.
            if (lapse.Contains(member.Id) && member.Status == "Active")
            {
                member.StatusBeforeLapse = member.Status;
                member.Status = "Lapsed";
                member.BillingLapsedAtUtc = now;
                tally.MembersLapsed++;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private static void Reinstate(ClubMember member)
    {
        member.Status = string.IsNullOrWhiteSpace(member.StatusBeforeLapse) ? "Active" : member.StatusBeforeLapse;
        member.BillingLapsedAtUtc = null;
        member.StatusBeforeLapse = null;
    }

    public async Task ReinstateIfSettledAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member?.BillingLapsedAtUtc == null || member.Status != "Lapsed") return;
        if ((await OverdueAsync(organizationId, DateTime.UtcNow, memberId, ct)).Count > 0) return;
        Reinstate(member);
        await _db.SaveChangesAsync(ct);
    }

    // ── statements ───────────────────────────────────────────────────────────

    public async Task<Result<StatementFile>> StatementPdfAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var read = await _ledger.StatementAsync(organizationId, memberId, fromUtc, toUtc, ct);
        if (!read.IsSuccess) return read.Error;
        var statement = read.Value;
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        var workspace = await WorkspaceAsync(organizationId, ct);
        var lines = statement.Lines.Select(l => new StatementPdfLine(
            l.AtUtc,
            l.Description,
            Detail(l),
            l.Debit,
            l.Credit,
            Owed(l.Balance),
            l.VatRate)).ToList();
        var amountDue = Math.Max(0, Owed(statement.ClosingBalance));
        // The club's QR-bill for what is due, with a reference that names the member and this
        // statement, when its bank details are set; none otherwise.
        var slip = await _qrBills.ForStatementAsync(organizationId, memberId, statement.FromUtc, statement.ToUtc, amountDue, statement.Currency, ct);
        var bytes = StatementPdf.Render(new StatementPdfRequest(
            workspace.Name,
            statement.MemberName,
            member?.Email,
            member?.Tier,
            statement.FromUtc,
            statement.ToUtc,
            statement.Currency,
            workspace.Locale,
            workspace.TimeZone,
            Owed(statement.OpeningBalance),
            statement.TotalDebits,
            statement.TotalCredits,
            Owed(statement.ClosingBalance),
            lines,
            DateTime.UtcNow,
            slip));
        return new StatementFile(FileName(statement, workspace), bytes, statement, amountDue);
    }

    public async Task<Result<InvoiceFile>> ChargeInvoicePdfAsync(Guid organizationId, Guid chargeId, CancellationToken ct = default)
    {
        // The charge as the ledger sees it now: what has been paid on it, whether it was voided.
        var memberId = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.Id == chargeId && c.OrganizationId == organizationId)
            .Select(c => (Guid?)c.MemberId).FirstOrDefaultAsync(ct);
        if (memberId is not { } owner) return DomainErrors.Club.ChargeNotFound;
        var charge = (await _ledger.ChargesAsync(organizationId, owner, ct)).FirstOrDefault(c => c.Id == chargeId);
        if (charge == null) return DomainErrors.Club.ChargeNotFound;
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == owner && m.OrganizationId == organizationId, ct);
        var workspace = await WorkspaceAsync(organizationId, ct);

        // The charge's document number in the accounting journal.
        var number = "C-" + charge.Id.ToString("N")[..8].ToUpperInvariant();
        var voided = charge.Status == "Voided";
        var amountDue = ChargeInvoicePdf.Owed(charge.Amount, charge.PaidAmount, voided);
        // The club's QR-bill for what is left on the charge, with a reference that names it.
        var slip = await _qrBills.ForChargeAsync(organizationId, owner, charge.Id, amountDue, charge.Currency, "Invoice " + number, ct);
        var bytes = ChargeInvoicePdf.Render(new ChargeInvoicePdfRequest(
            workspace.Name,
            charge.MemberName,
            member?.Email,
            member?.Tier,
            number,
            charge.Description,
            charge.EntryType == "Charge" ? charge.Kind : "Balance brought over",
            charge.PostedAtUtc,
            charge.DueAtUtc,
            charge.Amount,
            charge.PaidAmount,
            voided,
            charge.VatRate,
            charge.Currency,
            workspace.Locale,
            workspace.TimeZone,
            DateTime.UtcNow,
            slip));
        return new InvoiceFile("invoice-" + number + ".pdf", bytes, amountDue);
    }

    public async Task<Result<StatementDeliveryDto>> EmailStatementAsync(Guid organizationId, Guid userId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        if (!ValidationHelper.IsValidEmail((member.Email ?? "").Trim())) return DomainErrors.Club.MemberNoEmail;
        var sent = await SendStatementAsync(organizationId, member, fromUtc, toUtc, StatementSources.OnDemand, null, userId, ct);
        if (!sent.IsSuccess) return sent.Error;
        return ToDto(sent.Value);
    }

    /// <summary>
    /// Sends last month's statements, on and after the club's statement day, to every member
    /// with something on their account that month or a balance at its end, once each.
    /// </summary>
    private async Task SendMonthlyStatementsAsync(Guid organizationId, Guid runId, ClubRulesSnapshot rules, DateTime now, Tally tally, CancellationToken ct)
    {
        var workspace = await WorkspaceAsync(organizationId, ct);
        var zone = RegionalSettings.ZoneOf(workspace.TimeZone);
        var today = RegionalSettings.TodayIn(zone, now);
        if (today.Day < rules.Billing.StatementDay) return;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var fromUtc = RegionalSettings.StartOfDayUtc(thisMonth.AddMonths(-1), zone);
        var toUtc = RegionalSettings.StartOfDayUtc(thisMonth, zone);

        var active = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.PostedAtUtc >= fromUtc && c.PostedAtUtc < toUtc)
            .Select(c => c.MemberId)
            .Concat(_db.ClubPayments.AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && p.ReceivedAtUtc >= fromUtc && p.ReceivedAtUtc < toUtc)
                .Select(p => p.MemberId))
            .Distinct().ToListAsync(ct);
        var balances = await _ledger.BalancesAsync(organizationId, toUtc, ct);
        var due = active.Concat(balances.Where(b => b.Value != 0).Select(b => b.Key)).ToHashSet();
        var already = (await _db.ClubStatementDeliveries.AsNoTracking()
                .Where(d => d.OrganizationId == organizationId && d.Source == StatementSources.Monthly && d.PeriodStartUtc == fromUtc)
                .Select(d => d.MemberId).ToListAsync(ct))
            .ToHashSet();
        var dueIds = due.Where(id => !already.Contains(id)).ToList();
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && dueIds.Contains(m.Id))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Take(MaxStatementsPerRun)
            .ToListAsync(ct);
        foreach (var member in members)
        {
            try
            {
                var sent = await SendStatementAsync(organizationId, member, fromUtc, toUtc, StatementSources.Monthly, runId, null, ct);
                if (!sent.IsSuccess)
                    tally.Failures.Add(new BillingRunFailureDto("statement", member.Id, NameOf(member), sent.Error.Description));
                else if (sent.Value.Status == ReminderStatuses.Queued)
                    tally.StatementsQueued++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The statement for member {MemberId} could not be sent", member.Id);
                Forget();
                tally.Failures.Add(new BillingRunFailureDto("statement", member.Id, NameOf(member), ex.Message));
            }
        }
    }

    /// <summary>
    /// Renders the member's statement and queues it to them as a service email with the PDF
    /// attached, recording the delivery and a line in the communications log. A monthly
    /// statement the member already has for the month is not sent again (409).
    /// </summary>
    private async Task<Result<ClubStatementDelivery>> SendStatementAsync(Guid organizationId, ClubMember member, DateTime fromUtc, DateTime toUtc,
        string source, Guid? runId, Guid? userId, CancellationToken ct)
    {
        var file = await StatementPdfAsync(organizationId, member.Id, fromUtc, toUtc, ct);
        if (!file.IsSuccess) return file.Error;
        var workspace = await WorkspaceAsync(organizationId, ct);
        var culture = Culture(workspace.Locale);
        var zone = RegionalSettings.ZoneOf(workspace.TimeZone);
        var statement = file.Value.Statement;
        var period = PeriodWords(fromUtc, toUtc, zone);
        var address = (member.Email ?? "").Trim();
        var reachable = ValidationHelper.IsValidEmail(address);
        var now = DateTime.UtcNow;
        var subject = $"Your statement for {period}";

        var closing = Owed(statement.ClosingBalance);
        var body = new StringBuilder();
        body.Append("Dear ").Append(string.IsNullOrWhiteSpace(member.FirstName) ? NameOf(member) : member.FirstName.Trim()).Append(",\n\n");
        body.Append("Your statement for ").Append(period).Append(" is attached.\n\n");
        body.Append("Brought forward: ").Append(Money(Owed(statement.OpeningBalance), statement.Currency, culture)).Append('\n');
        body.Append("Charges: ").Append(Money(statement.TotalDebits, statement.Currency, culture)).Append('\n');
        body.Append("Payments and credits: ").Append(Money(statement.TotalCredits, statement.Currency, culture)).Append('\n');
        body.Append(closing > 0 ? "Amount due: " : closing < 0 ? "In credit: " : "Balance: ")
            .Append(Money(Math.Abs(closing), statement.Currency, culture)).Append("\n\n");
        body.Append("If you have a question about it, reply to this email.\n\n").Append(workspace.Name);

        var delivery = new ClubStatementDelivery
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            PeriodStartUtc = fromUtc,
            PeriodEndUtc = toUtc,
            Source = source,
            Status = reachable ? ReminderStatuses.Queued : ReminderStatuses.Skipped,
            Reason = reachable ? null : "The member has no valid email address on their record.",
            AmountDue = file.Value.AmountDue,
            Currency = statement.Currency,
            RunId = runId,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
        };
        var log = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Email = reachable ? address : null,
            Channel = CampaignChannels.Email,
            Category = "Dues",
            Purpose = ConsentPurposes.Service,
            Subject = subject,
            Preview = Clip(body.ToString(), 500),
            Status = reachable ? MemberMessageStatuses.Queued : MemberMessageStatuses.Skipped,
            Reason = delivery.Reason,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        };

        try
        {
            await InTransactionAsync(async () =>
            {
                _db.ClubStatementDeliveries.Add(delivery);
                _db.MemberMessages.Add(log);
                await _db.SaveChangesAsync(ct);
                if (!reachable) return false;
                var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
                {
                    OrganizationId = organizationId,
                    Source = OutboundSources.Statement,
                    SourceId = delivery.Id,
                    To = [address],
                    RecipientName = NameOf(member),
                    Subject = subject,
                    Body = body.ToString(),
                    CreatedByUserId = userId,
                    ReplyToUserId = userId,
                    MemberId = member.Id,
                    Purpose = ConsentPurposes.Service,
                    Attachments = [new EmailAttachment(file.Value.FileName, "application/pdf", file.Value.Content)],
                }, ct);
                if (queued.IsSuccess)
                {
                    delivery.OutboundMessageId = queued.Value.Id;
                    log.OutboundMessageId = queued.Value.Id;
                }
                else
                {
                    delivery.Status = ReminderStatuses.Suppressed;
                    delivery.Reason = Clip(queued.Error.Description, 500);
                    log.Status = MemberMessageStatuses.Suppressed;
                    log.Reason = delivery.Reason;
                }
                await _db.SaveChangesAsync(ct);
                return true;
            }, ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The month's statement went to the member from another server meanwhile.
            Forget();
            return new Error("Statement.Sent.Conflict", "This member has already been sent this month's statement.");
        }
        return delivery;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>What a run did, added up as it goes.</summary>
    private sealed class Tally
    {
        public int SubscriptionsBilled;
        public int ChargesPosted;
        public decimal AmountBilled;
        public int OverdueCharges;
        public int SubscriptionsPastDue;
        public int RemindersQueued;
        public int MembersLapsed;
        public int MembersReinstated;
        public int StatementsQueued;
        public readonly List<BillingRunFailureDto> Failures = new();
    }

    private sealed record Workspace(string Name, string Currency, string Locale, string TimeZone);

    private async Task<Workspace> WorkspaceAsync(Guid organizationId, CancellationToken ct)
    {
        // Organizations are not filtered by workspace: this is the one the run acts for.
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Name, o.Currency, o.Locale, o.TimeZone }).FirstOrDefaultAsync(ct);
        return new Workspace(
            org?.Name ?? "",
            RegionalSettings.TryCurrency(org?.Currency, out var currency) ? currency : RegionalSettings.DefaultCurrency,
            string.IsNullOrWhiteSpace(org?.Locale) ? RegionalSettings.DefaultLocale : org.Locale,
            string.IsNullOrWhiteSpace(org?.TimeZone) ? RegionalSettings.DefaultTimeZone : org.TimeZone);
    }

    /// <summary>Runs <paramref name="work"/> as one transaction on SQL Server; the in-memory provider the tests use has none.</summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
        {
            try
            {
                return await work();
            }
            catch
            {
                Forget();
                throw;
            }
        }
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            Forget();
            throw;
        }
    }

    /// <summary>Lets go of whatever this request was in the middle of saving, so the next save does not carry it.</summary>
    private void Forget()
    {
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity is not ClubBillingRun).ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    /// <summary>A balance (negative while owing) as what is owed (positive while owing).</summary>
    private static decimal Owed(decimal balance) => balance == 0 ? 0 : -balance;

    private static string? Detail(StatementLineDto line)
    {
        var label = line.EntryType switch
        {
            ClubEntryTypes.Opening => "Balance brought over",
            ClubEntryTypes.Adjustment => "Correction",
            ClubEntryTypes.Void => "Void",
            ClubEntryTypes.Refund => "Refund",
            _ => null,
        };
        var parts = new[] { label, line.Reason, line.Reference }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>"October 2026" for a calendar month in the club's time zone; otherwise its first and last day.</summary>
    private static string PeriodWords(DateTime fromUtc, DateTime toUtc, TimeZoneInfo zone)
    {
        var from = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), zone);
        var to = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), zone);
        if (from.Day == 1 && from.TimeOfDay == TimeSpan.Zero && to == from.AddMonths(1))
            return from.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return $"{from:d MMM yyyy} – {to.AddTicks(-1):d MMM yyyy}".Replace("  ", " ");
    }

    private static string FileName(AccountStatementDto statement, Workspace workspace)
    {
        var zone = RegionalSettings.ZoneOf(workspace.TimeZone);
        var from = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(statement.FromUtc, DateTimeKind.Utc), zone);
        var slug = new string(statement.MemberName.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return $"statement-{from:yyyy-MM}-{(slug.Length == 0 ? "member" : slug[..Math.Min(slug.Length, 60)])}.pdf";
    }

    private static string Money(decimal amount, string currency, CultureInfo culture) =>
        $"{(amount < 0 ? "-" : "")}{currency} {Math.Abs(amount).ToString("N2", culture)}";

    private static string Date(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static CultureInfo Culture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(RegionalSettings.DefaultLocale);
        }
    }

    private static string NameOf(ClubMember member) => Clip($"{member.FirstName} {member.LastName}".Trim(), 200);

    private static string Clip(string? text, int max)
    {
        var value = (text ?? "").Trim();
        return value.Length <= max ? value : value[..max];
    }

    private static JsonObject? ParseObject(string payload)
    {
        try
        {
            return JsonNode.Parse(payload) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BillingRunDto ToDto(ClubBillingRun r, DateTime now) => new()
    {
        Id = r.Id,
        Trigger = r.Trigger,
        ScheduledFor = r.ScheduledFor,
        Status = r.Status == BillingRunStatuses.Running && now - r.StartedAtUtc > StoppedAfter ? "stopped" : r.Status,
        StartedAtUtc = r.StartedAtUtc,
        FinishedAtUtc = r.FinishedAtUtc,
        StartedByName = string.IsNullOrWhiteSpace(r.StartedByName) ? null : r.StartedByName,
        Steps = string.IsNullOrWhiteSpace(r.Steps) ? Array.Empty<string>() : r.Steps.Split(',', StringSplitOptions.RemoveEmptyEntries),
        SubscriptionsBilled = r.SubscriptionsBilled,
        ChargesPosted = r.ChargesPosted,
        AmountBilled = r.AmountBilled,
        Currency = r.Currency,
        OverdueCharges = r.OverdueCharges,
        SubscriptionsPastDue = r.SubscriptionsPastDue,
        RemindersQueued = r.RemindersQueued,
        MembersLapsed = r.MembersLapsed,
        MembersReinstated = r.MembersReinstated,
        StatementsQueued = r.StatementsQueued,
        Failures = string.IsNullOrWhiteSpace(r.Failures)
            ? Array.Empty<BillingRunFailureDto>()
            : JsonSerializer.Deserialize<List<BillingRunFailureDto>>(r.Failures, Json) ?? (IReadOnlyList<BillingRunFailureDto>)Array.Empty<BillingRunFailureDto>(),
    };

    private static StatementDeliveryDto ToDto(ClubStatementDelivery d) =>
        new(d.Id, d.MemberId, d.PeriodStartUtc, d.PeriodEndUtc, d.Status, d.Reason, d.AmountDue, d.Currency, d.OutboundMessageId);
}
