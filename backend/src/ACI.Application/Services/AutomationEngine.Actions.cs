using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>What each action step does, and what it says it did in the run's log.</summary>
/// <remarks>
/// An action that has nothing to do for a reason that is not a failure (the lead has no
/// address, the person asked not to be emailed, the rep left the workspace) says so and the
/// run goes on: trying again would change nothing. Only an error is tried again.
/// </remarks>
public sealed partial class AutomationEngine
{
    /// <summary>The club's records: a member, their renewal, booking, place at an event, application or payment.</summary>
    private static bool IsClubRecord(AutomationRun run) => run.RecordType is AutomationRecords.Member or AutomationRecords.Subscription
        or AutomationRecords.Reservation or AutomationRecords.Registration or AutomationRecords.Application or AutomationRecords.Payment;

    private async Task<Outcome> CreateTaskAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        var title = Value(config, "title");
        if (string.IsNullOrWhiteSpace(title)) title = "Follow up";
        title = title.Trim();
        var days = int.TryParse(Value(config, "dueInDays"), out var n) ? n : 1;
        AutomationRecordView? view = null;
        if (IsClubRecord(run))
        {
            view = await ViewAsync(run, ct);
            if (view.Gone != null) return Outcome.End(view.Gone);
        }
        // Assigned to whoever the rule names, while they are in the workspace; else to whoever
        // works the lead or deal. It used to go to whoever set the rule off — the importer, or
        // the workspace owner for a webhook lead — so the rep the lead was routed to never saw
        // the task. With nobody, the person who set it off keeps it, as before.
        var assignee = await ChosenAsync(run, Value(config, "assigneeId"), ct) ?? await OwnerAsync(run, ct);
        var priority = (Value(config, "priority") ?? "").Trim().ToLowerInvariant();
        var created = await _tasks.CreateAsync(run.ActorUserId, run.OrganizationId, new CreateTaskRequest
        {
            Title = title,
            DueDateUtc = Now().AddDays(Math.Max(0, days)),
            Priority = priority is "low" or "medium" or "high" ? priority : null,
            LeadId = run.LeadId,
            DealId = run.DealId,
            // A club record has no task link of its own: the task names it, and sits on the
            // member's contact when they have one.
            ContactId = view?.ContactId,
            Description = view == null ? null : Clip($"About {view.Name}.", 2000),
            AssigneeId = assignee,
            // The rep it goes to hears it came from this rule, not from whoever set it off.
            MadeBy = $"the automation \"{run.RuleName}\"",
            FromImport = run.FromImport,
        }, ct);
        // The task service answers an exception it caught (the database could not save the task)
        // with a server error: that is tried again later, like a step that throws. Its other
        // refusals (a link outside the workspace, say) would be the same next time, so they are
        // logged and the run goes on.
        if (created.IsFailure)
            return created.Error.Code == DomainErrors.General.ServerError.Code
                ? Outcome.Failed("The task could not be saved.")
                : Outcome.Skip($"No task was created: {created.Error.Description}");
        return Outcome.Did($"Created the task \"{title}\".", taskId: created.Value.Id);
    }

    /// <summary>
    /// Who works the record now — read fresh, so an assign earlier in the rule counts — or
    /// else the owner the event was raised with; only someone still in the workspace. Null
    /// when there is nobody.
    /// </summary>
    private async Task<Guid?> OwnerAsync(AutomationRun run, CancellationToken ct)
    {
        if (run.OrganizationId is not Guid organizationId) return null;
        Guid? current = null;
        if (run.LeadId is Guid leadId)
            current = (await _targets.TargetStateAsync("lead", leadId, run.OrganizationId, ct)).OwnerUserId;
        else if (run.DealId is Guid dealId)
            current = (await _deals.GetByIdAsync(dealId, run.ActorUserId, run.OrganizationId, ct))?.AssigneeId;
        foreach (var candidate in new[] { current, run.AssigneeId })
        {
            if (candidate is Guid userId && await _organizations.GetMemberRoleAsync(userId, organizationId, ct) != null)
                return userId;
        }
        return null;
    }

    /// <summary>The person a step names (a task's assignee, a notification's recipient), while they are in the workspace.</summary>
    private async Task<Guid?> ChosenAsync(AutomationRun run, string? raw, CancellationToken ct) =>
        Guid.TryParse(raw, out var userId) && run.OrganizationId is Guid organizationId
            && await _organizations.GetMemberRoleAsync(userId, organizationId, ct) != null
            ? userId
            : null;

    /// <summary>
    /// The rep a rule hands a lead or deal to hears so, and that the rule did it; they
    /// used to find out only by opening it. Not for an imported row: an import is not
    /// new work arriving, and one line per row would bury the bell.
    /// </summary>
    private Task TellNewOwnerAsync(AutomationRun run, string recordType, Guid recordId, string recordName, Guid ownerId, CancellationToken ct) =>
        _notifier == null || run.FromImport
            ? Task.CompletedTask
            : _notifier.RecordAssignedAsync(recordType, recordId, recordName, run.OrganizationId, ownerId, run.ActorUserId, $"the automation \"{run.RuleName}\"", ct);

    private async Task<Outcome> NotifyAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        var recipient = await ChosenAsync(run, Value(config, "userId"), ct) ?? run.AssigneeId ?? run.ActorUserId;
        var message = Value(config, "message");
        if (string.IsNullOrWhiteSpace(message)) message = run.RuleName;
        string link;
        if (run.LeadId is Guid lead) link = $"/leads/{lead}";
        else if (run.DealId is Guid deal) link = $"/deals/{deal}";
        else if (IsClubRecord(run)) link = (await ViewAsync(run, ct)).Link;
        else link = "/tasks";
        // Keyed by the run's step: a step tried again after the notice went does not send a second.
        await _notifications.CreateAsync(recipient, run.OrganizationId, new CreateNotificationRequest(
            "system", run.RuleName, message.Trim(), link, SourceKey: $"auto:{run.Id}:{run.CurrentPath}"), ct);
        return Outcome.Did("Sent a notification.");
    }

    private async Task<Outcome> AssignAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        if (!Guid.TryParse(Value(config, "userId"), out var userId)) return Outcome.Skip("The step names nobody to assign to.");
        // Checked when the rule runs, not only when it was saved: the person may have left since.
        if (run.OrganizationId is not Guid organizationId
            || await _organizations.GetMemberRoleAsync(userId, organizationId, ct) == null)
            return Outcome.Skip("The person this step assigns to is no longer in the workspace.");
        if (run.LeadId is Guid leadId)
        {
            var before = (await _targets.TargetStateAsync("lead", leadId, run.OrganizationId, ct)).OwnerUserId;
            // AssignAsync, not UpdateAsync: the general update does not copy the assignee,
            // so this action used to save the lead unchanged.
            var lead = await _leads.AssignAsync(leadId, run.ActorUserId, run.OrganizationId, userId, ct);
            if (lead == null) return Outcome.Skip("The lead is gone, so it was not assigned.");
            if (before != userId)
                await TellNewOwnerAsync(run, RecordTypes.Lead, leadId, lead.Name, userId, ct);
            return Outcome.Did("Assigned the lead.");
        }
        if (run.DealId is Guid dealId)
        {
            var deal = await _deals.GetByIdAsync(dealId, run.ActorUserId, run.OrganizationId, ct);
            if (deal == null) return Outcome.Skip("The deal is gone, so it was not assigned.");
            var before = deal.AssigneeId;
            deal.AssigneeId = userId;
            deal.UpdatedAtUtc = Now();
            await _deals.UpdateAsync(deal, run.ActorUserId, run.OrganizationId, ct);
            if (before != userId)
                await TellNewOwnerAsync(run, RecordTypes.Deal, dealId, deal.Name, userId, ct);
            return Outcome.Did("Assigned the deal.");
        }
        return Outcome.Skip("Only a lead or a deal can be assigned.");
    }

    private async Task<Outcome> SendAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        if (run.OrganizationId is not Guid organizationId) return Outcome.Skip("The run has no workspace.");
        var subject = Value(config, "subject");
        var body = Value(config, "body");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body)) return Outcome.Skip("The email step has no subject or no body.");

        if (IsClubRecord(run))
        {
            var view = await ViewAsync(run, ct);
            if (view.Gone != null) return Outcome.End(view.Gone);
            if (view.MemberId is not Guid memberId || string.IsNullOrWhiteSpace(view.MemberEmail))
                return Outcome.Skip("There is no member with an email address to write to.");
            // A member's email is for marketing unless the rule says it is about their
            // membership: marketing goes only to a member who agreed to it by email, and a
            // service message (a renewal reminder, a booking) goes whatever they chose. The
            // outbox applies the member's consent and adds the link that withdraws it.
            var purpose = string.Equals(Value(config, "purpose"), ConsentPurposes.Service, StringComparison.OrdinalIgnoreCase)
                ? ConsentPurposes.Service
                : ConsentPurposes.Marketing;
            var toMember = await _outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.Automation,
                SourceId = run.RuleId,
                To = [view.MemberEmail.Trim()],
                RecipientName = view.MemberName,
                Subject = subject,
                Body = body,
                CreatedByUserId = run.ActorUserId,
                ReplyToUserId = run.ActorUserId,
                MemberId = memberId,
                Purpose = purpose,
            }, ct);
            if (toMember.IsFailure) return Outcome.Skip($"No email was sent: {toMember.Error.Description}");
            return Outcome.Did($"Queued the email \"{toMember.Value.Subject}\" to {view.MemberEmail.Trim()}.", messageId: toMember.Value.Id);
        }

        if (run.LeadId is not Guid leadId)
            return Outcome.Skip(run.DealId != null
                ? "A deal has nobody of its own to write to: an email step writes to a lead."
                : "There is no lead to write to.");
        // The lead as it is now, read the way sequences read their targets: one that is
        // gone gets nothing, and the address is the record's own.
        var target = await _targets.TargetStateAsync("lead", leadId, run.OrganizationId, ct);
        if (!target.Exists) return Outcome.Skip("The lead is gone, so no email was sent.");
        if (string.IsNullOrWhiteSpace(target.Email)) return Outcome.Skip("The lead has no email address.");
        // Queued, not sent: the mail server is no longer waited on inside the save that set
        // the rule off (a public form, a webhook lead), and a send that fails is retried
        // instead of lost. The outbox fills every placeholder, in the subject too, refuses
        // anyone on the suppression list, sends it under the workspace's name with replies
        // to whoever works the lead (or whoever set the rule off), and puts it on the timeline.
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.Automation,
            SourceId = run.RuleId,
            To = [target.Email],
            RecipientName = target.Name ?? run.RecordName,
            Subject = subject,
            Body = body,
            CreatedByUserId = run.ActorUserId,
            ReplyToUserId = await OwnerAsync(run, ct) ?? run.ActorUserId,
            LeadId = leadId,
        }, ct);
        if (queued.IsFailure)
        {
            _logger.LogInformation("Automation {RuleId} sent no email for {Source}: {Reason}", run.RuleId, run.SourceKey, queued.Error.Description);
            return Outcome.Skip($"No email was sent: {queued.Error.Description}");
        }
        return Outcome.Did($"Queued the email \"{queued.Value.Subject}\" to {target.Email}.", messageId: queued.Value.Id);
    }

    private async Task<Outcome> EnrollAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, CancellationToken ct)
    {
        if (!Guid.TryParse(Value(config, "sequenceId"), out var sequenceId)) return Outcome.Skip("The step names no sequence.");
        if (run.LeadId is not Guid leadId) return Outcome.Skip("Only a lead can be enrolled in a sequence.");
        // Outreach sequences are the Sales module's. With it turned off a lead can still arrive
        // (a website form keeps posting them), but enrolling it would start emails nobody could
        // see or stop, the Sequences page being gone; turning Sales off is refused while a lead
        // is in a sequence for that reason (WorkspaceModuleService).
        if (run.OrganizationId is Guid org && _modules != null && !await _modules.IsOnAsync(org, WorkspaceModules.Sales, ct))
        {
            _logger.LogInformation("Automation enrolled nobody for {Source}: the workspace has turned Sales off", run.SourceKey);
            return Outcome.Skip("The Sales module is turned off, so nobody was enrolled.");
        }
        var enrolled = await _sequences.EnrollAsync(run.ActorUserId, run.OrganizationId, new EnrollRequest(
            sequenceId, "lead", leadId, run.RecordName ?? "", run.Email), ct);
        return enrolled.IsSuccess
            ? Outcome.Did("Enrolled the lead in the sequence.")
            : Outcome.Skip($"The lead was not enrolled: {enrolled.Error.Description}");
    }
}
