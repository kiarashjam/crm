using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// The bell's lines for work that comes to someone from a teammate: a task handed to
/// them, a comment on their task, a mention. See <see cref="ICollaborationNotifier"/>.
/// </summary>
/// <remarks>
/// There was none of this: a task could be handed to a rep, discussed, or a rep asked by
/// name for help in a note, and they heard of it only by opening the record. These are
/// in-app only. Email for a mention (the EmailOnTeamMention setting) belongs with the
/// outbox that sends every other email, and is not sent from here.
/// </remarks>
public sealed partial class CollaborationNotifier : ICollaborationNotifier
{
    private const int SnippetLength = 160;
    private const int MaxTitleLength = 512;

    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IWorkspaceRecords _records;
    private readonly ILogger<CollaborationNotifier> _logger;

    public CollaborationNotifier(
        INotificationRepository notifications,
        IUserRepository users,
        IOrganizationRepository organizations,
        IWorkspaceRecords records,
        ILogger<CollaborationNotifier> logger)
    {
        _notifications = notifications;
        _users = users;
        _organizations = organizations;
        _records = records;
        _logger = logger;
    }

    public async Task TaskAssignedAsync(TaskItem task, Guid actorId, string? madeBy = null, CancellationToken ct = default)
    {
        try
        {
            // In a personal workspace a task can only be its owner's own.
            if (task.AssigneeId is not Guid assignee || assignee == actorId || task.OrganizationId is not Guid org) return;
            if (!await IsMemberAsync(assignee, org, ct) || !await CanSeeDealAsync(assignee, org, task.DealId, ct)) return;
            var title = madeBy != null
                ? $"{Capitalized(madeBy)} gave you a task"
                : $"{await NameOfAsync(actorId, ct)} assigned you a task";
            await NotifyAsync(assignee, org, "task", title, task.Title, $"/tasks/{task.Id}", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell the assignee about task {TaskId}", task.Id);
        }
    }

    public async Task TasksAssignedAsync(Guid assigneeId, IReadOnlyList<TaskItem> tasks, Guid actorId, CancellationToken ct = default)
    {
        if (tasks.Count == 1)
        {
            await TaskAssignedAsync(tasks[0], actorId, null, ct);
            return;
        }
        try
        {
            if (tasks.Count == 0 || assigneeId == actorId || tasks[0].OrganizationId is not Guid org) return;
            if (!await IsMemberAsync(assigneeId, org, ct)) return;
            var visible = new List<TaskItem>();
            foreach (var task in tasks)
                if (await CanSeeDealAsync(assigneeId, org, task.DealId, ct)) visible.Add(task);
            if (visible.Count == 0) return;
            if (visible.Count == 1)
            {
                await TaskAssignedAsync(visible[0], actorId, null, ct);
                return;
            }
            // One line for the lot: fifty tasks handed over at once are one hand-over.
            var titles = string.Join("; ", visible.Take(3).Select(t => t.Title));
            var more = visible.Count > 3 ? $"; and {visible.Count - 3} more" : "";
            await NotifyAsync(assigneeId, org, "task",
                $"{await NameOfAsync(actorId, ct)} assigned you {visible.Count} tasks", titles + more, "/tasks", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell {AssigneeId} about {Count} tasks", assigneeId, tasks.Count);
        }
    }

    public async Task RecordAssignedAsync(string recordType, Guid recordId, string recordName, Guid? organizationId, Guid? assigneeId, Guid actorId, string? madeBy = null, CancellationToken ct = default)
    {
        try
        {
            if (assigneeId is not Guid assignee || assignee == actorId || organizationId is not Guid org) return;
            if (!await IsMemberAsync(assignee, org, ct)) return;
            if (recordType == RecordTypes.Deal && !await CanSeeDealAsync(assignee, org, recordId, ct)) return;
            var noun = Noun(recordType);
            var title = madeBy != null
                ? $"{Capitalized(madeBy)} gave you a {noun}"
                : $"{await NameOfAsync(actorId, ct)} assigned you a {noun}";
            await NotifyAsync(assignee, org, recordType, title, recordName, $"/{noun}s/{recordId}", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell the new owner about {RecordType} {RecordId}", recordType, recordId);
        }
    }

    public async Task RecordsAssignedAsync(string recordType, Guid assigneeId, Guid? organizationId, IReadOnlyList<(Guid Id, string Name)> records, Guid actorId, CancellationToken ct = default)
    {
        if (records.Count == 1)
        {
            await RecordAssignedAsync(recordType, records[0].Id, records[0].Name, organizationId, assigneeId, actorId, null, ct);
            return;
        }
        try
        {
            if (records.Count == 0 || assigneeId == actorId || organizationId is not Guid org) return;
            if (!await IsMemberAsync(assigneeId, org, ct)) return;
            var noun = Noun(recordType);
            var names = string.Join("; ", records.Take(3).Select(r => r.Name));
            var more = records.Count > 3 ? $"; and {records.Count - 3} more" : "";
            await NotifyAsync(assigneeId, org, recordType,
                $"{await NameOfAsync(actorId, ct)} assigned you {records.Count} {noun}s", names + more, $"/{noun}s", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell {AssigneeId} about {Count} {RecordType}s", assigneeId, records.Count, recordType);
        }
    }

    public async Task TaskCommentedAsync(TaskItem task, Guid commentId, string body, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            if (task.OrganizationId is not Guid org) return;
            var members = await _organizations.GetMembersAsync(org, ct);
            var memberIds = members.Select(m => m.UserId).ToHashSet();
            var actor = NameIn(members, actorId) ?? await NameOfAsync(actorId, ct);
            var snippet = Snippet(body);
            var link = $"/tasks/{task.Id}";

            var mentioned = Mentions.Find(body, People(members)).Where(id => id != actorId).ToList();
            foreach (var id in mentioned)
            {
                if (!await CanSeeDealAsync(id, org, task.DealId, ct)) continue;
                await NotifyAsync(id, org, "mention", $"{actor} mentioned you",
                    $"On the task \"{task.Title}\": {snippet}", link, $"mention:comment:{commentId}", ct);
            }

            // Whoever works the task and whoever made it, unless the mention already told them.
            foreach (var id in new[] { task.AssigneeId, task.UserId }.OfType<Guid>().Distinct())
            {
                if (id == actorId || mentioned.Contains(id) || !memberIds.Contains(id)) continue;
                if (!await CanSeeDealAsync(id, org, task.DealId, ct)) continue;
                await NotifyAsync(id, org, "task", $"{actor} commented on a task",
                    $"{task.Title}: {snippet}", link, $"comment:{commentId}", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell anyone about comment {CommentId} on task {TaskId}", commentId, task.Id);
        }
    }

    public async Task ActivityMentionsAsync(Activity activity, string? recordName, string? textBefore, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            // The app's own timeline lines mention nobody; in a personal workspace there is nobody to mention.
            if (activity.OrganizationId is not Guid org || string.Equals(activity.Type, "system", StringComparison.OrdinalIgnoreCase)) return;
            var text = Mentions.TextOf(activity.Subject, activity.Body);
            if (!text.Contains('@')) return;

            var members = await _organizations.GetMembersAsync(org, ct);
            var people = People(members);
            HashSet<Guid> already = textBefore == null ? new() : Mentions.Find(textBefore, people).ToHashSet();
            var mentioned = Mentions.Find(text, people).Where(id => id != actorId && !already.Contains(id)).ToList();
            if (mentioned.Count == 0) return;

            var actor = NameIn(members, actorId) ?? await NameOfAsync(actorId, ct);
            var link = activity.LeadId is Guid lead ? $"/leads/{lead}"
                : activity.DealId is Guid deal ? $"/deals/{deal}"
                : activity.ContactId is Guid contact ? $"/contacts/{contact}"
                : "/activities";
            var kind = activity.Type.Replace('_', ' ').ToLowerInvariant();
            var on = string.IsNullOrWhiteSpace(recordName) ? "" : $" on {recordName.Trim()}";
            var message = $"In a {kind}{on}: {Snippet(string.IsNullOrWhiteSpace(activity.Body) ? activity.Subject : activity.Body)}";
            foreach (var id in mentioned)
            {
                if (!await CanSeeDealAsync(id, org, activity.DealId, ct)) continue;
                await NotifyAsync(id, org, "mention", $"{actor} mentioned you", message, link, $"mention:activity:{activity.Id}", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell anyone about the mentions in activity {ActivityId}", activity.Id);
        }
    }

    private async Task NotifyAsync(Guid recipient, Guid organizationId, string type, string title, string? message, string link, string? sourceKey, CancellationToken ct)
    {
        // No settings saved means the defaults, which have the bell on.
        var settings = await _users.GetSettingsAsync(recipient, ct);
        if (settings != null && !settings.InAppNotificationsEnabled) return;
        // Told once per comment or note: editing it again does not tell them again.
        if (sourceKey != null && (await _notifications.GetExistingSourceKeysAsync(recipient, [sourceKey], ct)).Count > 0) return;
        try
        {
            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipient,
                OrganizationId = organizationId,
                Type = type,
                Title = title.Length <= MaxTitleLength ? title : title[..MaxTitleLength],
                Message = message,
                Link = link,
                Read = false,
                CreatedAtUtc = DateTime.UtcNow,
                SourceKey = sourceKey,
            }, ct);
        }
        catch (Exception ex)
        {
            // Another request wrote the same line between the check and this write.
            _logger.LogDebug(ex, "Skipped notification {SourceKey} for {UserId}", sourceKey, recipient);
        }
    }

    private async Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct) =>
        await _organizations.GetMemberRoleAsync(userId, organizationId, ct) != null;

    /// <summary>Whether this person can open work on the deal: a private deal's work is as hidden as the deal.</summary>
    private async Task<bool> CanSeeDealAsync(Guid userId, Guid organizationId, Guid? dealId, CancellationToken ct) =>
        dealId is not Guid deal || await _records.ExistsAsync(RecordTypes.Deal, deal, userId, organizationId, ct);

    private async Task<string> NameOfAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return DisplayName(user?.Name, user?.Email);
    }

    private static string? NameIn(IReadOnlyList<OrganizationMember> members, Guid userId)
    {
        var member = members.FirstOrDefault(m => m.UserId == userId);
        return member?.User == null ? null : DisplayName(member.User.Name, member.User.Email);
    }

    private static string DisplayName(string? name, string? email) =>
        !string.IsNullOrWhiteSpace(name) ? name.Trim() : !string.IsNullOrWhiteSpace(email) ? email.Trim() : "Someone";

    private static IEnumerable<Mentions.Person> People(IReadOnlyList<OrganizationMember> members) =>
        members.Select(m => new Mentions.Person(m.UserId, m.User?.Name, m.User?.Email));

    /// <summary>"lead" or "deal": also the bell's type for it and, with an s, the page it is on.</summary>
    private static string Noun(string recordType) => recordType == RecordTypes.Deal ? "deal" : "lead";

    private static string Capitalized(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>The first words of a comment or note, on one line.</summary>
    private static string Snippet(string? text)
    {
        var line = Spaces().Replace(text ?? "", " ").Trim();
        return line.Length <= SnippetLength ? line : line[..(SnippetLength - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
