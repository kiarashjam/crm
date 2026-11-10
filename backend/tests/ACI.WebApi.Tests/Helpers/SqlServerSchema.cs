using System.Reflection;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// Reading and making schemas on the SQL Server in CADENCE_TEST_SQLSERVER (see <see cref="SqlServerApp"/>).
/// </summary>
public static class SqlServerSchema
{
    /// <summary>The ids the four migrations before the baseline recorded, in order.</summary>
    public static readonly string[] LegacyMigrationIds =
    {
        "20260206130556_InitialCreate",
        "20260209103032_AddDealEnhancements",
        "20260506124606_AddOrganizationWebhookPassword",
        "20261005124929_SpeedUpLeadAndActivityLookups",
    };

    /// <summary>A context on <paramref name="database"/>, outside any app.</summary>
    public static AppDbContext Context(string database) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);

    /// <summary>Every migration the app has, in the order they apply.</summary>
    public static IReadOnlyList<string> AllMigrationIds()
    {
        using var db = Context("unused");
        return db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
    }

    /// <summary>The schema the migrations alone make, without the app or its start-up DDL.</summary>
    public static async Task MigrateAloneAsync(string database)
    {
        await using var db = Context(database);
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// A database as the migrations before the baseline left it (Fixtures/LegacyMigrations.sql),
    /// before the old start-up DDL added the rest.
    /// </summary>
    public static async Task CreateLegacyAsync(string database)
    {
        await using var stream = typeof(SqlServerSchema).Assembly.GetManifestResourceStream("LegacyMigrations.sql")
            ?? throw new InvalidOperationException("The LegacyMigrations.sql fixture is not embedded in the test assembly.");
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync();

        // One connection for the whole script: it opens a transaction in one batch and
        // commits it in a later one, as `dotnet ef migrations script` writes them.
        await using var connection = new SqlConnection(SqlServerApp.ConnectionString(database));
        await connection.OpenAsync();
        foreach (var batch in Batches(script))
        {
            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    // The batches between the GO lines that sqlcmd would send one by one.
    private static IEnumerable<string> Batches(string script)
    {
        var batch = new System.Text.StringBuilder();
        foreach (var line in script.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
                batch.Clear();
                continue;
            }
            batch.Append(line).Append('\n');
        }
        if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
    }

    /// <summary>The migration ids in __EFMigrationsHistory, in order.</summary>
    public static async Task<List<string>> HistoryAsync(string database) =>
        await LinesAsync(database, "SELECT [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId]");

    /// <summary>
    /// One line per column (type, nullability), default, index or key (with its columns and
    /// filter), foreign key (with its columns, target and delete action) and check, sorted:
    /// two databases with the same lines have the same schema.
    /// </summary>
    public static async Task<List<string>> ListAsync(string database)
    {
        var lines = await LinesAsync(database, ListingSql);
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static async Task<List<string>> LinesAsync(string database, string sql)
    {
        await using var connection = new SqlConnection(SqlServerApp.ConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
        return lines;
    }

    private const string ListingSql = @"
        SELECT N'COL ' + t.[name] COLLATE DATABASE_DEFAULT + N'.' + c.[name] COLLATE DATABASE_DEFAULT + N' ' + ty.[name] COLLATE DATABASE_DEFAULT
            + CASE
                WHEN ty.[name] IN (N'nvarchar', N'nchar') THEN N'(' + CASE WHEN c.[max_length] = -1 THEN N'max' ELSE CAST(c.[max_length] / 2 AS nvarchar(10)) END + N')'
                WHEN ty.[name] IN (N'varchar', N'char', N'varbinary', N'binary') THEN N'(' + CASE WHEN c.[max_length] = -1 THEN N'max' ELSE CAST(c.[max_length] AS nvarchar(10)) END + N')'
                WHEN ty.[name] IN (N'decimal', N'numeric') THEN N'(' + CAST(c.[precision] AS nvarchar(10)) + N',' + CAST(c.[scale] AS nvarchar(10)) + N')'
                ELSE N'' END
            + CASE WHEN c.[is_nullable] = 1 THEN N' NULL' ELSE N' NOT NULL' END
            + CASE WHEN c.[is_identity] = 1 THEN N' IDENTITY' ELSE N'' END AS [Line]
        FROM sys.tables t
        JOIN sys.columns c ON c.[object_id] = t.[object_id]
        JOIN sys.types ty ON ty.[user_type_id] = c.[user_type_id]
        WHERE t.[is_ms_shipped] = 0
        UNION ALL
        SELECT N'DEF ' + t.[name] COLLATE DATABASE_DEFAULT + N'.' + c.[name] COLLATE DATABASE_DEFAULT + N' ' + dc.[definition] COLLATE DATABASE_DEFAULT
        FROM sys.default_constraints dc
        JOIN sys.tables t ON t.[object_id] = dc.[parent_object_id]
        JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
        UNION ALL
        SELECT N'IDX ' + t.[name] COLLATE DATABASE_DEFAULT + N'.' + i.[name] COLLATE DATABASE_DEFAULT
            + CASE WHEN i.[is_primary_key] = 1 THEN N' PK' WHEN i.[is_unique_constraint] = 1 THEN N' UQ' WHEN i.[is_unique] = 1 THEN N' UNIQUE' ELSE N'' END
            + N' (' + ISNULL(STUFF((SELECT N',' + ic_c.[name] COLLATE DATABASE_DEFAULT + CASE WHEN ic.[is_descending_key] = 1 THEN N' DESC' ELSE N'' END
                FROM sys.index_columns ic JOIN sys.columns ic_c ON ic_c.[object_id] = ic.[object_id] AND ic_c.[column_id] = ic.[column_id]
                WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id] AND ic.[is_included_column] = 0
                ORDER BY ic.[key_ordinal] FOR XML PATH('')), 1, 1, N'') COLLATE DATABASE_DEFAULT, N'') + N')'
            + ISNULL(N' INCLUDE (' + STUFF((SELECT N',' + ic_c.[name] COLLATE DATABASE_DEFAULT
                FROM sys.index_columns ic JOIN sys.columns ic_c ON ic_c.[object_id] = ic.[object_id] AND ic_c.[column_id] = ic.[column_id]
                WHERE ic.[object_id] = i.[object_id] AND ic.[index_id] = i.[index_id] AND ic.[is_included_column] = 1
                ORDER BY ic_c.[name] FOR XML PATH('')), 1, 1, N'') COLLATE DATABASE_DEFAULT + N')', N'')
            + ISNULL(N' WHERE ' + i.[filter_definition] COLLATE DATABASE_DEFAULT, N'')
            + CASE WHEN i.[type] = 1 THEN N' CLUSTERED' ELSE N'' END
        FROM sys.indexes i
        JOIN sys.tables t ON t.[object_id] = i.[object_id]
        WHERE t.[is_ms_shipped] = 0 AND i.[type] > 0
        UNION ALL
        SELECT N'FK ' + t.[name] COLLATE DATABASE_DEFAULT + N'.' + fk.[name] COLLATE DATABASE_DEFAULT + N' ('
            + STUFF((SELECT N',' + fc.[name] COLLATE DATABASE_DEFAULT FROM sys.foreign_key_columns fkc
                JOIN sys.columns fc ON fc.[object_id] = fkc.[parent_object_id] AND fc.[column_id] = fkc.[parent_column_id]
                WHERE fkc.[constraint_object_id] = fk.[object_id] ORDER BY fkc.[constraint_column_id] FOR XML PATH('')), 1, 1, N'') COLLATE DATABASE_DEFAULT
            + N') -> ' + rt.[name] COLLATE DATABASE_DEFAULT + N' ('
            + STUFF((SELECT N',' + rc.[name] COLLATE DATABASE_DEFAULT FROM sys.foreign_key_columns fkc
                JOIN sys.columns rc ON rc.[object_id] = fkc.[referenced_object_id] AND rc.[column_id] = fkc.[referenced_column_id]
                WHERE fkc.[constraint_object_id] = fk.[object_id] ORDER BY fkc.[constraint_column_id] FOR XML PATH('')), 1, 1, N'') COLLATE DATABASE_DEFAULT
            + N') ON DELETE ' + fk.[delete_referential_action_desc] COLLATE DATABASE_DEFAULT
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.[object_id] = fk.[parent_object_id]
        JOIN sys.tables rt ON rt.[object_id] = fk.[referenced_object_id]
        UNION ALL
        SELECT N'CHK ' + t.[name] COLLATE DATABASE_DEFAULT + N'.' + cc.[name] COLLATE DATABASE_DEFAULT + N' ' + cc.[definition] COLLATE DATABASE_DEFAULT
        FROM sys.check_constraints cc
        JOIN sys.tables t ON t.[object_id] = cc.[parent_object_id]";
}
