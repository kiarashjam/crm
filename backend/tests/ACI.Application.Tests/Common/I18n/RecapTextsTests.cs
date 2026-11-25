using System.Text.RegularExpressions;
using ACI.Application.Common.I18n;

namespace ACI.Application.Tests.Common.I18n;

/// <summary>
/// The recap emails' words in English, German and French: every entry in all three, the same
/// placeholders in each, Swiss German spelling and Romandy punctuation, as MemberTexts.
/// </summary>
public class RecapTextsTests
{
    private static readonly Regex Placeholder = new(@"\{(\w+)\}");

    private static HashSet<string> PlaceholdersOf(string text) =>
        Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    [Fact]
    public void EveryEntryHasAllThreeLanguages_WithTheSamePlaceholdersAsTheEnglish()
    {
        RecapTexts.All.Should().NotBeEmpty();
        foreach (var (key, (en, de, fr)) in RecapTexts.All)
        {
            en.Should().NotBeNullOrWhiteSpace(key);
            de.Should().NotBeNullOrWhiteSpace(key);
            fr.Should().NotBeNullOrWhiteSpace(key);
            PlaceholdersOf(de).Should().BeEquivalentTo(PlaceholdersOf(en), $"the German of {key} fills what the English does");
            PlaceholdersOf(fr).Should().BeEquivalentTo(PlaceholdersOf(en), $"the French of {key} fills what the English does");
        }
    }

    [Fact]
    public void GermanIsSwissSpelling_WithoutAnSharpS()
    {
        foreach (var (key, (_, de, _)) in RecapTexts.All)
            de.Should().NotContain("ß", $"Swiss German writes ss ({key})");
    }

    [Fact]
    public void FrenchPutsANoBreakSpaceBeforeColonsQuestionMarksExclamationMarksAndSemicolons()
    {
        foreach (var (key, (_, _, fr)) in RecapTexts.All)
        {
            // The date patterns are .NET's, not words.
            if (key.StartsWith("date.", StringComparison.Ordinal)) continue;
            Regex.IsMatch(fr, "[^ ][:?!;]").Should().BeFalse($"'{fr}' ({key}) has a no-break space before : ? ! ;");
        }
    }

    [Fact]
    public void PlaceholdersAreFilled_AndAnyOtherLanguageReadsEnglish()
    {
        RecapTexts.T("fr", "text.line", ("label", "Nouveaux prospects"), ("value", "12")).Should().Be("Nouveaux prospects : 12");
        RecapTexts.T("de", "title.weekly").Should().Be("Wochenrückblick");
        RecapTexts.T("it", "title.weekly").Should().Be("Weekly recap");
        var act = () => RecapTexts.T("en", "no.such.key");
        act.Should().Throw<KeyNotFoundException>();
    }
}
