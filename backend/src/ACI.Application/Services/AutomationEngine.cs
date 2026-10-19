using System.Text.Json;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public sealed record AutomationEvent(
    Guid? OrganizationId,
    Guid ActorUserId,
    string SourceKey,
    string? RecordName,
    Guid? LeadId = null,
    Guid? DealId = null,
    Guid? TaskId = null,
    string? Email = null,
    Guid? AssigneeId = null);

public interface IAutomationEngine
{
    Task DispatchAsync(string trigger, AutomationEvent evt, CancellationToken ct = default);
}

public interface IAutomationRepository
{
    Task<IReadOnlyList<AutomationRule>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<AutomationRule?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task SaveAsync(AutomationRule rule, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRule>> EnabledForTriggerAsync(Guid? organizationId, string trigger, CancellationToken ct = default);
    Task<bool> TryMarkFiredAsync(Guid ruleId, string sourceKey, CancellationToken ct = default);
    Task IncrementRunCountAsync(Guid ruleId, CancellationToken ct = default);
}

/// <summary>
/// Runs saved rules. A failure in one action does not undo the lead or deal that triggered it.
/// </summary>
public sealed class AutomationEngine : IAutomationEngine
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IAutomationRepository _rules;
    private readonly ITaskService _tasks;
    private readonly INotificationService _notifications;
    private readonly ILeadRepository _leads;
    private readonly IDealRepository _deals;
    private readonly IEmailSender _email;
    private readonly ISequenceService _sequences;
    private readonly ISequenceRepository _targets;
    private readonly IOrganizationRepository _organizations;
    private readonly ILogger<AutomationEngine> _logger;

    public AutomationEngine(
        IAutomationRepository rules,
        ITaskService tasks,
        INotificationService notifications,
        ILeadRepository leads,
        IDealRepository deals,
        IEmailSender email,
        ISequenceService sequences,
        ISequenceRepository targets,
        IOrganizationRepository organizations,
        ILogger<AutomationEngine> logger)
    {
        _rules = rules;
        _tasks = tasks;
        _notifications = notifications;
        _leads = leads;
        _deals = deals;
        _email = email;
        _sequences = sequences;
        _targets = targets;
        _organizations = organizations;
        _logger = logger;
    }

