using System.Reflection;
using ACI.Infrastructure.Migrations;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi;

/// <summary>
/// Puts a database made before the baseline migration onto it, without running it.
/// </summary>
/// <remarks>
/// <para>
/// A database made the old way (the first migrations, then the start-up DDL in
/// Program.cs on every start) already has every table the baseline creates, so running
/// the baseline there would fail on its first CREATE TABLE. It is recorded in
/// __EFMigrationsHistory as applied instead, and the migrations after it then run there
/// as on any other database.
/// </para>
/// <para>
/// Only once the database has what the baseline would have made: every table and column
/// the model maps. One that lacks any is refused, not recorded, because once recorded
/// nothing would ever add them. While the start-up DDL still runs first, it adds them;
/// once that DDL is gone, a database that never started on a release with it has to do
/// so first, and the refusal says so.
/// </para>
/// <para>
/// Two things the old DDL did differently are put right first where they can be, being only
/// names or a check: the primary keys it declared inline, which SQL Server named at random
/// (PK__Attachme__3214EC07…), are given the names the baseline gives them, so a later
/// migration that drops or rebuilds one finds it; and sequence steps get the foreign key
/// to their sequence that the DDL left out, unless a step has lost its sequence. Anything
/// else that differs from the model is logged and left as it is.
/// </para>
/// </remarks>
public static class SchemaBaseline
{
    /// <summary>The baseline migration's id, as __EFMigrationsHistory records it.</summary>
    public static string MigrationId { get; } = typeof(Baseline).GetCustomAttribute<MigrationAttribute>()!.Id;

    /// <summary>
    /// Whether the database has no tables yet, other than the migrations history: one for
    /// the migrations to make, not one made the old way.
    /// </summary>
    /// <remarks>
    /// MigrateAsync makes the history table before it runs the baseline, outside the
    /// baseline's transaction, so a first start cut off part-way through the baseline leaves
    /// that table and nothing else. EF's HasTablesAsync counts it, and such a database was
    /// taken for one made the old way: the start-up DDL's first statement, a foreign key to
    /// Users, then failed on every start.
    /// </remarks>
    public static async Task<bool> IsEmptyAsync(AppDbContext db, CancellationToken ct = default) =>
        await db.Database.SqlQueryRaw<int>(OtherTablesSql).SingleAsync(ct) == 0;

    // EF's own HasTablesAsync query (user tables, not the ones SSMS makes for diagrams),
    // less the history table.
    private const string OtherTablesSql = @"
        SELECT COUNT(*) AS [Value] FROM sys.objects o
        WHERE o.[type] = 'U' AND o.[is_ms_shipped] = 0
            AND o.[name] <> N'" + HistoryRepository.DefaultTableName + @"'
            AND NOT EXISTS (SELECT 1 FROM sys.extended_properties ep
                WHERE ep.[major_id] = o.[object_id] AND ep.[minor_id] = 0 AND ep.[class] = 1
                    AND ep.[name] = N'microsoft_database_tools_support')";

    /// <summary>
    /// Records the baseline on a database made the old way, once it has everything the
    /// baseline would have made. Does nothing on a database the migrations made, one already
    /// recorded, or one with no tables yet.
    /// </summary>
    /// <returns>Whether it recorded the baseline.</returns>
    /// <exception cref="InvalidOperationException">The database lacks a table or column the model maps.</exception>
    public static async Task<bool> RecordOnExistingDatabaseAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var history = db.GetService<IHistoryRepository>();
        if (await history.ExistsAsync(ct)
            && (await history.GetAppliedMigrationsAsync(ct)).Any(m => m.MigrationId == MigrationId))
            return false;

        var before = await CompareAsync(db, ct);
        if (before.TableCount == 0) return false;
        if (before.Missing.Count > 0)
        {
            throw new InvalidOperationException(
                "This database was made before the baseline migration but lacks "
                + string.Join(", ", before.Missing.Take(20)) + (before.Missing.Count > 20 ? $" and {before.Missing.Count - 20} more" : "")
                + ". It is not recorded at the baseline, as nothing would add them afterwards. They come from the old "
                + "start-up DDL: start the database once on a release that still runs it, then on this one.");
        }

