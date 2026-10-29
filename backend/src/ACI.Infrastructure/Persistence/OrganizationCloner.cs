using System.Data;
using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace ACI.Infrastructure.Persistence;

/// <summary>What a copy made, table by table, and what it left out on purpose.</summary>
public sealed record OrganizationCloneReport(
    Guid SourceId,
    Guid CopyId,
    string CopyName,
    IReadOnlyDictionary<string, int> RowsCopied,
    IReadOnlyDictionary<string, int> RowsLeftOut,
    IReadOnlyDictionary<string, int> Adjustments);

/// <summary>What a copy would hold, table by table, counted without copying anything.</summary>
public sealed record OrganizationClonePlan(
    Guid SourceId,
    IReadOnlyDictionary<string, int> RowsToCopy,
    IReadOnlyDictionary<string, int> RowsLeftOut,
    long AttachmentBytes);

/// <summary>
/// Copies an organization into a new one: every record it owns, everything that hangs
/// off those records, and its members with their roles, under new ids.
/// </summary>
/// <remarks>
/// <para>
/// It works from the data model rather than a list of columns: every table whose rows
/// carry an organization is copied, so is every table in <see cref="Children"/>, and a
/// test fails if a table is added that is in neither and not in <see cref="LeftOut"/>.
/// Every id that points at a copied row is swapped for its copy's, in id columns and
/// inside text alike (club records and rules keep ids in JSON), so nothing in the copy
/// refers back into the original. People are shared, not copied: an assignee in the
/// original is the same person in the copy.
/// </para>
/// <para>
/// Nothing that already exists emails anyone a second time from the copy: enrollments
/// that were running start paused, a contract out for signature keeps its link and its
/// reminder with the original, and task reminders not yet sent are marked sent. The
/// webhook key, password and outbound address stay with the original too, so leads sent
/// in from outside and events posted out keep going to one place.
/// </para>
/// </remarks>
public sealed class OrganizationCloner
{
    /// <summary>Tables that belong to an organization through a parent row, not a column of their own.</summary>
    public static readonly IReadOnlyList<(Type Child, string ParentKey, Type Parent)> Children =
    [
        (typeof(DealStage), nameof(DealStage.PipelineId), typeof(Pipeline)),
        (typeof(DealStageChange), nameof(DealStageChange.DealId), typeof(Deal)),
        (typeof(DealLineItem), nameof(DealLineItem.DealId), typeof(Deal)),
        (typeof(TaskComment), nameof(TaskComment.TaskItemId), typeof(TaskItem)),
        (typeof(ContractEvent), nameof(ContractEvent.ContractId), typeof(Contract)),
        (typeof(OutreachStep), nameof(OutreachStep.SequenceId), typeof(OutreachSequence)),
        (typeof(AutomationFire), nameof(AutomationFire.RuleId), typeof(AutomationRule)),
        (typeof(SalesTeamMember), nameof(SalesTeamMember.TeamId), typeof(SalesTeam)),
    ];

    /// <summary>Tables that are not copied, and why.</summary>
    public static readonly IReadOnlyDictionary<Type, string> LeftOut = new Dictionary<Type, string>
    {
        [typeof(User)] = "People are shared between the two organizations, not copied.",
        [typeof(UserSettings)] = "A person's own preferences, the same in every organization.",
        [typeof(Organization)] = "The copy is a new organization, made rather than copied row for row.",
        [typeof(JoinRequest)] = "Requests to join the original. Everyone in it is a member of the copy already.",
    };

    private static readonly Regex GuidText = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public OrganizationCloner(AppDbContext db) => _db = db;

    /// <summary>The tables whose rows carry an organization of their own.</summary>
    public static IReadOnlyList<IEntityType> OrganizationTables(IModel model) =>
        model.GetEntityTypes()
            .Where(t => t.FindProperty("OrganizationId") != null && !LeftOut.ContainsKey(t.ClrType))
            .OrderBy(t => t.GetTableName())
            .ToList();

    /// <summary>
    /// A copy is only started with at least this many times its estimated size free in
    /// the database. The copy's rows, their indexes and the transaction that writes them
    /// all take room, and the database is shared: running out mid-copy fails every
    /// tenant's writes until the rollback is done.
    /// </summary>
    public const int Headroom = 2;