    public async Task DispatchAsync(string trigger, AutomationEvent evt, CancellationToken ct = default)
    {
        // Rules belong to an organization. A personal workspace has none of its own, and
        // the rows left with no organization are not any one person's to run.
        if (evt.OrganizationId == null) return;
        IReadOnlyList<AutomationRule> rules;
        try
        {
            rules = await _rules.EnabledForTriggerAsync(evt.OrganizationId, trigger, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load automations for {Trigger}", trigger);
            return;
        }

        foreach (var rule in rules)
        {
            try
            {
                if (!await _rules.TryMarkFiredAsync(rule.Id, evt.SourceKey, ct))
                    continue;
                await RunAsync(rule, evt, ct);
                await _rules.IncrementRunCountAsync(rule.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automation {RuleId} failed for {Source}", rule.Id, evt.SourceKey);
            }
        }
    }

    private async Task RunAsync(AutomationRule rule, AutomationEvent evt, CancellationToken ct)
    {
        var actions = JsonSerializer.Deserialize<List<StoredAction>>(rule.ActionsJson, Json) ?? [];
        foreach (var action in actions)
        {
            var type = (action.Type ?? "").Trim();
            var config = action.Config ?? new Dictionary<string, string>();
            switch (type)
            {
                case "create_task":
                    await CreateTaskAsync(config, evt, ct);
                    break;
                case "notify":
                    await NotifyAsync(rule, config, evt, ct);
                    break;
                case "assign":
                    await AssignAsync(config, evt, ct);
                    break;
                case "send_email":
                    await SendAsync(config, evt, ct);
                    break;
                case "add_to_sequence":
                    await EnrollAsync(config, evt, ct);
                    break;
            }
        }
    }

    private async Task CreateTaskAsync(IReadOnlyDictionary<string, string> config, AutomationEvent evt, CancellationToken ct)
    {
        var title = Value(config, "title");
        if (string.IsNullOrWhiteSpace(title)) title = "Follow up";
        var days = int.TryParse(Value(config, "dueInDays"), out var n) ? n : 1;
        await _tasks.CreateAsync(evt.ActorUserId, evt.OrganizationId, new CreateTaskRequest
        {
            Title = title,
            DueDateUtc = DateTime.UtcNow.AddDays(Math.Max(0, days)),
            LeadId = evt.LeadId,
            DealId = evt.DealId,
        }, ct);
    }

    private async Task NotifyAsync(AutomationRule rule, IReadOnlyDictionary<string, string> config, AutomationEvent evt, CancellationToken ct)
    {
        var recipient = evt.AssigneeId ?? evt.ActorUserId;
        var message = Value(config, "message");
        if (string.IsNullOrWhiteSpace(message)) message = rule.Name;
        var link = evt.LeadId is Guid lead ? $"/leads/{lead}" : evt.DealId is Guid deal ? $"/deals/{deal}" : "/tasks";
        await _notifications.CreateAsync(recipient, evt.OrganizationId, new CreateNotificationRequest(
            "system", rule.Name, message, link, SourceKey: $"auto:{rule.Id}:{evt.SourceKey}"), ct);
    }

    private async Task AssignAsync(IReadOnlyDictionary<string, string> config, AutomationEvent evt, CancellationToken ct)
    {
        if (!Guid.TryParse(Value(config, "userId"), out var userId)) return;
        // Checked when the rule runs, not only when it was saved: the person may have left since.
        if (evt.OrganizationId is not Guid organizationId
            || await _organizations.GetMemberRoleAsync(userId, organizationId, ct) == null) return;
        if (evt.LeadId is Guid leadId)
        {
            // AssignAsync, not UpdateAsync: the general update does not copy the assignee,
            // so this action used to save the lead unchanged.
            await _leads.AssignAsync(leadId, evt.ActorUserId, evt.OrganizationId, userId, ct);
        }
        else if (evt.DealId is Guid dealId)
        {
            var deal = await _deals.GetByIdAsync(dealId, evt.ActorUserId, evt.OrganizationId, ct);
            if (deal == null) return;
            deal.AssigneeId = userId;
            deal.UpdatedAtUtc = DateTime.UtcNow;
            await _deals.UpdateAsync(deal, evt.ActorUserId, evt.OrganizationId, ct);
        }
    }

    private async Task SendAsync(IReadOnlyDictionary<string, string> config, AutomationEvent evt, CancellationToken ct)
    {
        if (evt.LeadId is not Guid leadId) return;
        var subject = Value(config, "subject");
        var body = Value(config, "body");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body)) return;
        // The lead as it is now, read the way sequences read their targets: one that is
        // gone gets nothing, and the address is the record's own, so the same do-not-contact
        // rules apply here as there.
        var target = await _targets.TargetStateAsync("lead", leadId, evt.OrganizationId, ct);
        if (!target.Exists || target.DoNotContact || string.IsNullOrWhiteSpace(target.Email)) return;
        body = body.Replace("{{firstName}}", FirstName(target.Name ?? evt.RecordName), StringComparison.OrdinalIgnoreCase);
        await _email.SendMessageAsync(target.Email, target.Name ?? evt.RecordName ?? "", subject, body, ct);
    }

    private async Task EnrollAsync(IReadOnlyDictionary<string, string> config, AutomationEvent evt, CancellationToken ct)
    {
        if (!Guid.TryParse(Value(config, "sequenceId"), out var sequenceId)) return;
        if (evt.LeadId is not Guid leadId) return;
        await _sequences.EnrollAsync(evt.ActorUserId, evt.OrganizationId, new EnrollRequest(
            sequenceId, "lead", leadId, evt.RecordName ?? "", evt.Email), ct);
    }

    private static string? Value(IReadOnlyDictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var value) ? value : null;

    private static string FirstName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }

    private sealed class StoredAction
    {
        public string? Type { get; set; }
        public Dictionary<string, string>? Config { get; set; }
    }
}