        // Names and a key, not anything the app needs to run: if one cannot be put right, the
        // baseline is recorded all the same and what is left is logged with the rest below.
        async Task PutRightAsync(string sql, string what)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                logger.LogWarning(ex, "Could not {What} before recording the baseline", what);
            }
        }
        await PutRightAsync(NamePrimaryKeysSql(db.Model, before.PrimaryKeys), "give the primary keys the baseline's names");
        await PutRightAsync(OutreachStepsForeignKeySql, "add the sequence steps' foreign key");

        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await db.Database.ExecuteSqlRawAsync(string.Join("\n",
            history.GetBeginIfNotExistsScript(MigrationId),
            history.GetInsertScript(new HistoryRow(MigrationId, ProductInfo.GetVersion())),
            history.GetEndIfScript()), ct);
        logger.LogInformation("Recorded the baseline migration {MigrationId} on a database made before it, without running it", MigrationId);

        var after = await CompareAsync(db, ct);
        foreach (var difference in after.Different)
            logger.LogWarning("The database differs from the baseline: {Difference}. A migration that changes it has to allow for that", difference);
        return true;
    }

    /// <summary>
    /// How the database differs from the model, read from its catalog: what it lacks that
    /// the app cannot run without (tables and columns), and what it has otherwise (keys,
    /// indexes, column types and nullability). Tables and columns it has beyond the model
    /// are not counted: a column the model no longer maps is left alone, not dropped.
    /// </summary>
    public static async Task<SchemaComparison> CompareAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQueryRaw<CatalogRow>(CatalogSql).ToListAsync(ct);
        var ignoreCase = StringComparer.OrdinalIgnoreCase;
        var columns = rows.Where(r => r.Kind == "Column")
            .ToDictionary(r => r.TableName + "." + r.Name, ignoreCase);
        var tables = rows.Where(r => r.Kind == "Column").Select(r => r.TableName).ToHashSet(ignoreCase);
        var primaryKeys = rows.Where(r => r.Kind == "PrimaryKey")
            .GroupBy(r => r.TableName, ignoreCase).ToDictionary(g => g.Key, g => g.First().Name!, ignoreCase);
        var constraints = rows.Where(r => r.Kind is "ForeignKey" or "Index")
            .Select(r => r.Kind + " " + r.TableName + "." + r.Name).ToHashSet(ignoreCase);

        var missing = new List<string>();
        var different = new List<string>();
        foreach (var table in db.Model.GetRelationalModel().Tables)
        {
            if (!tables.Contains(table.Name))
            {
                missing.Add($"table {table.Name}");
                continue;
            }
            foreach (var column in table.Columns)
            {
                if (!columns.TryGetValue(table.Name + "." + column.Name, out var actual))
                {
                    missing.Add($"column {table.Name}.{column.Name}");
                    continue;
                }
                if (actual.IsNullable != column.IsNullable)
                    different.Add($"{table.Name}.{column.Name} is {(actual.IsNullable == true ? "" : "NOT ")}NULL where the model has it {(column.IsNullable ? "" : "NOT ")}NULL");
                if (!SameStoreType(actual.StoreType, column.StoreType))
                    different.Add($"{table.Name}.{column.Name} is {actual.StoreType} where the model has {column.StoreType}");
            }
            if (table.PrimaryKey is { } key)
            {
                if (!primaryKeys.TryGetValue(table.Name, out var actualKey))
                    different.Add($"{table.Name} has no primary key where the model has {key.Name}");
                else if (!string.Equals(actualKey, key.Name, StringComparison.OrdinalIgnoreCase))
                    different.Add($"{table.Name}'s primary key is named {actualKey} where the model names it {key.Name}");
            }
            foreach (var foreignKey in table.ForeignKeyConstraints)
                if (!constraints.Contains($"ForeignKey {table.Name}.{foreignKey.Name}"))
                    different.Add($"{table.Name} has no foreign key {foreignKey.Name}");
            foreach (var index in table.Indexes)
                if (!constraints.Contains($"Index {table.Name}.{index.Name}"))
                    different.Add($"{table.Name} has no index {index.Name}");
        }
        return new SchemaComparison(tables.Count, missing, different, primaryKeys);
    }

    private static bool SameStoreType(string? actual, string expected)
    {
        static string Normal(string type) => type.Replace(" ", "").ToLowerInvariant() switch
        {
            "timestamp" => "rowversion",
            var t => t,
        };
        return actual != null && Normal(actual) == Normal(expected);
    }

    // The primary keys the old DDL declared inline (PRIMARY KEY on the column) were named
    // by SQL Server, differently in every database. A rename changes only the name.
    private static string NamePrimaryKeysSql(IModel model, IReadOnlyDictionary<string, string> actual)
    {
        var sql = new System.Text.StringBuilder();
        foreach (var table in model.GetRelationalModel().Tables)
        {
            if (table.PrimaryKey is not { } key) continue;
            if (!actual.TryGetValue(table.Name, out var current)) continue;
            if (string.Equals(current, key.Name, StringComparison.OrdinalIgnoreCase)) continue;
            sql.Append("IF OBJECT_ID(N'").Append(Literal(Quoted(key.Name))).Append("') IS NULL ")
                .Append("EXEC sp_rename N'").Append(Literal(Quoted(current))).Append("', N'").Append(Literal(key.Name)).Append("', N'OBJECT';\n");
        }
        return sql.Length == 0 ? "SELECT 1;" : sql.ToString();
    }

    private static string Quoted(string name) => "[dbo].[" + name.Replace("]", "]]") + "]";

    private static string Literal(string text) => text.Replace("'", "''");

    /// <summary>
    /// The one foreign key the model has that the old DDL never made. Without it, deleting a
    /// sequence left its steps behind, so where any step's sequence is gone the key is left
    /// out (and logged as a difference) rather than those steps being deleted.
    /// </summary>
    internal const string OutreachStepsForeignKeySql = @"
        IF OBJECT_ID(N'[dbo].[FK_OutreachSteps_OutreachSequences_SequenceId]', N'F') IS NULL
            AND OBJECT_ID(N'[dbo].[OutreachSteps]', N'U') IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM [OutreachSteps] s
                WHERE NOT EXISTS (SELECT 1 FROM [OutreachSequences] q WHERE q.[Id] = s.[SequenceId]))
            ALTER TABLE [OutreachSteps] ADD CONSTRAINT [FK_OutreachSteps_OutreachSequences_SequenceId]
                FOREIGN KEY ([SequenceId]) REFERENCES [OutreachSequences] ([Id]) ON DELETE CASCADE;";

    // Every column, primary key, foreign key and index in dbo, with each column's type
    // written the way EF writes a store type (nvarchar(256), decimal(18,2), rowversion).
    private const string CatalogSql = @"
        SELECT N'Column' AS [Kind], t.[name] COLLATE DATABASE_DEFAULT AS [TableName], c.[name] COLLATE DATABASE_DEFAULT AS [Name],
            c.[is_nullable] AS [IsNullable],
            CASE
                WHEN ty.[name] IN (N'nvarchar', N'nchar')
                    THEN ty.[name] COLLATE DATABASE_DEFAULT + N'(' + CASE WHEN c.[max_length] = -1 THEN N'max' ELSE CAST(c.[max_length] / 2 AS nvarchar(10)) END + N')'
                WHEN ty.[name] IN (N'varchar', N'char', N'varbinary', N'binary')
                    THEN ty.[name] COLLATE DATABASE_DEFAULT + N'(' + CASE WHEN c.[max_length] = -1 THEN N'max' ELSE CAST(c.[max_length] AS nvarchar(10)) END + N')'
                WHEN ty.[name] IN (N'decimal', N'numeric')
                    THEN ty.[name] COLLATE DATABASE_DEFAULT + N'(' + CAST(c.[precision] AS nvarchar(10)) + N',' + CAST(c.[scale] AS nvarchar(10)) + N')'
                ELSE ty.[name] COLLATE DATABASE_DEFAULT
            END AS [StoreType]
        FROM sys.tables t
        JOIN sys.columns c ON c.[object_id] = t.[object_id]
        JOIN sys.types ty ON ty.[user_type_id] = c.[user_type_id]
        WHERE t.[schema_id] = SCHEMA_ID(N'dbo') AND t.[is_ms_shipped] = 0
        UNION ALL
        SELECT N'PrimaryKey', t.[name] COLLATE DATABASE_DEFAULT, k.[name] COLLATE DATABASE_DEFAULT, NULL, NULL
        FROM sys.key_constraints k JOIN sys.tables t ON t.[object_id] = k.[parent_object_id]
        WHERE k.[type] = 'PK' AND t.[schema_id] = SCHEMA_ID(N'dbo')
        UNION ALL
        SELECT N'ForeignKey', t.[name] COLLATE DATABASE_DEFAULT, f.[name] COLLATE DATABASE_DEFAULT, NULL, NULL
        FROM sys.foreign_keys f JOIN sys.tables t ON t.[object_id] = f.[parent_object_id]
        WHERE t.[schema_id] = SCHEMA_ID(N'dbo')
        UNION ALL
        SELECT N'Index', t.[name] COLLATE DATABASE_DEFAULT, i.[name] COLLATE DATABASE_DEFAULT, NULL, NULL
        FROM sys.indexes i JOIN sys.tables t ON t.[object_id] = i.[object_id]
        WHERE i.[name] IS NOT NULL AND i.[is_primary_key] = 0 AND t.[schema_id] = SCHEMA_ID(N'dbo')";

    private sealed class CatalogRow
    {
        public string Kind { get; set; } = "";
        public string TableName { get; set; } = "";
        public string? Name { get; set; }
        public bool? IsNullable { get; set; }
        public string? StoreType { get; set; }
    }
}

/// <summary>What <see cref="SchemaBaseline.CompareAsync"/> found.</summary>
/// <param name="TableCount">How many tables the database has, any of them.</param>
/// <param name="Missing">Tables and columns the model maps that the database lacks.</param>
/// <param name="Different">Keys, indexes, types and nullability that differ from the model.</param>
/// <param name="PrimaryKeys">Each table's primary key name, as the database has it.</param>
public sealed record SchemaComparison(
    int TableCount,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Different,
    IReadOnlyDictionary<string, string> PrimaryKeys);
