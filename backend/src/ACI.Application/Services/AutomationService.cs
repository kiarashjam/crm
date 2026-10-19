using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public record AutomationActionDto(Guid Id, string Type, Dictionary<string, string> Config);

public record AutomationRuleDto(
    Guid Id,
    string Name,
    bool Enabled,
    string Trigger,
    Dictionary<string, string>? TriggerConfig,
    IReadOnlyList<AutomationActionDto> Actions,
    int RunCount,
    DateTime CreatedAtUtc);

public class AutomationWriteRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public string? Trigger { get; set; }
    public Dictionary<string, string>? TriggerConfig { get; set; }
    public List<AutomationActionWrite>? Actions { get; set; }
}

public class AutomationActionWrite
{
    public string? Type { get; set; }
    public Dictionary<string, string>? Config { get; set; }
}

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<AutomationRuleDto>> CreateAsync(Guid userId, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default);
    Task<Result<AutomationRuleDto>> UpdateAsync(Guid id, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
}

public sealed class AutomationService : IAutomationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Triggers = new(StringComparer.Ordinal)
    {
        "lead.created", "lead.status_changed", "deal.stage_changed", "deal.won", "deal.lost", "task.overdue",
    };
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "create_task", "send_email", "notify", "assign", "add_to_sequence",
    };

    private readonly IAutomationRepository _rules;

    public AutomationService(IAutomationRepository rules) => _rules = rules;

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _rules.ListAsync(organizationId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<AutomationRuleDto>> CreateAsync(Guid userId, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return new Error("Automation.NameRequired", "A rule needs a name.");
        var trigger = (request.Trigger ?? "").Trim();
        if (!Triggers.Contains(trigger)) return new Error("Automation.TriggerInvalid", "That trigger is not supported.");
        var actions = Normalize(request.Actions);
        if (actions.Count == 0) return new Error("Automation.ActionRequired", "A rule needs at least one action.");

        var row = new AutomationRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            Name = name,
            Enabled = request.Enabled ?? true,
            Trigger = trigger,
            TriggerConfigJson = request.TriggerConfig == null ? null : JsonSerializer.Serialize(request.TriggerConfig, Json),
            ActionsJson = JsonSerializer.Serialize(actions, Json),
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _rules.SaveAsync(row, ct);
        return ToDto(row);
    }

    public async Task<Result<AutomationRuleDto>> UpdateAsync(Guid id, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default)
    {
        var row = await _rules.GetAsync(id, organizationId, ct);
        if (row == null) return new Error("Automation.NotFound", "That rule was not found.");
        if (!string.IsNullOrWhiteSpace(request.Name)) row.Name = request.Name.Trim();
        if (request.Enabled.HasValue) row.Enabled = request.Enabled.Value;
        if (!string.IsNullOrWhiteSpace(request.Trigger))
        {
            if (!Triggers.Contains(request.Trigger)) return new Error("Automation.TriggerInvalid", "That trigger is not supported.");
            row.Trigger = request.Trigger;
        }
        if (request.TriggerConfig != null) row.TriggerConfigJson = JsonSerializer.Serialize(request.TriggerConfig, Json);
        if (request.Actions != null)
        {
            var actions = Normalize(request.Actions);
            if (actions.Count == 0) return new Error("Automation.ActionRequired", "A rule needs at least one action.");
            row.ActionsJson = JsonSerializer.Serialize(actions, Json);
        }
        await _rules.SaveAsync(row, ct);
        return ToDto(row);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var deleted = await _rules.DeleteAsync(id, organizationId, ct);
        return deleted ? Result.Success() : new Error("Automation.NotFound", "That rule was not found.");
    }

    private static List<AutomationActionDto> Normalize(List<AutomationActionWrite>? actions)
    {
        var list = new List<AutomationActionDto>();
        foreach (var action in actions ?? [])
        {
            var type = (action.Type ?? "").Trim();
            if (!Actions.Contains(type)) continue;
            list.Add(new AutomationActionDto(Guid.NewGuid(), type, action.Config ?? new Dictionary<string, string>()));
        }
        return list;
    }

    private static AutomationRuleDto ToDto(AutomationRule row)
    {
        var actions = JsonSerializer.Deserialize<List<AutomationActionDto>>(row.ActionsJson, Json) ?? [];
        var config = string.IsNullOrWhiteSpace(row.TriggerConfigJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(row.TriggerConfigJson, Json);
        return new AutomationRuleDto(row.Id, row.Name, row.Enabled, row.Trigger, config, actions, row.RunCount, row.CreatedAtUtc);
    }
}
