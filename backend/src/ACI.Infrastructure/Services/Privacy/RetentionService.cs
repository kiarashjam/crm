using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = ACI.Domain.Enums.TaskStatus;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>
/// A workspace's retention settings, applied: once a day by the background job, or now by
/// someone who handles privacy requests (see <see cref="IRetention"/>).
/// </summary>
/// <remarks>
/// <para>
/// Nothing was ever deleted on a schedule: leads that went nowhere, members who left years
/// ago, the text of every email and the names on every audit line stayed for ever. Each rule
/// is off until the workspace sets its period, and each takes a bounded slice per run so a
/// first run over years of history stays short; the next day carries on where it stopped.
/// What a rule removes is gone for the next run, so a run repeated after a crash does no harm.
/// </para>
/// <para>
/// A lead and a member go through the same erasure as a request (PersonDataService), so
/// everything hanging off them goes or is anonymised the same way, except that the job does
/// not search everyone else's notes and mail for mentions, and puts no address on the
/// suppression list: nobody asked to stop hearing from the club.
/// </para>
/// </remarks>
public sealed class RetentionService : IRetention
{
    /// <summary>How much one run takes per rule, at most.</summary>
    internal const int LeadsPerRun = 200;
    internal const int MembersPerRun = 100;
    internal const int RowsPerRun = 5000;

    /// <summary>The daily run waits until this time in the workspace's own zone: the quiet hours.</summary>
    private static readonly TimeSpan RunsFromLocalTime = TimeSpan.FromHours(4);

    /// <summary>Far longer than a run takes with the slices above: a run still "running" after this was cut off.</summary>
    private static readonly TimeSpan RunGoesOnFor = TimeSpan.FromHours(2);

    /// <summary>What an email's text reads once a retention period took it.</summary>
    internal const string PurgedText = "(The text of this email was removed: the workspace keeps sent emails for a limited time.)";

    /// <summary>Statuses of a member who has left.</summary>
    private static readonly string[] FormerStatuses = ["Lapsed", "Cancelled", "Resigned"];

    private readonly AppDbContext _db;
    private readonly PersonDataService _people;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(AppDbContext db, PersonDataService people, ILogger<RetentionService> logger)
    {
        _db = db;
        _people = people;
        _logger = logger;
    }

    public async Task<RetentionPreviewDto> PreviewAsync(Guid organizationId, CancellationToken ct = default)
    {
        var settings = await _db.PrivacySettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        var now = DateTime.UtcNow;
        var rules = new List<RetentionRuleDto>();
        var kept = new List<string>();

        var leads = settings?.LeadRetentionMonths is int leadMonths ? await StaleLeads(organizationId, now.AddMonths(-leadMonths)).CountAsync(ct) : 0;
        rules.Add(new("leads", settings?.LeadRetentionMonths, "months", leads,
            "Leads that never became a customer, with nothing done on them for this long, are deleted with everything on them (and the contact made for them, when nobody worked on it either)."));

        var members = 0;
        if (settings?.MemberRetentionYears is int years)
        {
            var cutoff = now.AddYears(-years);
            members = await FormerMembers(organizationId, cutoff).CountAsync(ct);
            var owing = await FormerMembers(organizationId, cutoff, includeOpenMoney: true).CountAsync(ct) - members;
            if (owing > 0) kept.Add($"{owing} former {(owing == 1 ? "member is" : "members are")} kept although their period is over: money is still open on their account, or a subscription still runs.");
        }
        rules.Add(new("members", settings?.MemberRetentionYears, "years", members,
            "Members who resigned, lapsed or were cancelled, with nothing on their account for this long, are anonymised: their charges and payments stay in the books under a pseudonym."));

        var mail = settings?.MailRetentionMonths is int mailMonths ? await CountMailAsync(organizationId, now.AddMonths(-mailMonths), ct) : 0;
        rules.Add(new("mail", settings?.MailRetentionMonths, "months", mail,
            "Older sent emails lose their text and attachments (who, when and whether it arrived stay), and older generated copy is deleted."));

        var details = settings?.DetailRetentionMonths is int detailMonths ? await CountDetailsAsync(organizationId, now.AddMonths(-detailMonths), ct) : 0;
        rules.Add(new("details", settings?.DetailRetentionMonths, "months", details,
            "Older audit lines lose the name of the record they were about, and older webhook payloads, raw till tickets, reply subjects, notifications and logs of finished automation runs are removed."));

        var anythingOn = settings != null && (settings.LeadRetentionMonths != null || settings.MemberRetentionYears != null
            || settings.MailRetentionMonths != null || settings.DetailRetentionMonths != null);
        return new RetentionPreviewDto(anythingOn, rules, kept);
    }

