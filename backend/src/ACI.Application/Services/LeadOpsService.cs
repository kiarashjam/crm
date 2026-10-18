using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public interface ILeadOpsService
{
    Task<IReadOnlyList<ScoringRuleDto>> GetScoringRulesAsync(Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<ScoringRuleDto>> SaveScoringRulesAsync(Guid? organizationId, IReadOnlyList<ScoringRuleDto> rules, CancellationToken ct = default);
    Task<IReadOnlyList<AssignmentRuleDto>> GetAssignmentRulesAsync(Guid? organizationId, CancellationToken ct = default);
    Task<AssignmentRuleDto> SaveAssignmentRuleAsync(Guid? organizationId, AssignmentRuleDto rule, CancellationToken ct = default);
    Task DeleteAssignmentRuleAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<TerritoryDto>> GetTerritoriesAsync(Guid? organizationId, CancellationToken ct = default);
    Task<TerritoryDto> SaveTerritoryAsync(Guid? organizationId, TerritoryDto territory, CancellationToken ct = default);
    Task DeleteTerritoryAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<QueueDto>> GetQueuesAsync(Guid? organizationId, CancellationToken ct = default);
    Task<QueueDto> SaveQueueAsync(Guid? organizationId, QueueDto queue, CancellationToken ct = default);
    Task DeleteQueueAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<SlaSettingsDto> GetSlaAsync(Guid? organizationId, CancellationToken ct = default);
    Task<SlaSettingsDto> SetSlaAsync(Guid? organizationId, int hours, CancellationToken ct = default);
    Task<IReadOnlyList<CaptureFormDto>> GetFormsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<CaptureFormDto> CreateFormAsync(Guid userId, Guid? organizationId, string name, CancellationToken ct = default);
    Task<PublicCaptureFormDto?> GetPublicFormAsync(string token, CancellationToken ct = default);
    Task<Result<PublicCaptureReceipt>> CaptureAsync(string token, PublicCaptureRequest request, CancellationToken ct = default);
    /// <summary>
    /// When an open lead with this email already exists, record the new inquiry on
    /// it and tell its owner, and return its id. Null when there is no such lead.
    /// </summary>
    Task<Guid?> AttachInquiryAsync(Guid? organizationId, Guid actingUserId, string email, string source, string summary, CancellationToken ct = default);
    Task<IReadOnlyList<LeadStatusEventDto>> HistoryAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Result<BulkLeadUpdateResult>> BulkUpdateAsync(Guid userId, Guid? organizationId, BulkLeadUpdateRequest request, CancellationToken ct = default);
    Task<ConversionReportDto> ConversionReportAsync(Guid? organizationId, CancellationToken ct = default);
    Task NotifyOverdueAsync(CancellationToken ct = default);
    Task<bool> RecordEmailOpenAsync(Guid leadId, CancellationToken ct = default);
    Task<int> RescoreOpenLeadsAsync(CancellationToken ct = default);
}

public sealed class LeadOpsService : ILeadOpsService
{
    private readonly ILeadOpsRepository _ops;
    private readonly ILeadService _leads;
    private readonly ILeadRepository _leadRepository;
    private readonly INotificationService _notifications;
    private readonly ILeadAutomation _automation;
    private readonly IActivityService _activities;

    public LeadOpsService(
        ILeadOpsRepository ops,
        ILeadService leads,
        ILeadRepository leadRepository,
        INotificationService notifications,
        ILeadAutomation automation,
        IActivityService activities)
    {
        _ops = ops;
        _leads = leads;
        _leadRepository = leadRepository;
        _notifications = notifications;
        _automation = automation;
        _activities = activities;
    }

    public async Task<IReadOnlyList<ScoringRuleDto>> GetScoringRulesAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rules = await _ops.GetScoringRulesAsync(organizationId, ct);
        // With none saved, show the built-ins — which is what scoring already uses
        // when an organisation has no active rules — WITHOUT writing them. Seeding
        // on read meant two first loads at once each inserted a full set, and every
        // built-in rule then counted twice.
        if (rules.Count == 0)
            return LeadScoreCalculator.BuiltinRules().Select(ToDto).ToList();
        return rules.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ScoringRuleDto>> SaveScoringRulesAsync(Guid? organizationId, IReadOnlyList<ScoringRuleDto> rules, CancellationToken ct = default)
    {
        var rows = rules.Select(r => new LeadScoringRule
        {
            // Always a new id: this is a replace-all, and reusing a client-supplied id
            // could collide with a row belonging to another organisation.
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Field = string.IsNullOrWhiteSpace(r.Field) ? "source" : r.Field.Trim(),
            MatchValue = r.MatchValue?.Trim(),
            Points = r.Points,
            IsActive = r.IsActive,
        }).ToList();
        await _ops.ReplaceScoringRulesAsync(organizationId, rows, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AssignmentRuleDto>> GetAssignmentRulesAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rules = await _ops.GetAssignmentRulesAsync(organizationId, ct);
        return rules.Select(ToDto).ToList();
    }

    public async Task<AssignmentRuleDto> SaveAssignmentRuleAsync(Guid? organizationId, AssignmentRuleDto rule, CancellationToken ct = default)
    {
        var row = new LeadAssignmentRule
        {
            Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id,
            OrganizationId = organizationId,
            Name = string.IsNullOrWhiteSpace(rule.Name) ? "Assignment rule" : rule.Name.Trim(),
            Kind = string.IsNullOrWhiteSpace(rule.Kind) ? "RoundRobin" : rule.Kind.Trim(),
            Priority = rule.Priority,
            SourceMatch = rule.SourceMatch?.Trim(),
            TerritoryId = rule.TerritoryId,
            QueueId = rule.QueueId,
            MemberUserIds = LeadAutomation.JoinIds(rule.MemberUserIds ?? Array.Empty<Guid>()),
            IsActive = rule.IsActive,
        };
        var existing = (await _ops.GetAssignmentRulesAsync(organizationId, ct)).FirstOrDefault(r => r.Id == row.Id);
        if (existing != null) row.LastIndex = existing.LastIndex;
        // An id that is not one of THIS organisation's rules is a new rule, never an
        // edit: the repository only ever updates within the organisation.
        else row.Id = Guid.NewGuid();
        await _ops.SaveAssignmentRuleAsync(row, ct);
        return ToDto(row);
    }

    public Task DeleteAssignmentRuleAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _ops.DeleteAssignmentRuleAsync(id, organizationId, ct);

    public async Task<IReadOnlyList<TerritoryDto>> GetTerritoriesAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _ops.GetTerritoriesAsync(organizationId, ct)).Select(t => new TerritoryDto(t.Id, t.Name, t.Countries, t.Industries)).ToList();

    public async Task<TerritoryDto> SaveTerritoryAsync(Guid? organizationId, TerritoryDto territory, CancellationToken ct = default)
    {
        var row = new LeadTerritory
        {
            Id = territory.Id == Guid.Empty ? Guid.NewGuid() : territory.Id,
            OrganizationId = organizationId,
            Name = string.IsNullOrWhiteSpace(territory.Name) ? "Territory" : territory.Name.Trim(),
            Countries = territory.Countries?.Trim(),
            Industries = territory.Industries?.Trim(),
        };
        await _ops.SaveTerritoryAsync(row, ct);
        return new TerritoryDto(row.Id, row.Name, row.Countries, row.Industries);
    }

    public Task DeleteTerritoryAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _ops.DeleteTerritoryAsync(id, organizationId, ct);

    public async Task<IReadOnlyList<QueueDto>> GetQueuesAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _ops.GetQueuesAsync(organizationId, ct))
            .Select(q => new QueueDto(q.Id, q.Name, LeadAutomation.ParseIds(q.MemberUserIds)))
            .ToList();

    public async Task<QueueDto> SaveQueueAsync(Guid? organizationId, QueueDto queue, CancellationToken ct = default)
    {
        var row = new LeadQueue
        {
            Id = queue.Id == Guid.Empty ? Guid.NewGuid() : queue.Id,
            OrganizationId = organizationId,
            Name = string.IsNullOrWhiteSpace(queue.Name) ? "Queue" : queue.Name.Trim(),
            MemberUserIds = LeadAutomation.JoinIds(queue.MemberUserIds ?? Array.Empty<Guid>()),
        };
        await _ops.SaveQueueAsync(row, ct);
        return new QueueDto(row.Id, row.Name, LeadAutomation.ParseIds(row.MemberUserIds));
    }

    public Task DeleteQueueAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _ops.DeleteQueueAsync(id, organizationId, ct);

    public async Task<SlaSettingsDto> GetSlaAsync(Guid? organizationId, CancellationToken ct = default) =>
        new(await _ops.GetSlaHoursAsync(organizationId, ct));

    public async Task<SlaSettingsDto> SetSlaAsync(Guid? organizationId, int hours, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(hours, 1, 168);
        await _ops.SetSlaHoursAsync(organizationId, clamped, ct);
        return new SlaSettingsDto(clamped);
    }

    public async Task<IReadOnlyList<CaptureFormDto>> GetFormsAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _ops.GetFormsAsync(organizationId, ct)).Select(ToForm).ToList();

    public async Task<CaptureFormDto> CreateFormAsync(Guid userId, Guid? organizationId, string name, CancellationToken ct = default)
    {
        var form = new LeadCaptureForm
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? "Website" : name.Trim(),
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            SuccessMessage = "Thanks. We'll be in touch.",
        };
        await _ops.SaveFormAsync(form, ct);
        return ToForm(form);
    }

    public async Task<PublicCaptureFormDto?> GetPublicFormAsync(string token, CancellationToken ct = default)
    {
        var form = await _ops.GetFormByTokenAsync(token, ct);
        return form == null ? null : new PublicCaptureFormDto(form.Name, form.SuccessMessage);
    }

    /// <summary>
    /// A stranger's submission from a public form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anyone holding the form's token — which is on a public website — can call
    /// this, so it reveals nothing. It used to answer a duplicate email with
    /// "An open lead with this email already exists (Jane Doe)": a lookup of any
    /// address's name for anyone who asked. And a success returned the whole lead,
    /// with the matched company (which companies the workspace holds), the
    /// assigned rep, the score and internal ids. Now every outcome a visitor can
    /// cause gets the same short receipt.
    /// </para>
    /// <para>
    /// A returning prospect is not an error. Their second inquiry used to be
    /// rejected and lost; it is now recorded on the lead they already have.
    /// </para>
    /// </remarks>
    public async Task<Result<PublicCaptureReceipt>> CaptureAsync(string token, PublicCaptureRequest request, CancellationToken ct = default)
    {
        var form = await _ops.GetFormByTokenAsync(token, ct);
        if (form == null) return DomainErrors.Lead.NotFound;
        var receipt = new PublicCaptureReceipt(string.IsNullOrWhiteSpace(form.SuccessMessage) ? "Thanks. We'll be in touch." : form.SuccessMessage);

        // The hidden honeypot field: a person never sees it, a bot fills it.
        if (!string.IsNullOrWhiteSpace(request.Website)) return receipt;

        // Leads.Source is nvarchar(64) and a form name may be up to 120. Unclipped,
        // a form with a long name failed on every single submission.
        var source = Clip(form.Name.Trim(), 64);
        var summary = Summary(request);

        if (await AttachInquiryAsync(form.OrganizationId, form.CreatedByUserId, request.Email, source, summary, ct) != null)
            return receipt;

        var created = await _leads.CreateAsync(form.CreatedByUserId, form.OrganizationId, new CreateLeadRequest
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            Status = "New",
            Source = source,
            JobTitle = request.JobTitle?.Trim(),
            Country = request.Country?.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Company) ? null : $"Company given on the form: {request.Company.Trim()}",
            // Checked just above; a lead created between the two checks is a
            // duplicate the lead list can merge, not a reason to lose the inquiry.
            AllowDuplicate = true,
        }, ct);
        return created.IsSuccess
            ? receipt
            : new Error("Lead.CaptureFailed", "That could not be saved. Please check the details and try again.");
    }

    public async Task<Guid?> AttachInquiryAsync(Guid? organizationId, Guid actingUserId, string email, string source, string summary, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var existing = await _automation.FindOpenDuplicateAsync(organizationId, email, null, ct);
        if (existing == null) return null;

        // An inbound inquiry, not our response to them — so automated: it must not
        // clear the "nobody has replied yet" deadline.
        await _activities.CreateAsync(actingUserId, organizationId, new CreateActivityRequest
        {
            Type = "note",
            Subject = $"New inquiry via {source}",
            Body = summary,
            LeadId = existing.Id,
            IsAutomated = true,
        }, ct);
        await _notifications.CreateAsync(existing.AssignedToUserId ?? existing.UserId, existing.OrganizationId, new CreateNotificationRequest(
            "lead_inquiry",
            "A lead got in touch again",
            $"{existing.Name} sent a new inquiry via {source}.",
            $"/leads/{existing.Id}"), ct);
        return existing.Id;
    }

    private static string Summary(PublicCaptureRequest r)
    {
        var lines = new List<string> { $"Name: {r.Name.Trim()}", $"Email: {r.Email.Trim()}" };
        if (!string.IsNullOrWhiteSpace(r.Phone)) lines.Add($"Phone: {r.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Company)) lines.Add($"Company: {r.Company.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.JobTitle)) lines.Add($"Job title: {r.JobTitle.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Country)) lines.Add($"Country: {r.Country.Trim()}");
        return string.Join("\n", lines);
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];

    public async Task<IReadOnlyList<LeadStatusEventDto>> HistoryAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var lead = await _leadRepository.GetByIdAsync(leadId, userId, organizationId, ct);
        if (lead == null) return Array.Empty<LeadStatusEventDto>();
        var events = await _ops.GetStatusEventsAsync(leadId, ct);
        return events.Select(e => new LeadStatusEventDto(e.Id, e.FromStatus, e.ToStatus, e.ChangedByUserId, e.ChangedAtUtc)).ToList();
    }

    public async Task<Result<BulkLeadUpdateResult>> BulkUpdateAsync(Guid userId, Guid? organizationId, BulkLeadUpdateRequest request, CancellationToken ct = default)
    {
        // The status is derived from the five-phase pipeline and is read-only by
        // design; setting it here made the badge disagree with the pipeline, and the
        // next pipeline edit silently overwrote it. Refused rather than ignored, so
        // a caller is never told "updated" about a field that did not change.
        if (!string.IsNullOrWhiteSpace(request.Status))
            return new Error("Lead.StatusIsDerived", "A lead's status follows its pipeline and cannot be set in bulk.");
        var source = request.Source?.Trim();
        // Checked before the first lead is touched: Leads.Source is nvarchar(64), and
        // a longer value used to fail mid-batch with some leads already saved.
        if (source is { Length: > 64 })
            return new Error("Lead.SourceTooLong", "A source can be at most 64 characters.");

        var updated = 0;
        var missing = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var lead = await _leadRepository.GetByIdAsync(id, userId, organizationId, ct);
            if (lead == null || lead.IsConverted)
            {
                missing++;
                continue;
            }
            if (!string.IsNullOrWhiteSpace(source)) lead.Source = source;
            if (request.AssignedToUserId != null) lead.AssignedToUserId = request.AssignedToUserId;
            if (request.ClearQueue) lead.QueueId = null;
            else if (request.QueueId != null) lead.QueueId = request.QueueId;
            lead.UpdatedAtUtc = DateTime.UtcNow;
            lead.UpdatedByUserId = userId;
            await _automation.RescoreAsync(lead, ct);
            await _leadRepository.UpdateAsync(lead, userId, organizationId, ct);
            updated++;
        }
        return new BulkLeadUpdateResult(updated, missing);
    }

    public async Task<ConversionReportDto> ConversionReportAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var leads = await _ops.LeadsForReportAsync(organizationId, ct);
        return new ConversionReportDto(
            Slice(leads, l => string.IsNullOrWhiteSpace(l.Source) ? "Unknown" : l.Source!.Trim()),
            Slice(leads, l => l.AssignedToUserId?.ToString() ?? "Unassigned"));
    }

    public Task<int> RescoreOpenLeadsAsync(CancellationToken ct = default) =>
        _ops.RescoreOpenLeadsAsync(DateTime.UtcNow, ct);

    public async Task NotifyOverdueAsync(CancellationToken ct = default)
    {
        var due = await _ops.OverdueFirstResponseAsync(DateTime.UtcNow, 100, ct);
        foreach (var lead in due)
        {
            lead.SlaBreached = true;
            lead.SlaNotifiedAtUtc = DateTime.UtcNow;
            await _ops.SaveLeadAsync(lead, ct);
            var recipient = lead.AssignedToUserId ?? lead.UserId;
            await _notifications.CreateAsync(recipient, lead.OrganizationId, new CreateNotificationRequest(
                "lead_sla",
                "First response is late",
                $"{lead.Name} has had no call or email since {lead.CreatedAtUtc:u}.",
                $"/leads/{lead.Id}",
                SourceKey: $"lead-sla:{lead.Id}"), ct);
        }
    }

    public async Task<bool> RecordEmailOpenAsync(Guid leadId, CancellationToken ct = default) =>
        await _ops.IncrementEmailOpensAsync(leadId, ct) > 0;

    private static IReadOnlyList<ConversionSliceDto> Slice(IReadOnlyList<Lead> leads, Func<Lead, string> key)
    {
        return leads.GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.ToList();
                // "Qualified" means met and still interested: Contract Pending or later
                // in the agreed stages. It used to look for "qualif" in the status,
                // which no current status contains — so Qualified always equalled
                // Converted, and "Unqualified" would have matched.
                var qualified = list.Count(l => l.IsConverted || LeadStatusVocabulary.ReachedStage(l.Status, "Contract Pending"));
                var converted = list.Count(l => l.IsConverted);
                var responses = list.Where(l => l.FirstResponseAtUtc != null)
                    .Select(l => (l.FirstResponseAtUtc!.Value - l.CreatedAtUtc).TotalHours)
                    .ToList();
                var conversions = list.Where(l => l.IsConverted && l.ConvertedAtUtc != null)
                    .Select(l => (l.ConvertedAtUtc!.Value - l.CreatedAtUtc).TotalHours)
                    .ToList();
                return new ConversionSliceDto(
                    g.Key,
                    list.Count,
                    qualified,
                    converted,
                    list.Count == 0 ? 0 : Math.Round(converted / (double)list.Count, 3),
                    responses.Count == 0 ? null : Math.Round(responses.Average(), 1),
                    conversions.Count == 0 ? null : Math.Round(conversions.Average(), 1));
            })
            .OrderByDescending(s => s.Created)
            .ToList();
    }

    private static ScoringRuleDto ToDto(LeadScoringRule r) =>
        new(r.Id, r.Field, r.MatchValue, r.Points, r.IsActive);

    private static AssignmentRuleDto ToDto(LeadAssignmentRule r) =>
        new(r.Id, r.Name, r.Kind, r.Priority, r.SourceMatch, r.TerritoryId, r.QueueId, LeadAutomation.ParseIds(r.MemberUserIds), r.IsActive);

    private static CaptureFormDto ToForm(LeadCaptureForm f) =>
        new(f.Id, f.Name, f.PublicToken, f.IsActive, f.SuccessMessage, $"/capture/{f.PublicToken}");
}
