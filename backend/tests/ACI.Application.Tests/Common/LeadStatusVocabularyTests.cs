using ACI.Domain.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The lead status vocabulary, and the properties the remap has to have to be safe
/// to run against real records.
/// </summary>
/// <remarks>
/// The alignment SQL is destructive: it rewrites <c>Leads.Status</c> and deletes
/// rows from <c>LeadStatuses</c>. It runs on every boot, so anything wrong with the
/// two tables it is generated from is wrong repeatedly and against production data.
/// These are the invariants that make it safe, checked on the arrays rather than on
/// a copy of the SQL.
/// </remarks>
public class LeadStatusVocabularyTests
{
    [Fact]
    public void TheStagesAreDistinctAndNonEmpty()
    {
        LeadStatusVocabulary.Default.Should().OnlyHaveUniqueItems();
        LeadStatusVocabulary.Default.Should().OnlyContain(name => !string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void EveryRetiredLabelLandsOnAStageThatExists()
    {
        // Step 3 of the alignment sets Leads.Status to the mapped name and looks the
        // FK up by that name. A target that is not one of the seeded stages would
        // leave the lead pointing at nothing AND wearing a label the pipeline
        // cannot resolve — the exact failure this whole change exists to fix.
        foreach (var (old, @new) in LeadStatusVocabulary.Retired)
        {
            LeadStatusVocabulary.Default.Should().Contain(@new,
                $"'{old}' is remapped to '{@new}', which must be a stage every organisation has");
        }
    }

    [Fact]
    public void NoStageIsAlsoRetired()
    {
        // Step 5 deletes every row whose name is in the retired map. A name in both
        // tables would be inserted by step 1 and deleted by step 5 on every single
        // boot, permanently churning the FKs of every lead sitting on it.
        var retired = LeadStatusVocabulary.Retired.Select(r => r.Old).ToArray();
        LeadStatusVocabulary.Default.Should().NotIntersectWith(retired);
    }

    [Fact]
    public void RetiredLabelsAreListedOnce()
    {
        // @map declares OldName as a PRIMARY KEY, so a duplicate would not fail a
        // test — it would fail the INSERT at runtime, inside the try/catch, and the
        // alignment would silently never happen again.
        LeadStatusVocabulary.Retired.Select(r => r.Old).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheGeneratedSqlNamesEveryStageAndEveryRetiredLabel()
    {
        var sql = LeadStatusVocabulary.AlignmentSql();

        foreach (var name in LeadStatusVocabulary.Default)
        {
            sql.Should().Contain($"N'{name}'", $"'{name}' must reach the database");
        }

        foreach (var (old, @new) in LeadStatusVocabulary.Retired)
        {
            sql.Should().Contain($"(N'{old}', N'{@new}')");
        }
    }

    [Fact]
    public void TheStagesAreNumberedFromZeroInOrder()
    {
        var sql = LeadStatusVocabulary.AlignmentSql();
        for (var i = 0; i < LeadStatusVocabulary.Default.Length; i++)
        {
            sql.Should().Contain($"(N'{LeadStatusVocabulary.Default[i]}', {i})",
                "DisplayOrder is what the picker sorts by, so it has to match the agreed order");
        }
    }

    [Fact]
    public void NamesAreQuotedRatherThanInterpolatedRaw()
    {
        // Nothing in Default contains an apostrophe today. "Lost / Not Interested"
        // was very nearly "Lost / Not Interested (Client's decision)", and a name
        // like that would have ended the statement early and run whatever followed.
        var quoted = typeof(LeadStatusVocabulary)
            .GetMethod("Quote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { "Client's decision" });

        quoted.Should().Be("N'Client''s decision'");
    }
}
