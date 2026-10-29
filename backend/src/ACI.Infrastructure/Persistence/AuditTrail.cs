using System.Text.RegularExpressions;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Turns the changes in a save into audit lines, written in the same transaction.
/// </summary>
/// <remarks>
/// <para>
/// Recording here rather than in each service means every way a record changes is
/// covered — the app, the website webhook, a public capture form, lead routing, a
/// background job — and none of them can forget to log.
/// </para>
/// <para>
/// Only fields a person would recognise as an edit are watched. Bookkeeping that
/// workers rewrite on a timer (SLA flags, reminder stamps, the derived lead score,
/// UpdatedAtUtc) never produces a line; otherwise the log would fill with a change
/// every minute and bury the ones that matter.
/// </para>
/// </remarks>
internal static partial class AuditTrail
{
    /// <param name="Label">For a record whose name is not one property: a member's two names, a charge's JSON.</param>
    private sealed record Spec(string EntityType, string Noun, string LabelProperty, string[] Fields, Func<EntityEntry, string?>? Label = null);

    private static readonly Dictionary<Type, Spec> Specs = new()
    {
        [typeof(Lead)] = new("lead", "Lead", nameof(Lead.Name), new[]
        {
            nameof(Lead.Name), nameof(Lead.Email), nameof(Lead.Phone), nameof(Lead.CompanyId), nameof(Lead.Source),
            nameof(Lead.LeadSourceId), nameof(Lead.Description), nameof(Lead.LifecycleStage), nameof(Lead.PipelineState),
            nameof(Lead.JobTitle), nameof(Lead.Country), nameof(Lead.Industry), nameof(Lead.CompanySize),
            nameof(Lead.QueueId), nameof(Lead.TerritoryId), nameof(Lead.ReferredByContactId),
        }),
        [typeof(Deal)] = new("deal", "Deal", nameof(Deal.Name), new[]
        {
            nameof(Deal.Name), nameof(Deal.Value), nameof(Deal.Currency), nameof(Deal.PipelineId), nameof(Deal.CompanyId),
            nameof(Deal.ContactId), nameof(Deal.ExpectedCloseDateUtc), nameof(Deal.Description), nameof(Deal.Probability),
            nameof(Deal.ClosedReason), nameof(Deal.ClosedReasonCategory),
        }),
        [typeof(Contact)] = new("contact", "Contact", nameof(Contact.Name), new[]
        {
            nameof(Contact.Name), nameof(Contact.Email), nameof(Contact.Phone), nameof(Contact.JobTitle),
            nameof(Contact.CompanyId), nameof(Contact.Description), nameof(Contact.DoNotContact),
            nameof(Contact.PreferredContactMethod),
        }),
        [typeof(Company)] = new("company", "Company", nameof(Company.Name), new[]
        {
            nameof(Company.Name), nameof(Company.Domain), nameof(Company.Industry), nameof(Company.Size),
            nameof(Company.Description), nameof(Company.Website), nameof(Company.Location),
        }),
        [typeof(TaskItem)] = new("task", "Task", nameof(TaskItem.Title), new[]
        {
            nameof(TaskItem.Title), nameof(TaskItem.Description), nameof(TaskItem.DueDateUtc),
            nameof(TaskItem.ReminderDateUtc), nameof(TaskItem.Priority), nameof(TaskItem.LeadId),
            nameof(TaskItem.DealId), nameof(TaskItem.ContactId), nameof(TaskItem.Notes),
        }),
    };

    private static readonly Dictionary<string, string> FieldNames = new(StringComparer.Ordinal)
    {
        [nameof(Lead.PipelineState)] = "pipeline progress",
        [nameof(Lead.LeadSourceId)] = "source",
        [nameof(Lead.ReferredByContactId)] = "referred by",
        [nameof(Contact.DoNotContact)] = "do-not-contact",
        [nameof(Deal.ClosedReasonCategory)] = "close reason",
        [nameof(Deal.ClosedReason)] = "close reason",
    };