    /// <summary>
    /// Counts what <see cref="CloneAsync"/> would copy from <paramref name="sourceId"/>, and
    /// the bytes of its attachments, without writing anything.
    /// </summary>
    /// <remarks>
    /// The dry run used to make the whole copy, attachment bytes included, inside a
    /// transaction in the production database every tenant shares, and then roll it back.
    /// This reads ids and counts only.
    /// </remarks>
    public async Task<OrganizationClonePlan> PlanAsync(Guid sourceId, CancellationToken ct = default)
    {
        var model = _db.Model;
        var keys = new Dictionary<Type, List<Guid>>();
        var toCopy = new Dictionary<string, int>();
        var leftOut = new Dictionary<string, int>
        {
            ["JoinRequests"] = await _db.JoinRequests.CountAsync(r => r.OrganizationId == sourceId, ct),
        };

        async Task Count(IEntityType type, string column, IReadOnlyCollection<Guid> ids)
        {
            var (count, found) = await CountOf(type, column, ids, ct);
            if (type.ClrType == typeof(Invite))
            {
                // As in the copy: an accepted invitation is history, and stays behind.
                var accepted = await _db.Invites.CountAsync(i => i.OrganizationId == sourceId && i.AcceptedByUserId != null, ct);
                leftOut[type.GetTableName()!] = accepted;
                count -= accepted;
            }
            toCopy[type.GetTableName()!] = count;
            if (found != null) keys[type.ClrType] = found;
        }

        foreach (var type in OrganizationTables(model))
            await Count(type, "OrganizationId", [sourceId]);
        foreach (var (child, parentKey, parent) in Children)
            await Count(model.FindEntityType(child)!, parentKey, keys.GetValueOrDefault(parent) ?? []);

        var attachmentBytes = await _db.Attachments.Where(a => a.OrganizationId == sourceId).SumAsync(a => (long)a.Content.Length, ct);
        return new OrganizationClonePlan(sourceId, toCopy, leftOut, attachmentBytes);
    }

    /// <summary>
    /// Roughly how many bytes the copy in <paramref name="plan"/> adds: each table's share
    /// of the space it takes now, by rows, and never less than the attachments' own bytes,
    /// of which one organization can hold far more than its share. SQL Server only.
    /// </summary>
    public async Task<long> EstimateBytesAsync(OrganizationClonePlan plan, CancellationToken ct = default)
    {
        var tables = new Dictionary<string, (long Bytes, long Rows)>(StringComparer.OrdinalIgnoreCase);
        await ReadAsync(@"
            SELECT t.[name],
                   SUM(CAST(ps.[used_page_count] AS bigint)) * 8192,
                   SUM(CASE WHEN ps.[index_id] IN (0, 1) THEN ps.[row_count] ELSE 0 END)
            FROM sys.dm_db_partition_stats ps
            INNER JOIN sys.tables t ON t.[object_id] = ps.[object_id]
            GROUP BY t.[name]", r => tables[r.GetString(0)] = (r.GetInt64(1), r.GetInt64(2)), ct);

        long estimate = 0;
        foreach (var (table, rows) in plan.RowsToCopy)
        {
            if (rows == 0 || !tables.TryGetValue(table, out var space) || space.Rows == 0) continue;
            var share = (long)Math.Ceiling((double)space.Bytes * rows / space.Rows);
            if (string.Equals(table, "Attachments", StringComparison.OrdinalIgnoreCase)) share = Math.Max(share, plan.AttachmentBytes);
            estimate += share;
        }
        return estimate;
    }

    /// <summary>
    /// How many bytes the database can still take. On Azure SQL Database that is its size
    /// cap less what it uses; on a SQL Server, what its data files can still grow to on
    /// their disks. SQL Server only.
    /// </summary>
    public async Task<long> FreeBytesAsync(CancellationToken ct = default)
    {
        long? cap = null, used = null;
        await ReadAsync(@"
            SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'MaxSizeInBytes') AS bigint),
                   (SELECT SUM(CAST(FILEPROPERTY([name], 'SpaceUsed') AS bigint)) * 8192 FROM sys.database_files WHERE [type_desc] = 'ROWS')",
            r => { cap = r.IsDBNull(0) ? null : r.GetInt64(0); used = r.IsDBNull(1) ? null : r.GetInt64(1); }, ct);
        if (cap is { } max && used is { } inUse) return max - inUse;

        // Not Azure SQL Database: no cap on the database, only on its files and disks.
        long free = 0;
        await ReadAsync(@"
            SELECT CASE WHEN f.[max_size] = -1
                        THEN (CAST(f.[size] AS bigint) - CAST(FILEPROPERTY(f.[name], 'SpaceUsed') AS bigint)) * 8192 + v.[available_bytes]
                        ELSE (CAST(f.[max_size] AS bigint) - CAST(FILEPROPERTY(f.[name], 'SpaceUsed') AS bigint)) * 8192 END
            FROM sys.database_files f
            CROSS APPLY sys.dm_os_volume_stats(DB_ID(), f.[file_id]) v
            WHERE f.[type_desc] = 'ROWS'", r => free += r.GetInt64(0), ct);
        return free;
    }

