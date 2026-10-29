using ACI.Domain.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The lead status vocabulary, and the properties the remap has to have to be safe
/// to run against real records.
/// </summary>
/// <remarks>
/// The alignment SQL is destructive: it rewrites <c>Leads.Status</c> and deletes
/// rows from <c>LeadStatuses</c>. It runs once, against production data, so anything
/// wrong with the tables it is generated from is wrong for good. These are the
/// invariants that make it safe, checked on the arrays rather than on a copy of the
/// SQL.
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
        // Step 5 deletes unused rows whose name is in the retired map. A name in both
        // tables would be inserted by step 2 and deleted by step 5, and every lead on
        // it moved to another stage.
        var retired = LeadStatusVocabulary.Retired.Select(r => r.Old).ToArray();
        LeadStatusVocabulary.Default.Should().NotIntersectWith(retired);
    }

    [Fact]
    public void OnlyLabelsThisAppSeededAreRetired()
    {
        // The alignment tells an organisation still on the old list by its having none
        // of the stages the rework added, and only there deletes retired rows. A label
        // that was never in the old seed would be a manager's own, and none of ours to delete.
        LeadStatusVocabulary.PreviousDefault.Should().Contain(LeadStatusVocabulary.Retired.Select(r => r.Old));
        LeadStatusVocabulary.AddedInRework.Should().NotBeEmpty()
            .And.OnlyContain(name => LeadStatusVocabulary.Default.Contains(name));
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