    /// <summary>Names the summaries need, fetched in one query per kind.</summary>
    private sealed class Lookups
    {
        public HashSet<Guid> UserIds { get; } = new();
        public HashSet<Guid> StageIds { get; } = new();
        public HashSet<Guid> StatusIds { get; } = new();
        public Dictionary<Guid, string> Users { get; set; } = new();
        public Dictionary<Guid, string> Stages { get; set; } = new();
        public Dictionary<Guid, string> Statuses { get; set; } = new();

        public string? User(Guid? id) => id is { } g && Users.TryGetValue(g, out var n) ? n : null;
        public string? Stage(Guid? id) => id is { } g && Stages.TryGetValue(g, out var n) ? n : null;
        public string? Status(Guid? id) => id is { } g && Statuses.TryGetValue(g, out var n) ? n : null;
    }

    /// <summary>An event whose summary may need names resolved first.</summary>
    private sealed record Pending(EntityEntry Entry, Spec Spec, string Action, Func<Lookups, string> Summary);

    public static async Task<IReadOnlyList<AuditEvent>> CaptureAsync(AppDbContext db, ICurrentUserService? currentUser, CancellationToken ct)
    {
        var (pending, lookups) = Collect(db, currentUser);
        if (pending.Count == 0) return Array.Empty<AuditEvent>();
        if (lookups.UserIds.Count > 0)
            lookups.Users = await db.Users.AsNoTracking().Where(u => lookups.UserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        if (lookups.StageIds.Count > 0)
            lookups.Stages = await db.DealStages.AsNoTracking().Where(s => lookups.StageIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        if (lookups.StatusIds.Count > 0)
            lookups.Statuses = await db.LeadStatuses.AsNoTracking().Where(s => lookups.StatusIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return Build(pending, lookups, currentUser);
    }

    public static IReadOnlyList<AuditEvent> Capture(AppDbContext db, ICurrentUserService? currentUser)
    {
        var (pending, lookups) = Collect(db, currentUser);
        if (pending.Count == 0) return Array.Empty<AuditEvent>();
        if (lookups.UserIds.Count > 0)
            lookups.Users = db.Users.AsNoTracking().Where(u => lookups.UserIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.Name);
        if (lookups.StageIds.Count > 0)
            lookups.Stages = db.DealStages.AsNoTracking().Where(s => lookups.StageIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name);
        if (lookups.StatusIds.Count > 0)
            lookups.Statuses = db.LeadStatuses.AsNoTracking().Where(s => lookups.StatusIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name);
        return Build(pending, lookups, currentUser);
    }

    private static (List<Pending>, Lookups) Collect(AppDbContext db, ICurrentUserService? currentUser)
    {
        db.ChangeTracker.DetectChanges();
        var pending = new List<Pending>();
        var lookups = new Lookups();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            // A club record's kind decides what it is; its fields live in its JSON.
            if (entry.Entity is ClubDocument)
            {
                CollectClubMoney(entry, pending);
                continue;
            }
            if (!Specs.TryGetValue(entry.Metadata.ClrType, out var spec)) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add(new(entry, spec, "created", _ => $"{spec.Noun} created"));
                    break;
                case EntityState.Deleted:
                    pending.Add(new(entry, spec, "deleted", _ => $"{spec.Noun} deleted"));
                    break;
                case EntityState.Modified:
                    CollectChanges(entry, spec, pending, lookups);
                    break;
            }
        }

        if (pending.Count > 0 && currentUser?.UserId is { } actorId && string.IsNullOrWhiteSpace(currentUser.UserName))
            lookups.UserIds.Add(actorId);
        return (pending, lookups);
    }

    private static void CollectChanges(EntityEntry entry, Spec spec, List<Pending> pending, Lookups lookups)
    {
        var changed = entry.Properties.Where(Differs).Select(p => p.Metadata.Name).ToHashSet(StringComparer.Ordinal);
        if (changed.Count == 0) return;

        object? Was(string prop) => entry.Property(prop).OriginalValue;
        object? Now(string prop) => entry.Property(prop).CurrentValue;

        switch (entry.Entity)
        {
            case Lead:
                if (changed.Contains(nameof(Lead.IsConverted)) && Now(nameof(Lead.IsConverted)) is true)
                {
                    var toDeal = Now(nameof(Lead.ConvertedToDealId)) != null;
                    pending.Add(new(entry, spec, "converted", _ => toDeal ? "Converted to a contact and a deal" : "Converted to a contact"));
                }
                if (changed.Contains(nameof(Lead.Status)))
                {
                    var (from, to) = (Text(Was(nameof(Lead.Status))), Text(Now(nameof(Lead.Status))));
                    pending.Add(new(entry, spec, "status_changed", _ => Moved("Status", from, to)));
                }
                else if (changed.Contains(nameof(Lead.LeadStatusId)))
                {
                    var (from, to) = (Was(nameof(Lead.LeadStatusId)) as Guid?, Now(nameof(Lead.LeadStatusId)) as Guid?);
                    Track(lookups.StatusIds, from, to);
                    pending.Add(new(entry, spec, "status_changed", l => Moved("Status", l.Status(from), l.Status(to))));
                }
                AddAssigned(entry, spec, changed, nameof(Lead.AssignedToUserId), pending, lookups);
                break;

            case Deal:
                var phrases = new List<Func<Lookups, string>>();
                if (changed.Contains(nameof(Deal.DealStageId)))
                {
                    var (from, to) = (Was(nameof(Deal.DealStageId)) as Guid?, Now(nameof(Deal.DealStageId)) as Guid?);
                    var (fromText, toText) = (Text(Was(nameof(Deal.Stage))), Text(Now(nameof(Deal.Stage))));
                    Track(lookups.StageIds, from, to);
                    phrases.Add(l => Moved("Stage", l.Stage(from) ?? fromText, l.Stage(to) ?? toText));
                }
                else if (changed.Contains(nameof(Deal.Stage)))
                {
                    var (from, to) = (Text(Was(nameof(Deal.Stage))), Text(Now(nameof(Deal.Stage))));
                    phrases.Add(_ => Moved("Stage", from, to));
                }
                if (changed.Contains(nameof(Deal.IsWon)))
                {
                    var won = Now(nameof(Deal.IsWon)) as bool?;
                    phrases.Add(_ => won switch { true => "Marked as won", false => "Marked as lost", null => "Reopened" });
                }
                if (phrases.Count > 0)
                    pending.Add(new(entry, spec, "status_changed", l => string.Join("; ", phrases.Select((p, i) => i == 0 ? p(l) : LowerFirst(p(l))))));
                AddAssigned(entry, spec, changed, nameof(Deal.AssigneeId), pending, lookups);
                break;

            case Contact:
                if (changed.Contains(nameof(Contact.IsArchived)))
                {
                    var archived = Now(nameof(Contact.IsArchived)) is true;
                    pending.Add(new(entry, spec, "updated", _ => archived ? "Archived" : "Restored"));
                }
                break;

            case TaskItem:
                if (changed.Contains(nameof(TaskItem.Completed)))
                {
                    var done = Now(nameof(TaskItem.Completed)) is true;
                    pending.Add(new(entry, spec, "status_changed", _ => done ? "Marked as done" : "Reopened"));
                }
                else if (changed.Contains(nameof(TaskItem.Status)))
                {
                    var (from, to) = (Text(Was(nameof(TaskItem.Status))), Text(Now(nameof(TaskItem.Status))));
                    pending.Add(new(entry, spec, "status_changed", _ => Moved("Status", from, to)));
                }
                AddAssigned(entry, spec, changed, nameof(TaskItem.AssigneeId), pending, lookups);
                break;
        }

        var edited = spec.Fields.Where(changed.Contains).Select(FieldName).Distinct().ToList();
        if (edited.Count > 0)
            pending.Add(new(entry, spec, "updated", _ => $"Updated {JoinList(edited)}"));
    }

    private static void AddAssigned(EntityEntry entry, Spec spec, HashSet<string> changed, string property, List<Pending> pending, Lookups lookups)
    {
        if (!changed.Contains(property)) return;
        var to = entry.Property(property).CurrentValue as Guid?;
        if (to is { } id) lookups.UserIds.Add(id);
        pending.Add(new(entry, spec, "assigned", l => to == null ? "Unassigned" : $"Assigned to {l.User(to) ?? "a teammate"}"));
    }

    private static List<AuditEvent> Build(List<Pending> pending, Lookups lookups, ICurrentUserService? currentUser)
    {
        var actorId = currentUser?.UserId;
        var actorName = actorId == null
            ? AuditVocabulary.SystemActor
            : !string.IsNullOrWhiteSpace(currentUser!.UserName) ? currentUser.UserName! : lookups.User(actorId) ?? "Someone";
        var now = DateTime.UtcNow;

        // Lines from one save share a moment; a tick apart keeps them in the order they happened.
        return pending.Select((p, i) => new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = p.Entry.Property("OrganizationId").CurrentValue as Guid?,
            OwnerUserId = Owner(p.Entry),
            EntityType = p.Spec.EntityType,
            EntityId = p.Entry.Property("Id").CurrentValue as Guid?,
            EntityLabel = Truncate(p.Spec.Label != null ? p.Spec.Label(p.Entry) : Text(p.Entry.Property(p.Spec.LabelProperty).CurrentValue), 200),
            Action = p.Action,
            Summary = Truncate(p.Summary(lookups), 500) ?? string.Empty,
            ActorUserId = actorId,
            ActorName = Truncate(actorName, 200) ?? AuditVocabulary.SystemActor,
            CreatedAtUtc = now.AddTicks(i),
        }).ToList();
    }