    /// <summary>
    /// Copies <paramref name="sourceId"/> into a new organization called <paramref name="name"/>.
    /// It saves in the caller's transaction; nothing stays until the caller commits.
    /// </summary>
    public async Task<OrganizationCloneReport> CloneAsync(Guid sourceId, string name, CancellationToken ct = default)
    {
        var source = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == sourceId, ct)
            ?? throw new InvalidOperationException($"Organization {sourceId} does not exist.");
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("The copy needs a name.");

        var model = _db.Model;
        var copyId = Guid.NewGuid();
        var map = new Dictionary<Guid, Guid> { [sourceId] = copyId };
        var copiedIds = new Dictionary<Type, List<Guid>>();
        var rows = new List<(IEntityType Type, List<object> Rows)>();
        var leftOut = new Dictionary<string, int>
        {
            ["JoinRequests"] = await _db.JoinRequests.CountAsync(r => r.OrganizationId == sourceId, ct),
        };

        // 1. Every row to copy. Each gets its new id now, so any row can point at any other.
        foreach (var type in OrganizationTables(model))
            Take(type, await RowsOf(type, "OrganizationId", [sourceId], ct));
        foreach (var (child, parentKey, parent) in Children)
            Take(model.FindEntityType(child)!, await RowsOf(model.FindEntityType(child)!, parentKey, copiedIds.GetValueOrDefault(parent) ?? [], ct));

        void Take(IEntityType type, List<object> found)
        {
            if (type.ClrType == typeof(Invite))
            {
                // An accepted invitation is history: the person it brought in is a member already.
                leftOut[type.GetTableName()!] = found.RemoveAll(r => ((Invite)r).AcceptedByUserId != null);
            }
            rows.Add((type, found));
            if (SingleGuidKey(type) is not { } key) return;
            var ids = copiedIds[type.ClrType] = [];
            foreach (var row in found)
            {
                var old = (Guid)key.PropertyInfo!.GetValue(row)!;
                ids.Add(old);
                map[old] = Guid.NewGuid();
            }
        }

        // 2. The new organization: the original's settings, but not its ways in and out.
        var org = (Organization)CopyOf(model.FindEntityType(typeof(Organization))!, source, map);
        org.Id = copyId;
        org.Name = name;
        org.CreatedAtUtc = DateTime.UtcNow;
        org.WebhookApiKeyHash = null;
        org.WebhookApiKeyCreatedAtUtc = null;
        org.McpApiKeyHash = null;
        org.McpApiKeyCreatedAtUtc = null;
        org.WebhookPassword = null;
        org.OutboundWebhookUrl = null;
        org.OutboundWebhookSecret = null;

        // 3. The copies, saved twice. A link between copied records that the model knows as
        //    a foreign key is filled in on the second save: a converted lead points at its
        //    deal, the deal at its contact and the contact back at the lead, and no insert
        //    order satisfies a loop like that.
        var newIds = map.Values.ToHashSet();
        var copied = new Dictionary<string, int>();
        var adjustments = new Dictionary<string, int>();
        var deferred = new List<(object Copy, IProperty Property, object Value)>();
        var previous = _db.RecordsAuditTrail;
        _db.RecordsAuditTrail = false;
        try
        {
            _db.Organizations.Add(org);
            foreach (var (type, found) in rows)
            {
                var links = type.GetForeignKeys()
                    .Where(fk => fk.Properties.Count == 1 && fk.Properties[0].IsNullable && fk.PrincipalEntityType.ClrType != typeof(Organization))
                    .Select(fk => fk.Properties[0])
                    .ToList();
                foreach (var row in found)
                {
                    var copy = CopyOf(type, row, map);
                    foreach (var link in links)
                    {
                        if (link.PropertyInfo!.GetValue(copy) is Guid g && newIds.Contains(g))
                        {
                            deferred.Add((copy, link, g));
                            link.PropertyInfo.SetValue(copy, null);
                        }
                    }
                    if (Adjust(row, copy) is { } adjustment)
                        adjustments[adjustment] = adjustments.GetValueOrDefault(adjustment) + 1;
                    _db.Add(copy);
                }
                copied[type.GetTableName()!] = found.Count;
            }
            await _db.SaveChangesAsync(ct);

            foreach (var (copy, link, value) in deferred)
                link.PropertyInfo!.SetValue(copy, value);
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _db.RecordsAuditTrail = previous;
            _db.ChangeTracker.Clear();
        }