    public async Task<Result<RetentionRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        // One run at a time per workspace: two at once would each anonymise the same former
        // member, under two erasure numbers.
        if (await RunGoingAsync(organizationId, DateTime.UtcNow, ct)) return DomainErrors.Privacy.RetentionRunning;
        var settings = await _db.PrivacySettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        var name = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var run = new RetentionRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Trigger = RetentionRunTriggers.Manual,
            Status = RetentionRunStatuses.Running,
            StartedAtUtc = DateTime.UtcNow,
            StartedByUserId = userId,
            StartedByName = name,
        };
        _db.RetentionRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        await ExecuteAsync(organizationId, run, settings, DateTime.UtcNow, userId, name, ct);
        return Result.Success(ToDto(run));
    }

    public async Task<IReadOnlyList<RetentionRunDto>> RunsAsync(Guid organizationId, CancellationToken ct = default) =>
        (await _db.RetentionRuns.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(60)
            .ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Across every workspace, as only this job reads: which ones set a retention period.
        // Each is then run acting for that workspace alone.
        var due = await _db.PrivacySettings.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.LeadRetentionMonths != null || s.MemberRetentionYears != null || s.MailRetentionMonths != null || s.DetailRetentionMonths != null)
            .Select(s => s.OrganizationId)
            .ToListAsync(ct);

        var ran = 0;
        foreach (var organizationId in due)
        {
            ct.ThrowIfCancellationRequested();
            var zoneName = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
            var zone = RegionalSettings.ZoneOf(zoneName ?? RegionalSettings.DefaultTimeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
            if (local.TimeOfDay < RunsFromLocalTime) continue;
            var day = DateOnly.FromDateTime(local);
            // Across workspaces, by this one's id: whether its day was taken by another server.
            if (await _db.RetentionRuns.IgnoreQueryFilters().AnyAsync(r => r.OrganizationId == organizationId && r.ScheduledFor == day, ct)) continue;

            using var acting = _db.ActFor(organizationId, null);
            // A run started by hand is still going: the day is left for the next look.
            if (await RunGoingAsync(organizationId, utcNow, ct)) continue;
            var run = new RetentionRun
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Trigger = RetentionRunTriggers.Schedule,
                ScheduledFor = day,
                Status = RetentionRunStatuses.Running,
                StartedAtUtc = utcNow,
            };
            _db.RetentionRuns.Add(run);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another server took the day between the look and the claim.
                _db.ChangeTracker.Clear();
                continue;
            }
            ran++;
            var settings = await _db.PrivacySettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
            await ExecuteAsync(organizationId, run, settings, utcNow, null, null, ct);
            // Nothing one workspace's run tracked is carried into the next one's.
            _db.ChangeTracker.Clear();
        }
        return ran;
    }

    /// <summary>
    /// Whether a run of the workspace is still going. A run still marked running after
    /// <see cref="RunGoesOnFor"/> was cut off (its server stopped half way) and no longer counts.
    /// </summary>
    private Task<bool> RunGoingAsync(Guid organizationId, DateTime utcNow, CancellationToken ct)
    {
        var since = utcNow - RunGoesOnFor;
        return _db.RetentionRuns.AnyAsync(r => r.OrganizationId == organizationId && r.Status == RetentionRunStatuses.Running && r.StartedAtUtc > since, ct);
    }

    /// <summary>
    /// Runs every rule that is on, recording what each removed and what failed; one failure
    /// does not stop the rest. The run's row is written at the end.
    /// </summary>
    private async Task ExecuteAsync(Guid organizationId, RetentionRun run, PrivacySettings? settings, DateTime now, Guid? userId, string? actor, CancellationToken ct)
    {
        var failures = new List<string>();
        async Task Rule(string name, Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Retention rule {Rule} failed for {OrganizationId}", name, organizationId);
                failures.Add($"{name}: {ex.Message}");
                _db.ChangeTracker.Clear();
            }
        }

        if (settings?.LeadRetentionMonths is int leadMonths)
            await Rule("Leads", async () => run.LeadsDeleted = await DeleteStaleLeadsAsync(organizationId, leadMonths, now, ct));
        if (settings?.MemberRetentionYears is int years)
            await Rule("Members", async () => run.MembersAnonymised = await AnonymiseFormerMembersAsync(organizationId, years, now, ct));
        if (settings?.MailRetentionMonths is int mailMonths)
            await Rule("Emails", async () => run.MessagesPurged = await PurgeMailAsync(organizationId, now.AddMonths(-mailMonths), ct));
        if (settings?.DetailRetentionMonths is int detailMonths)
            await Rule("Details", async () => run.DetailsPurged = await PurgeDetailsAsync(organizationId, now.AddMonths(-detailMonths), ct));

        _db.ChangeTracker.Clear();
        var row = await _db.RetentionRuns.FirstAsync(r => r.Id == run.Id, ct);
        row.LeadsDeleted = run.LeadsDeleted;
        row.MembersAnonymised = run.MembersAnonymised;
        row.MessagesPurged = run.MessagesPurged;
        row.DetailsPurged = run.DetailsPurged;
        row.FinishedAtUtc = DateTime.UtcNow;
        row.Failures = failures.Count == 0 ? null : Clip(string.Join("\n", failures), 2000);
        var anything = row.LeadsDeleted + row.MembersAnonymised + row.MessagesPurged + row.DetailsPurged > 0;
        row.Status = failures.Count == 0 ? RetentionRunStatuses.Succeeded : anything ? RetentionRunStatuses.PartlyFailed : RetentionRunStatuses.Failed;
        (run.Status, run.FinishedAtUtc, run.Failures) = (row.Status, row.FinishedAtUtc, row.Failures);
        if (anything || failures.Count > 0)
        {
            // One line per run that did something: what went, in counts. It names nobody.
            _db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                OwnerUserId = userId ?? Guid.Empty,
                EntityType = "privacy",
                EntityId = row.Id,
                EntityLabel = "Retention",
                Action = "purged",
                Summary = Clip($"Retention run: {row.LeadsDeleted} {Plural(row.LeadsDeleted, "lead")} deleted, {row.MembersAnonymised} former {Plural(row.MembersAnonymised, "member")} anonymised, "
                    + $"{row.MessagesPurged} {Plural(row.MessagesPurged, "email")} and copies cleared, {row.DetailsPurged} {Plural(row.DetailsPurged, "detail")} removed"
                    + (failures.Count > 0 ? $"; {failures.Count} {Plural(failures.Count, "rule")} failed" : ""), 500),
                ActorUserId = userId,
                ActorName = Clip(actor ?? AuditVocabulary.SystemActor, 200),
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── Leads ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Leads that never became anything (not converted, no deal, no member, no application, no
    /// signed contract) and on which nothing has happened since <paramref name="cutoff"/>.
    /// </summary>
    private IQueryable<Lead> StaleLeads(Guid organizationId, DateTime cutoff) =>
        _db.Leads.Where(l => l.OrganizationId == organizationId && !l.IsConverted && l.ConvertedToDealId == null
            && l.CreatedAtUtc < cutoff
            && (l.UpdatedAtUtc == null || l.UpdatedAtUtc < cutoff)
            && (l.LastContactedAt == null || l.LastContactedAt < cutoff)
            && !_db.Activities.Any(a => a.LeadId == l.Id && (a.OccurredAtUtc >= cutoff || a.CreatedAtUtc >= cutoff))
            && !_db.OutboundMessages.Any(m => m.LeadId == l.Id && m.CreatedAtUtc >= cutoff)
            && !_db.TaskItems.Any(t => t.LeadId == l.Id && (t.CreatedAtUtc >= cutoff || (t.UpdatedAtUtc != null && t.UpdatedAtUtc >= cutoff)
                || (t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled && t.DueDateUtc != null && t.DueDateUtc >= cutoff)))
            && !_db.OutreachEnrollments.Any(e => e.TargetType == "lead" && e.TargetId == l.Id && (e.Status == "active" || e.Status == "paused"))
            // Nor one an automation is still working on (a wait before its next email, say), as
            // for a sequence: the run would go with the lead.
            && !_db.AutomationRuns.Any(r => r.LeadId == l.Id && (r.Status == AutomationRunStatuses.Running
                || r.Status == AutomationRunStatuses.Waiting || r.Status == AutomationRunStatuses.Retrying))
            && !_db.ClubMembers.Any(m => m.LeadId == l.Id)
            && !_db.ClubApplications.Any(a => a.LeadId == l.Id)
            && !_db.Contracts.Any(c => c.LeadId == l.Id && (c.Status == ContractStatuses.SignedByClient || c.Status == ContractStatuses.Countersigned))
            // A member's guest at next week's gala, or someone who booked a table, has only an
            // address on the booking: they are in touch with the club, and their lead is not stale.
            // An erasure would cancel the booking, and a retention run cancels nobody's.
            && (l.Email.Trim() == ""
                || (!_db.ClubEventRegistrations.Any(r => r.Email != null && r.Email.Trim().ToLower() == l.Email.Trim().ToLower()
                        && (r.RegisteredAtUtc >= cutoff || _db.ClubEvents.Any(e => e.Id == r.EventId && e.StartAtUtc >= cutoff)))
                    && !_db.ClubReservations.Any(r => r.MemberEmail != null && r.MemberEmail.Trim().ToLower() == l.Email.Trim().ToLower()
                        && (r.StartAtUtc >= cutoff || r.CreatedAtUtc >= cutoff)))));

    private async Task<int> DeleteStaleLeadsAsync(Guid organizationId, int months, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddMonths(-months);
        var ids = await StaleLeads(organizationId, cutoff).OrderBy(l => l.CreatedAtUtc).Select(l => l.Id).Take(LeadsPerRun).ToListAsync(ct);
        var deleted = 0;
        foreach (var leadId in ids)
        {
            // The contact made for the lead goes with it when nobody worked on the contact either;
            // a contact with a life of its own stays, let go of the lead.
            var contactId = await _db.Contacts.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.ConvertedFromLeadId == leadId
                    && (c.UpdatedAtUtc == null || c.UpdatedAtUtc < cutoff)
                    && !_db.Deals.Any(d => d.ContactId == c.Id)
                    && !_db.Activities.Any(a => a.ContactId == c.Id && (a.OccurredAtUtc >= cutoff || a.CreatedAtUtc >= cutoff))
                    && !_db.TaskItems.Any(t => t.ContactId == c.Id && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled)
                    && !_db.OutboundMessages.Any(m => m.ContactId == c.Id && m.CreatedAtUtc >= cutoff)
                    && !_db.ClubMembers.Any(m => m.ContactId == c.Id)
                    && !_db.ClubHouseholdMembers.Any(h => h.ContactId == c.Id))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
            var selection = new PersonSelection { LeadIds = [leadId], ContactIds = contactId is Guid c ? [c] : [] };
            var result = await _people.EraseAsync(organizationId, selection, PersonErasureTriggers.Retention, null, "Retention", null, ct,
                $"A lead nobody worked on for {months} {Plural(months, "month")} was deleted by the retention settings");
            if (result.IsSuccess) deleted++;
            else _logger.LogWarning("Retention left lead {LeadId} in {OrganizationId}: {Error}", leadId, organizationId, result.Error.Description);
            _db.ChangeTracker.Clear();
        }
        return deleted;
    }

    // ── Members ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Members who left (resigned, lapsed or cancelled), not yet anonymised, with nothing on
    /// their account since <paramref name="cutoff"/>. One with money still open, or a
    /// subscription still running, is kept unless <paramref name="includeOpenMoney"/>.
    /// </summary>
    private IQueryable<ClubMember> FormerMembers(Guid organizationId, DateTime cutoff, bool includeOpenMoney = false)
    {
        var query = _db.ClubMembers.Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null
            && FormerStatuses.Contains(m.Status)
            && m.JoinedAtUtc < cutoff && m.RenewsAtUtc < cutoff
            && (m.BillingLapsedAtUtc == null || m.BillingLapsedAtUtc < cutoff)
            && !_db.ClubCharges.Any(c => c.MemberId == m.Id && c.PostedAtUtc >= cutoff)
            && !_db.ClubPayments.Any(p => p.MemberId == m.Id && p.ReceivedAtUtc >= cutoff)
            && !_db.ClubVisits.Any(v => v.MemberId == m.Id && v.VisitedAtUtc >= cutoff)
            && !_db.ClubReservations.Any(r => r.MemberId == m.Id && r.StartAtUtc >= cutoff)
            && !_db.ClubEventRegistrations.Any(r => r.MemberId == m.Id && r.RegisteredAtUtc >= cutoff)
            && !_db.MemberMessages.Any(x => x.MemberId == m.Id && x.CreatedAtUtc >= cutoff)
            && !_db.AuditEvents.Any(e => e.EntityId == m.Id && e.CreatedAtUtc >= cutoff)
            // An automation still working on them keeps them, as it keeps a lead.
            && !_db.AutomationRuns.Any(r => r.MemberId == m.Id && (r.Status == AutomationRunStatuses.Running
                || r.Status == AutomationRunStatuses.Waiting || r.Status == AutomationRunStatuses.Retrying))
            // Back as someone's guest, or booking a table, under the same address but no member.
            && (m.Email.Trim() == ""
                || (!_db.ClubEventRegistrations.Any(r => r.Email != null && r.Email.Trim().ToLower() == m.Email.Trim().ToLower()
                        && (r.RegisteredAtUtc >= cutoff || _db.ClubEvents.Any(e => e.Id == r.EventId && e.StartAtUtc >= cutoff)))
                    && !_db.ClubReservations.Any(r => r.MemberEmail != null && r.MemberEmail.Trim().ToLower() == m.Email.Trim().ToLower()
                        && (r.StartAtUtc >= cutoff || r.CreatedAtUtc >= cutoff)))));
        if (!includeOpenMoney)
            query = query.Where(m => m.HouseAccountBalance == 0
                && !_db.ClubSubscriptions.Any(s => s.MemberId == m.Id && s.Status != "Cancelled"));
        return query;
    }

    private async Task<int> AnonymiseFormerMembersAsync(Guid organizationId, int years, DateTime now, CancellationToken ct)
    {
        var ids = await FormerMembers(organizationId, now.AddYears(-years)).OrderBy(m => m.JoinedAtUtc).Select(m => m.Id).Take(MembersPerRun).ToListAsync(ct);
        var done = 0;
        foreach (var memberId in ids)
        {
            var result = await _people.EraseAsync(organizationId, new PersonSelection { MemberIds = [memberId] }, PersonErasureTriggers.Retention, null, "Retention", null, ct,
                $"A member who left over {years} {Plural(years, "year")} ago was anonymised by the retention settings");
            if (result.IsSuccess) done++;
            else _logger.LogWarning("Retention left member {MemberId} in {OrganizationId}: {Error}", memberId, organizationId, result.Error.Description);
            _db.ChangeTracker.Clear();
        }
        return done;
    }

    // ── Sent mail ───────────────────────────────────────────────────────────

    private IQueryable<OutboundMessage> OldMail(Guid organizationId, DateTime cutoff) =>
        _db.OutboundMessages.Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Email
            && m.CreatedAtUtc < cutoff && m.Status != OutboundStatuses.Queued
            && (m.TextBody != null || m.HtmlBody != null || (m.Body != null && m.Body != PurgedText)));

    private async Task<int> CountMailAsync(Guid organizationId, DateTime cutoff, CancellationToken ct) =>
        await OldMail(organizationId, cutoff).CountAsync(ct)
        + await _db.SentMessages.CountAsync(m => m.OrganizationId == organizationId && m.SentAtUtc < cutoff && m.Body != PurgedText, ct)
        + await _db.CopyHistoryItems.CountAsync(c => c.OrganizationId == organizationId && c.CreatedAtUtc < cutoff, ct);

    private async Task<int> PurgeMailAsync(Guid organizationId, DateTime cutoff, CancellationToken ct)
    {
        var messages = await OldMail(organizationId, cutoff).OrderBy(m => m.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        var ids = messages.Select(m => m.Id).ToList();
        var activityIds = messages.Where(m => m.ActivityId != null).Select(m => m.ActivityId!.Value).ToList();
        foreach (var m in messages)
        {
            m.Body = m.Body == null ? null : PurgedText;
            m.TextBody = null;
            m.HtmlBody = null;
        }
        // A statement's PDF and the like went with the email.
        foreach (var slice in ids.Chunk(1000))
        {
            var chunk = slice.ToList();
            _db.OutboundAttachments.RemoveRange(await _db.OutboundAttachments.Where(a => chunk.Contains(a.OutboundMessageId)).ToListAsync(ct));
        }
        // The timeline entry an email wrote holds its text too.
        foreach (var slice in activityIds.Chunk(1000))
        {
            var chunk = slice.ToList();
            foreach (var activity in await _db.Activities.Where(a => chunk.Contains(a.Id)).ToListAsync(ct))
                activity.Body = PurgedText;
        }
        var sent = await _db.SentMessages.Where(m => m.OrganizationId == organizationId && m.SentAtUtc < cutoff && m.Body != PurgedText)
            .OrderBy(m => m.SentAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var m in sent) m.Body = PurgedText;
        var copies = await _db.CopyHistoryItems.Where(c => c.OrganizationId == organizationId && c.CreatedAtUtc < cutoff)
            .OrderBy(c => c.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        _db.CopyHistoryItems.RemoveRange(copies);
        foreach (var x in await _db.MemberMessages.Where(x => x.OrganizationId == organizationId && x.CreatedAtUtc < cutoff && x.Preview != null)
                     .OrderBy(x => x.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct))
            x.Preview = null;
        await _db.SaveChangesAsync(ct);
        return messages.Count + sent.Count + copies.Count;
    }

    // ── Details around requests and the audit trail ─────────────────────────

    /// <summary>
    /// Audit lines older than <paramref name="cutoff"/> that still name what they were about.
    /// The lines of erasures and retention runs name nobody (a pseudonym, "Retention"); the line
    /// of an export names the person whose data was downloaded, like any other line.
    /// </summary>
    private IQueryable<AuditEvent> OldAuditLabels(Guid organizationId, DateTime cutoff) =>
        _db.AuditEvents.Where(e => e.OrganizationId == organizationId && e.CreatedAtUtc < cutoff && e.EntityLabel != null
            && (e.EntityType != "privacy" || e.Action == "exported"));

    private async Task<int> CountDetailsAsync(Guid organizationId, DateTime cutoff, CancellationToken ct) =>
        await OldAuditLabels(organizationId, cutoff).CountAsync(ct)
        + await _db.OutboundMessages.CountAsync(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Webhook && m.CreatedAtUtc < cutoff && m.PayloadJson != null, ct)
        + await _db.ClubPosTickets.CountAsync(t => t.OrganizationId == organizationId && t.ImportedAtUtc < cutoff && t.DataJson != null, ct)
        + await _db.InboundEmails.CountAsync(i => i.OrganizationId == organizationId && i.ReceivedAtUtc < cutoff && i.Subject != null, ct)
        + await _db.Notifications.CountAsync(n => n.OrganizationId == organizationId && n.CreatedAtUtc < cutoff, ct)
        + await OldAutomationRuns(organizationId, cutoff).CountAsync(ct);

    /// <summary>
    /// Automation runs that finished before <paramref name="cutoff"/>: each keeps the name of the
    /// record it was about, the address it wrote to and, in its log, what it did. A run still
    /// going is kept, whatever its age: it has steps to come.
    /// </summary>
    private IQueryable<AutomationRun> OldAutomationRuns(Guid organizationId, DateTime cutoff) =>
        _db.AutomationRuns.Where(r => r.OrganizationId == organizationId && r.FinishedAtUtc != null && r.FinishedAtUtc < cutoff
            && (r.Status == AutomationRunStatuses.Completed || r.Status == AutomationRunStatuses.Failed || r.Status == AutomationRunStatuses.Cancelled));

    private async Task<int> PurgeDetailsAsync(Guid organizationId, DateTime cutoff, CancellationToken ct)
    {
        // An audit line keeps who did what kind of thing and when; the name of the record it
        // was about goes.
        var lines = await OldAuditLabels(organizationId, cutoff).OrderBy(e => e.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var line in lines) line.EntityLabel = null;
        var hooks = await _db.OutboundMessages.Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Webhook && m.CreatedAtUtc < cutoff && m.PayloadJson != null)
            .OrderBy(m => m.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var hook in hooks) hook.PayloadJson = null;
        var tickets = await _db.ClubPosTickets.Where(t => t.OrganizationId == organizationId && t.ImportedAtUtc < cutoff && t.DataJson != null)
            .OrderBy(t => t.ImportedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var ticket in tickets) ticket.DataJson = null;
        var replies = await _db.InboundEmails.Where(i => i.OrganizationId == organizationId && i.ReceivedAtUtc < cutoff && i.Subject != null)
            .OrderBy(i => i.ReceivedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var reply in replies) reply.Subject = null;
        var notifications = await _db.Notifications.Where(n => n.OrganizationId == organizationId && n.CreatedAtUtc < cutoff)
            .OrderBy(n => n.CreatedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        _db.Notifications.RemoveRange(notifications);
        // A finished run goes with its log, as a notification does.
        var runs = await OldAutomationRuns(organizationId, cutoff).OrderBy(r => r.FinishedAtUtc).Take(RowsPerRun).ToListAsync(ct);
        foreach (var slice in runs.Select(r => r.Id).Chunk(1000))
        {
            var chunk = slice.ToList();
            _db.AutomationRunSteps.RemoveRange(await _db.AutomationRunSteps.Where(x => chunk.Contains(x.RunId)).ToListAsync(ct));
        }
        _db.AutomationRuns.RemoveRange(runs);
        await _db.SaveChangesAsync(ct);
        return lines.Count + hooks.Count + tickets.Count + replies.Count + notifications.Count + runs.Count;
    }

    private static RetentionRunDto ToDto(RetentionRun r) => new(
        r.Id, r.Trigger, r.ScheduledFor, r.Status, r.StartedAtUtc, r.FinishedAtUtc, r.StartedByName,
        r.LeadsDeleted, r.MembersAnonymised, r.MessagesPurged, r.DetailsPurged, r.Failures);

    private static string Plural(int n, string word) => n == 1 ? word : word + "s";

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
