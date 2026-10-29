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

    public const string AlignLeadStatusVocabularyName = "2026-10 align lead status vocabulary";

    /// <summary>
    /// Bring organisations still on the old lead status list onto the agreed one.
    /// </summary>
    /// <remarks>
    /// This ran on every start, outside this ledger, so whatever a manager did to the
    /// statuses was undone at the next deploy or restart. Every organisation was brought
    /// onto the agreed list by those runs, so once is enough; see
    /// <see cref="ACI.Domain.Common.LeadStatusVocabulary.AlignmentSql"/> for what it
    /// touches and what it leaves alone.
    /// </remarks>
    public static readonly string AlignLeadStatusVocabularySql = @"
        BEGIN TRY
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'" + AlignLeadStatusVocabularyName + @"')
            BEGIN
" + ACI.Domain.Common.LeadStatusVocabulary.AlignmentSql() + @"
                INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'" + AlignLeadStatusVocabularyName + @"', SYSUTCDATETIME());
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";
}
