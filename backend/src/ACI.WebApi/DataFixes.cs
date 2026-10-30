using ACI.Application.Common;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    public const string FillDealAmountsName = "2026-10 fill deal amounts";

    /// <summary>Records <see cref="FillDealAmountsName"/> as applied, once however many instances get here.</summary>
    public const string RecordDealAmountsFilledSql = @"
        BEGIN TRY
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'" + FillDealAmountsName + @"')
                INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'" + FillDealAmountsName + @"', SYSUTCDATETIME());
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";

    /// <summary>
    /// Give every existing deal the number its Value holds (see Deal.Amount).
    /// </summary>
    /// <remarks>
    /// <para>
    /// New and edited deals get it as they are saved (AppDbContext). Rows from before
    /// the column existed are read here, with the parser the rest of the app uses,
    /// MoneyText, which T-SQL cannot run: TRY_CAST reads "1.234" as one point two
    /// where the app reads twelve hundred, and "CHF 85,500" not at all.
    /// </para>
    /// <para>
    /// Only rows whose Amount is still empty are touched, so a second instance starting
    /// at the same moment writes the same numbers, and the ledger line stops later starts
    /// from re-reading the values that hold no number. It writes with ExecuteUpdate, past
    /// the audit trail, because nobody changed the deals' values.
    /// </para>
    /// </remarks>
    /// <returns>How many deals were given an amount.</returns>
    public static async Task<int> FillDealAmountsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var applied = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [AppliedDataFixes] WHERE [Name] = N'" + FillDealAmountsName + "'")
            .SingleAsync(ct);
        if (applied > 0) return 0;

        // Every row at once: a Guid and at most 64 characters each, read once ever.
        var rows = await db.Deals.AsNoTracking()
            .Where(d => d.Amount == null)
            .Select(d => new { d.Id, d.Value })
            .ToListAsync(ct);

        var filled = 0;
        // One UPDATE per distinct amount, so a thousand deals worth 0 or 5000 are a
        // handful of statements rather than a thousand.
        foreach (var byAmount in rows
            .Select(r => (r.Id, Amount: MoneyText.StoredAmount(r.Value)))
            .Where(r => r.Amount != null)
            .GroupBy(r => r.Amount!.Value))
        {
            decimal? amount = byAmount.Key;
            foreach (var chunk in byAmount.Select(r => r.Id).Chunk(500))
            {
                filled += await db.Deals
                    .Where(d => chunk.Contains(d.Id) && d.Amount == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(d => d.Amount, amount), ct);
            }
        }

        await db.Database.ExecuteSqlRawAsync(RecordDealAmountsFilledSql, ct);
        return filled;
    }

    public const string FillDealCurrenciesName = "2026-10 fill deal currencies";

    /// <summary>
    /// Give every existing deal a currency code, written the way new deals have it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The New Deal dialog opened with no currency picked and lead conversion never set
    /// one, so many deals have none, and everything counted those as USD for a club that
    /// bills in francs. New deals now take their workspace's currency
    /// (Organization.Currency), and these get the same: their workspace's, which is the
    /// default, CHF, everywhere when this first runs, and the default for a personal
    /// workspace's deals.
    /// </para>
    /// <para>
    /// A code written in another case or with spaces ("chf ") becomes the code ("CHF"),
    /// so it is no longer totalled as a currency of its own. Anything else, a symbol or a
    /// word, is left as it was: what "Fr." meant is not for a data fix to guess, and the
    /// reports show it under its own name. The audit trail is not written, as nobody
    /// priced the deals.
    /// </para>
    /// </remarks>
    public const string FillDealCurrenciesSql = @"
        BEGIN TRY
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'" + FillDealCurrenciesName + @"')
            BEGIN
                UPDATE [Deals] SET [Currency] = UPPER(LTRIM(RTRIM([Currency])))
                WHERE LTRIM(RTRIM([Currency])) COLLATE Latin1_General_BIN2 LIKE N'[A-Za-z][A-Za-z][A-Za-z]'
                  AND [Currency] COLLATE Latin1_General_BIN2 <> UPPER(LTRIM(RTRIM([Currency]))) COLLATE Latin1_General_BIN2;
                UPDATE d SET [Currency] = ISNULL(o.[Currency], N'CHF')
                FROM [Deals] d LEFT JOIN [Organizations] o ON o.[Id] = d.[OrganizationId]
                WHERE d.[Currency] IS NULL OR LTRIM(RTRIM(d.[Currency])) = N'';
                INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'" + FillDealCurrenciesName + @"', SYSUTCDATETIME());
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;";
}
