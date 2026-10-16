using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.WebApi.Mcp;

/// <summary>
/// Read-only CRM tools exposed to MCP clients. Every call is already scoped to one organization.
/// </summary>
public sealed class CrmMcpTools
{
    public const string ServerVersion = "1.0.0";

    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;

    private readonly IGlobalSearchService _search;
    private readonly ILeadService _leads;
    private readonly IDealService _deals;
    private readonly IContactService _contacts;
    private readonly ITaskService _tasks;
    private readonly ILogger<CrmMcpTools> _logger;

    public CrmMcpTools(
        IGlobalSearchService search,
        ILeadService leads,
        IDealService deals,
        IContactService contacts,
        ITaskService tasks,
        ILogger<CrmMcpTools> logger)
    {
        _search = search;
        _leads = leads;
        _deals = deals;
        _contacts = contacts;
        _tasks = tasks;
        _logger = logger;
    }

    public static JsonArray Definitions() =>
    [
        Tool(
            "search_crm",
            "Search this organization's leads, contacts, companies, and deals by name, email, or company.",
            new JsonObject
            {
                ["query"] = StringProp("Text to search for."),
            },
            ["query"]),
        Tool(
            "list_leads",
            "List recent leads in this organization. Optionally filter by a search string.",
            new JsonObject
            {
                ["query"] = StringProp("Optional name, email, or company filter."),
                ["limit"] = LimitProp(),
            },
            []),
        Tool(
            "get_lead",
            "Get one lead by its id.",
            new JsonObject
            {
                ["id"] = StringProp("Lead id (GUID)."),
            },
            ["id"]),
        Tool(
            "list_contacts",
            "List contacts in this organization. Optionally filter by a search string.",
            new JsonObject
            {
                ["query"] = StringProp("Optional name, email, or company filter."),
                ["limit"] = LimitProp(),
            },
            []),
        Tool(
            "list_deals",
            "List deals in this organization. Optionally filter by a search string.",
            new JsonObject
            {
                ["query"] = StringProp("Optional deal, company, or contact filter."),
                ["limit"] = LimitProp(),
            },
            []),
        Tool(
            "list_tasks",
            "List tasks in this organization. Optionally filter by a search string.",
            new JsonObject
            {
                ["query"] = StringProp("Optional title filter."),
                ["limit"] = LimitProp(),
            },
            []),
        Tool(
            "organization_snapshot",
            "Summarize lead and task counts for this organization.",
            new JsonObject(),
            []),
    ];

    public async Task<JsonObject> CallAsync(Organization organization, string name, JsonObject? arguments, CancellationToken ct)
    {
        try
        {
            var text = name switch
            {
                "search_crm" => await SearchAsync(organization, arguments, ct),
                "list_leads" => await ListLeadsAsync(organization, arguments, ct),
                "get_lead" => await GetLeadAsync(organization, arguments, ct),
                "list_contacts" => await ListContactsAsync(organization, arguments, ct),
                "list_deals" => await ListDealsAsync(organization, arguments, ct),
                "list_tasks" => await ListTasksAsync(organization, arguments, ct),
                "organization_snapshot" => await SnapshotAsync(organization, ct),
                _ => null,
            };

            if (text == null)
                return ToolError($"Unknown tool '{name}'.");

            var isError = text.StartsWith("Error:", StringComparison.Ordinal);
            return ToolResult(text, isError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool {Tool} failed for organization {OrganizationId}", name, organization.Id);
            return ToolError("That lookup failed. Try again in a moment.");
        }
    }

    private async Task<string> SearchAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var query = RequiredString(arguments, "query");
        if (query == null)
            return "Error: query is required.";

        var result = await _search.SearchAsync(organization.OwnerUserId, organization.Id, query, ct);
        if (result.Leads.Count == 0 && result.Contacts.Count == 0 && result.Companies.Count == 0 && result.Deals.Count == 0)
            return $"No matches for \"{query}\" in {organization.Name}.";

        var lines = new List<string> { $"Matches in {organization.Name} for \"{query}\":" };
        AppendSection(lines, "Leads", result.Leads.Select(FormatLead));
        AppendSection(lines, "Contacts", result.Contacts.Select(FormatContact));
        AppendSection(lines, "Companies", result.Companies.Select(FormatCompany));
        AppendSection(lines, "Deals", result.Deals.Select(FormatDeal));
        return string.Join('\n', lines);
    }

    private async Task<string> ListLeadsAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var limit = ReadLimit(arguments);
        var page = await _leads.GetLeadsPagedAsync(
            organization.OwnerUserId,
            organization.Id,
            1,
            limit,
            new LeadQueryOptions { Search = OptionalString(arguments, "query") },
            ct);