    // Club records belong to a workspace rather than to a user, and have no owner of their own.
    private static Guid Owner(EntityEntry entry) =>
        (entry.Metadata.FindProperty("UserId") ?? entry.Metadata.FindProperty("OwnerUserId")) is { } owner
        && entry.Property(owner.Name).CurrentValue is Guid id ? id : Guid.Empty;

    // An empty string and null are the same to a person reading the log.
    private static bool Differs(PropertyEntry p) => !Equals(Blank(p.OriginalValue), Blank(p.CurrentValue));
    private static object? Blank(object? value) => value is string s && s.Length == 0 ? null : value;

    private static void Track(HashSet<Guid> ids, Guid? a, Guid? b)
    {
        if (a is { } x) ids.Add(x);
        if (b is { } y) ids.Add(y);
    }

    private static string? Text(object? value) => value switch
    {
        null => null,
        string s => s.Length == 0 ? null : s,
        Enum e => Sentence(Words(e.ToString())),
        _ => value.ToString(),
    };

    private static string Moved(string what, string? from, string? to) => (from, to) switch
    {
        (null, null) => $"{what} cleared",
        (null, _) => $"{what} set to {to}",
        (_, null) => $"{what} cleared (was {from})",
        _ => $"{what} changed from {from} to {to}",
    };

    private static string FieldName(string property)
    {
        if (FieldNames.TryGetValue(property, out var name)) return name;
        var core = property.EndsWith("Utc", StringComparison.Ordinal) ? property[..^3] : property;
        if (core.Length > 2 && core.EndsWith("Id", StringComparison.Ordinal)) core = core[..^2];
        return Words(core);
    }

    private static string Words(string pascal) =>
        Regex.Replace(pascal, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();

    private static string Sentence(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string JoinList(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        <= 5 => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
        _ => $"{string.Join(", ", items.Take(4))} and {items.Count - 4} more fields",
    };

    private static string? Truncate(string? text, int max) =>
        text == null || text.Length <= max ? text : text[..(max - 1)] + "…";
}
