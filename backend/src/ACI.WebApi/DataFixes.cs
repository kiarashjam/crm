namespace ACI.WebApi;

/// <summary>
/// One-time data fixes, run at startup after the schema is in place.
/// </summary>
/// <remarks>
/// The rest of the startup DDL is idempotent by its shape: "add this column if it
/// is missing" can run on every start. A fix that changes data cannot — run again,
/// it would undo whatever people did since. So each fix is recorded by name in
/// AppliedDataFixes, inside the same transaction as the change, and skipped once
/// recorded. UPDLOCK/HOLDLOCK keeps two instances starting together from both
/// applying it.
/// </remarks>
public static class DataFixes
{
    public const string CreateTableSql = @"
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppliedDataFixes')
        CREATE TABLE [AppliedDataFixes] (
            [Name] nvarchar(200) NOT NULL CONSTRAINT [PK_AppliedDataFixes] PRIMARY KEY,
            [AppliedAtUtc] datetime2 NOT NULL
        );";

    public const string UnfreezeLeadScoresName = "2026-10 unfreeze lead scores";

    /// <summary>
    /// Give every lead its automatic score back.
    /// </summary>
    /// <remarks>
    /// From #57 until the fix in LeadService.UpdateAsync, saving either lead edit
    /// dialog marked the score as set by hand — the dialogs send the current score
    /// back with every save — so fixing a phone number froze that lead's score for
    /// good. Nothing recorded which manual scores were deliberate, so this clears
    /// them all; a score someone did set by hand is set again by typing it. The
    /// background rescore (LeadFollowUpBackgroundService) then recomputes them.
    /// </remarks>
    public const string UnfreezeLeadScoresSql = @"
        BEGIN TRY
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'" + UnfreezeLeadScoresName + @"')
            BEGIN
                UPDATE [Leads] SET [ScoreIsManual] = 0 WHERE [ScoreIsManual] = 1;
                INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'" + UnfreezeLeadScoresName + @"', SYSUTCDATETIME());
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";

    public const string KeepClubOpeningBalancesName = "2026-10 keep club opening balances";

    /// <summary>
    /// Give each existing club member the part of their house balance that no charge explains.
    /// </summary>
    /// <remarks>
    /// Until the fix in ClubDocumentService.RecomputeBalanceAsync, the balance was set to
    /// what the member's charges left owing every time a charge was posted or paid, so an
    /// opening balance, a correction typed in by hand or a balance merged in from a
    /// duplicate lasted only until the next charge. That part is the balance plus what the
    /// charges still owe: 0 for a member whose balance was last recomputed, and exactly the
    /// entered difference for one whose balance has been set since. The recompute now adds
    /// it back instead of wiping it. Charges are JSON, read with JSON_VALUE; a payload that
    /// is not JSON counts as nothing.
    /// </remarks>
    public const string KeepClubOpeningBalancesSql = @"
        BEGIN TRY
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'" + KeepClubOpeningBalancesName + @"')
            BEGIN
                UPDATE m SET [ManualBalance] = m.[HouseAccountBalance] + ISNULL(owed.[Outstanding], 0)
                FROM [ClubMembers] m
                OUTER APPLY (
                    SELECT SUM(ISNULL(TRY_CAST(JSON_VALUE(p.[Json], '$.amount') AS decimal(18,2)), 0)
                             - ISNULL(TRY_CAST(JSON_VALUE(p.[Json], '$.paidAmount') AS decimal(18,2)), 0)) AS [Outstanding]
                    FROM [ClubDocuments] d
                    CROSS APPLY (SELECT CASE WHEN ISJSON(d.[Payload]) = 1 THEN d.[Payload] END AS [Json]) p
                    WHERE d.[Kind] = N'charges'
                      AND (d.[OrganizationId] = m.[OrganizationId] OR (d.[OrganizationId] IS NULL AND m.[OrganizationId] IS NULL))
                      AND LOWER(JSON_VALUE(p.[Json], '$.memberId')) = LOWER(CONVERT(nvarchar(36), m.[Id]))
                      AND ISNULL(JSON_VALUE(p.[Json], '$.status'), N'') <> N'Voided'
                ) owed;
                INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'" + KeepClubOpeningBalancesName + @"', SYSUTCDATETIME());
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";
}