        return FormatList(
            organization.Name,
            "Leads",
            page.TotalCount,
            page.Items.Select(FormatLead));
    }

    private async Task<string> GetLeadAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var idText = RequiredString(arguments, "id");
        if (idText == null)
            return "Error: id is required.";
        if (!Guid.TryParse(idText, out var id))
            return "Error: id must be a GUID.";

        var result = await _leads.GetByIdAsync(id, organization.OwnerUserId, organization.Id, ct);
        if (result.IsFailure)
            return "Error: lead was not found in this organization.";

        return FormatLead(result.Value);
    }

    private async Task<string> ListContactsAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var limit = ReadLimit(arguments);
        var page = await _contacts.GetContactsPagedAsync(
            organization.OwnerUserId,
            organization.Id,
            1,
            limit,
            OptionalString(arguments, "query"),
            false,
            null,
            ct);

        return FormatList(organization.Name, "Contacts", page.TotalCount, page.Items.Select(FormatContact));
    }

    private async Task<string> ListDealsAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var limit = ReadLimit(arguments);
        var page = await _deals.GetDealsPagedAsync(
            organization.OwnerUserId,
            organization.Id,
            1,
            limit,
            OptionalString(arguments, "query"),
            null,
            null,
            ct);

        return FormatList(organization.Name, "Deals", page.TotalCount, page.Items.Select(FormatDeal));
    }

    private async Task<string> ListTasksAsync(Organization organization, JsonObject? arguments, CancellationToken ct)
    {
        var limit = ReadLimit(arguments);
        var page = await _tasks.GetPagedAsync(
            organization.OwnerUserId,
            organization.Id,
            1,
            limit,
            OptionalString(arguments, "query"),
            null,
            ct);

        return FormatList(organization.Name, "Tasks", page.TotalCount, page.Items.Select(FormatTask));
    }

    private async Task<string> SnapshotAsync(Organization organization, CancellationToken ct)
    {
        var leads = await _leads.GetStatsAsync(organization.OwnerUserId, organization.Id, ct);
        var tasks = await _tasks.GetStatsAsync(organization.OwnerUserId, organization.Id, ct);
        return string.Join('\n',
        [
            $"Organization: {organization.Name}",
            $"Leads: {leads.Total} total, {leads.Active} active, {leads.NewLeads} new, {leads.Qualified} qualified, {leads.Converted} converted, {leads.HotLeads} hot.",
            $"Tasks: {tasks.Total} total, {tasks.Todo} to do, {tasks.InProgress} in progress, {tasks.Overdue} overdue, {tasks.DueToday} due today.",
        ]);
    }

    private static JsonObject Tool(string name, string description, JsonObject properties, string[] required)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
        if (required.Length > 0)
            schema["required"] = new JsonArray(required.Select(r => JsonValue.Create(r)).ToArray());

        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = schema,
            ["annotations"] = new JsonObject
            {
                ["readOnlyHint"] = true,
                ["destructiveHint"] = false,
            },
        };
    }

    private static JsonObject StringProp(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description,
    };

    private static JsonObject LimitProp() => new()
    {
        ["type"] = "integer",
        ["description"] = $"How many records to return (1–{MaxLimit}, default {DefaultLimit}).",
        ["minimum"] = 1,
        ["maximum"] = MaxLimit,
    };

    private static JsonObject ToolResult(string text, bool isError) => new()
    {
        ["content"] = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = text,
            },
        },
        ["isError"] = isError,
    };

    private static JsonObject ToolError(string text) => ToolResult(text, true);

    private static string? RequiredString(JsonObject? arguments, string name)
    {
        var value = OptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? OptionalString(JsonObject? arguments, string name)
    {
        if (arguments == null || !arguments.TryGetPropertyValue(name, out var node) || node == null)
            return null;
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            return null;
        var text = value.GetValue<string>().Trim();
        if (text.Length > 200)
            text = text[..200];
        return text.Length == 0 ? null : text;
    }

    private static int ReadLimit(JsonObject? arguments)
    {
        if (arguments != null &&
            arguments.TryGetPropertyValue("limit", out var node) &&
            node is JsonValue value &&
            value.TryGetValue<int>(out var limit))
        {
            return Math.Clamp(limit, 1, MaxLimit);
        }

        return DefaultLimit;
    }

    private static string FormatList(string orgName, string label, int total, IEnumerable<string> lines)
    {
        var items = lines.ToList();
        if (items.Count == 0)
            return $"No {label.ToLowerInvariant()} in {orgName}.";

        var header = $"{label} in {orgName} ({items.Count} of {total}):";
        return header + "\n" + string.Join('\n', items.Select(line => "- " + line));
    }

    private static void AppendSection(List<string> lines, string label, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return;
        lines.Add(label + ":");
        lines.AddRange(list.Select(line => "- " + line));
    }

    private static string FormatLead(LeadDto lead) =>
        $"{lead.Name} · {lead.Email} · {lead.Status} · {lead.CompanyName ?? "no company"} · id {lead.Id}";

    private static string FormatContact(ContactDto contact) =>
        $"{contact.Name} · {contact.Email} · {contact.CompanyName ?? contact.JobTitle ?? "contact"} · id {contact.Id}";

    private static string FormatCompany(CompanyDto company) =>
        $"{company.Name} · {company.Domain ?? company.Industry ?? "company"} · id {company.Id}";

    private static string FormatDeal(DealDto deal) =>
        $"{deal.Name} · {deal.Value} {deal.Currency ?? ""} · {deal.DealStageName ?? deal.Stage ?? "no stage"} · id {deal.Id}".Trim();

    private static string FormatTask(TaskDto task) =>
        $"{task.Title} · {task.Status} · {task.Priority} · due {task.DueDateUtc:yyyy-MM-dd} · id {task.Id}";
}