        return new OrganizationCloneReport(sourceId, copyId, name, copied, leftOut, adjustments);
    }

    /// <summary>
    /// Reads the copy back and checks it against the original: each table holds as many
    /// rows as the original, less what was left out on purpose, and nothing in the copy
    /// points at a row of the original. Returns what is wrong; empty when it is right.
    /// </summary>
    public async Task<IReadOnlyList<string>> VerifyAsync(OrganizationCloneReport report, CancellationToken ct = default)
    {
        var model = _db.Model;
        var problems = new List<string>();
        var originalIds = new HashSet<Guid> { report.SourceId };
        var copies = new List<(IEntityType Type, List<object> Rows)>();
        var sourceKeys = new Dictionary<Type, List<Guid>>();
        var copyKeys = new Dictionary<Type, List<Guid>>();

        void Compare(IEntityType type, List<object> original, List<object> copy)
        {
            var table = type.GetTableName()!;
            var expected = original.Count - report.RowsLeftOut.GetValueOrDefault(table);
            if (copy.Count != expected)
                problems.Add($"{table}: the original has {original.Count} rows ({report.RowsLeftOut.GetValueOrDefault(table)} left out on purpose), the copy {copy.Count}.");
            if (report.RowsCopied.GetValueOrDefault(table) != copy.Count)
                problems.Add($"{table}: {report.RowsCopied.GetValueOrDefault(table)} rows were copied, but the copy holds {copy.Count}.");
            copies.Add((type, copy));
            if (SingleGuidKey(type) is not { } key) return;
            sourceKeys[type.ClrType] = original.Select(r => (Guid)key.PropertyInfo!.GetValue(r)!).ToList();
            copyKeys[type.ClrType] = copy.Select(r => (Guid)key.PropertyInfo!.GetValue(r)!).ToList();
            originalIds.UnionWith(sourceKeys[type.ClrType]);
        }

        foreach (var type in OrganizationTables(model))
            Compare(type, await RowsOf(type, "OrganizationId", [report.SourceId], ct), await RowsOf(type, "OrganizationId", [report.CopyId], ct));
        foreach (var (child, parentKey, parent) in Children)
        {
            var type = model.FindEntityType(child)!;
            Compare(type,
                await RowsOf(type, parentKey, sourceKeys.GetValueOrDefault(parent) ?? [], ct),
                await RowsOf(type, parentKey, copyKeys.GetValueOrDefault(parent) ?? [], ct));
        }

        foreach (var (type, rows) in copies)
        {
            foreach (var row in rows)
            {
                foreach (var property in type.GetProperties().Where(p => p.PropertyInfo != null))
                {
                    var value = property.PropertyInfo!.GetValue(row);
                    var points = value switch
                    {
                        Guid g => originalIds.Contains(g),
                        string text when text.Length >= 36 => GuidText.Matches(text).Any(m => Guid.TryParse(m.Value, out var g) && originalIds.Contains(g)),
                        _ => false,
                    };
                    if (points)
                        problems.Add($"{type.GetTableName()}.{property.Name} in the copy points at a row of the original.");
                }
            }
        }
        return problems.Distinct().ToList();
    }

    private async Task<List<object>> RowsOf(IEntityType type, string column, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = new List<object>();
        var load = typeof(OrganizationCloner)
            .GetMethod(nameof(Load), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(type.ClrType);
        // A thousand ids at a time keeps each query's parameter list well inside SQL Server's limit.
        foreach (var chunk in ids.Chunk(1000))
            rows.AddRange(await (Task<List<object>>)load.Invoke(this, [column, chunk, ct])!);
        return rows;
    }

    private async Task<List<object>> Load<T>(string column, Guid[] ids, CancellationToken ct) where T : class =>
        (await Matching<T>(column, ids).ToListAsync(ct)).Cast<object>().ToList();

    /// <summary>How many rows of <paramref name="type"/> have <paramref name="column"/> in <paramref name="ids"/>, and their ids if they have one.</summary>
    private async Task<(int Count, List<Guid>? Keys)> CountOf(IEntityType type, string column, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var count = typeof(OrganizationCloner)
            .GetMethod(nameof(CountChunk), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(type.ClrType);
        var keys = SingleGuidKey(type) != null ? new List<Guid>() : null;
        var total = 0;
        foreach (var chunk in ids.Chunk(1000))
        {
            var (n, found) = await (Task<(int, List<Guid>?)>)count.Invoke(this, [column, chunk, keys != null, ct])!;
            total += n;
            keys?.AddRange(found!);
        }
        return (total, keys);
    }

    private async Task<(int, List<Guid>?)> CountChunk<T>(string column, Guid[] ids, bool withKeys, CancellationToken ct) where T : class
    {
        if (!withKeys) return (await Matching<T>(column, ids).CountAsync(ct), null);
        var keys = await Matching<T>(column, ids).Select(r => EF.Property<Guid>(r, "Id")).ToListAsync(ct);
        return (keys.Count, keys);
    }

    private IQueryable<T> Matching<T>(string column, Guid[] ids) where T : class
    {
        if (_db.Model.FindEntityType(typeof(T))!.FindProperty(column)!.IsNullable)
        {
            var any = ids.Select(i => (Guid?)i).ToList();
            return _db.Set<T>().AsNoTracking().Where(r => any.Contains(EF.Property<Guid?>(r, column)));
        }
        var all = ids.ToList();
        return _db.Set<T>().AsNoTracking().Where(r => all.Contains(EF.Property<Guid>(r, column)));
    }

    /// <summary>Runs <paramref name="sql"/> on the context's connection, in its transaction if it has one.</summary>
    private async Task ReadAsync(string sql, Action<System.Data.Common.DbDataReader> row, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) row(reader);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    private static IProperty? SingleGuidKey(IEntityType type)
    {
        var key = type.FindPrimaryKey()!.Properties;
        return key.Count == 1 && key[0].Name == "Id" && key[0].ClrType == typeof(Guid) ? key[0] : null;
    }

    /// <summary>A new row with the same values, every copied id swapped for its copy's.</summary>
    private static object CopyOf(IEntityType type, object source, IReadOnlyDictionary<Guid, Guid> map)
    {
        var copy = Activator.CreateInstance(type.ClrType)!;
        foreach (var property in type.GetProperties())
        {
            // A row version is the database's to set.
            if (property.ValueGenerated == ValueGenerated.OnAddOrUpdate || property.PropertyInfo == null) continue;
            property.PropertyInfo.SetValue(copy, Remap(property.PropertyInfo.GetValue(source), map));
        }
        return copy;
    }

    private static object? Remap(object? value, IReadOnlyDictionary<Guid, Guid> map) => value switch
    {
        Guid g => map.TryGetValue(g, out var to) ? to : g,
        string text when text.Length >= 36 => GuidText.Replace(text, m =>
            Guid.TryParse(m.Value, out var g) && map.TryGetValue(g, out var to)
                ? (m.Value.Any(char.IsUpper) ? to.ToString().ToUpperInvariant() : to.ToString())
                : m.Value),
        _ => value,
    };

    /// <summary>What a copy must not share with the original, or must not do again. Returns what it changed.</summary>
    private static string? Adjust(object source, object copy)
    {
        switch (copy)
        {
            case OutreachEnrollment e when e.Status == "active":
                e.Status = "paused";
                return "Running sequence enrollments start paused";
            case Contract c when c.SigningTokenHash != null || (c.SentAtUtc != null && c.ReminderSentAtUtc == null):
                // The link the signer has opens the original. The copy keeps the record, not the link.
                c.SigningTokenHash = null;
                c.SigningTokenExpiresAtUtc = null;
                if (c.SentAtUtc != null && c.ReminderSentAtUtc == null) c.ReminderSentAtUtc = DateTime.UtcNow;
                return "Contracts out for signature keep their link and reminder with the original";
            case TaskItem t when t.ReminderDateUtc != null && t.ReminderSentAtUtc == null:
                t.ReminderSentAtUtc = DateTime.UtcNow;
                return "Task reminders not yet sent are marked sent in the copy";
            case LeadCaptureForm f:
                f.PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
                return "Lead capture forms get their own public address";
            case Invite i:
                // A hash of a token nobody has: the copy's invitation is accepted after a resend.
                i.TokenHash = ACI.Application.Common.PasswordResetCrypto.HashRawToken(
                    ACI.Application.Common.PasswordResetCrypto.CreateRawToken());
                return "Pending invitations get their own link";
            case Notification n when n.SourceKey != null && n.SourceKey == ((Notification)source).SourceKey:
                // The key stops a person being told the same thing twice, and is unique per
                // person. One with no copied record's id in it would be the original's exactly.
                n.SourceKey = null;
                return "Notification keys with no record in them are cleared";
            default:
                return null;
        }
    }
}
