using System.Text.RegularExpressions;
using ACI.Application.Common.I18n;

namespace ACI.Application.Tests.Common.I18n;

/// <summary>
/// What the server writes to a member, in English, German and French: every entry in all three,
/// the same placeholders in each, Swiss German spelling, Romandy punctuation, the QR-bill in the
/// SIX guidelines' words, and dates in the reader's language.
/// </summary>
public class MemberTextsTests
{
    private static readonly Regex Placeholder = new(@"\{(\w+)\}");

    private static HashSet<string> PlaceholdersOf(string text) =>
        Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    [Fact]
    public void EveryEntryHasAllThreeLanguages_WithTheSamePlaceholdersAsTheEnglish()
    {
        MemberTexts.All.Should().NotBeEmpty();
        foreach (var (key, (en, de, fr)) in MemberTexts.All)
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
        foreach (var (key, (_, de, _)) in MemberTexts.All)
            de.Should().NotContain("ß", $"Swiss German writes ss ({key})");
    }

    [Fact]
    public void FrenchPutsANoBreakSpaceBeforeColonsQuestionMarksExclamationMarksAndSemicolons()
    {
        foreach (var (key, (_, _, fr)) in MemberTexts.All)
        {
            // event.when is a .NET date pattern ("HH:mm"), not words.
            if (key == "event.when") continue;
            Regex.IsMatch(fr, "[^ ][:?!;]").Should().BeFalse($"'{fr}' ({key}) has a no-break space before : ? ! ;");
        }
    }

    [Theory]
    [InlineData("paymentPart", "Payment part", "Zahlteil", "Section paiement")]
    [InlineData("accountPayableTo", "Account / Payable to", "Konto / Zahlbar an", "Compte / Payable à")]
    [InlineData("reference", "Reference", "Referenz", "Référence")]
    [InlineData("additionalInformation", "Additional information", "Zusätzliche Informationen", "Informations supplémentaires")]
    [InlineData("currency", "Currency", "Währung", "Monnaie")]
    [InlineData("amount", "Amount", "Betrag", "Montant")]
    [InlineData("receipt", "Receipt", "Empfangsschein", "Récépissé")]
    [InlineData("acceptancePoint", "Acceptance point", "Annahmestelle", "Point de dépôt")]
    [InlineData("separate", "Separate before paying in", "Vor der Einzahlung abzutrennen", "A détacher avant le versement")]
    [InlineData("payableBy", "Payable by", "Zahlbar durch", "Payable par")]
    [InlineData("payableByNameAddress", "Payable by (name/address)", "Zahlbar durch (Name/Adresse)", "Payable par (nom/adresse)")]
    public void TheQrBillHeadingsAreTheSixGuidelinesOwnWords(string key, string en, string de, string fr)
    {
        MemberTexts.T("en", "qr." + key).Should().Be(en);
        MemberTexts.T("de", "qr." + key).Should().Be(de);
        MemberTexts.T("fr", "qr." + key).Should().Be(fr);
    }

    [Fact]
    public void PlaceholdersAreFilled_AnyOtherLanguageReadsEnglish_AndAnUnfilledOneStaysVisible()
    {
        MemberTexts.T("de", "welcome.subject", ("club", "Pavillon 46")).Should().Be("Willkommen bei Pavillon 46");
        MemberTexts.T("fr", "welcome.subject", ("club", "Pavillon 46")).Should().Be("Bienvenue chez Pavillon 46");
        MemberTexts.T("it", "welcome.subject", ("club", "Pavillon 46")).Should().Be("Welcome to Pavillon 46");
        MemberTexts.T("de", "pdf.page", ("page", 1)).Should().Be("Seite 1 von {pages}");
        var act = () => MemberTexts.T("en", "no.such.key");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void WordsTheLedgerStoresInEnglishAreTranslated_AndOnesItDoesNotKnowAreShownAsTheyAre()
    {
        MemberTexts.Kind("de", "Dues").Should().Be("Mitgliederbeitrag");
        MemberTexts.Kind("fr", "dues").Should().Be("Cotisation");
        MemberTexts.Kind("en", "Dues").Should().Be("Dues");
        MemberTexts.Kind("de", "Golf lesson").Should().Be("Golf lesson");
        MemberTexts.Method("fr", "Wire").Should().Be("Virement");
        MemberTexts.Method("en", "Wire").Should().Be("Wire", "English reads what it always did");
        MemberTexts.Role("de", "Spouse").Should().Be("Ehepartner/in");
        MemberTexts.Role("de", null).Should().BeEmpty();
        MemberTexts.JoinOr("de", ["Karte", "TWINT"]).Should().Be("Karte oder TWINT");
        MemberTexts.JoinOr("fr", ["carte", "TWINT", "prélèvement SEPA"]).Should().Be("carte ou TWINT ou prélèvement SEPA");
    }

    [Fact]
    public void DatesAreInTheReadersLanguage_AndEnglishKeepsWhatItAlwaysPrinted()
    {
        var day = new DateTime(2026, 10, 1);
        MemberTexts.Date("en", "de-CH", day).Should().Be("1 Oct 2026");
        MemberTexts.Date("de", "de-CH", day).Should().StartWith("1. Okt").And.EndWith("2026");
        MemberTexts.Date("fr", "de-CH", day).Should().StartWith("1 oct").And.EndWith("2026");

        MemberTexts.Period("en", "de-CH", day, day.AddMonths(1)).Should().Be("October 2026");
        MemberTexts.Period("de", "de-CH", day, day.AddMonths(1)).Should().Be("Oktober 2026");
        MemberTexts.Period("fr", "fr-CH", day, day.AddMonths(1)).Should().Be("octobre 2026");
        MemberTexts.Period("en", "de-CH", day, day.AddDays(15)).Should().Be("1 Oct 2026 – 15 Oct 2026");
    }

    [Fact]
    public void TheEnglishStandardWordsOfTheFormsAreTheOnesTheyAlwaysShowed()
    {
        // A capture form keeps these in its rows; reading them as the standard words is what lets
        // a visitor see them in their own language.
        MemberTexts.T("en", "form.captureConsent").Should().Be(ACI.Application.Services.FormConsentDefaults.Text);
        MemberTexts.T("en", "form.captureThanks").Should().Be("Thanks. We'll be in touch.");
        MemberTexts.T("en", "form.applicationThanks").Should().Be("Thank you for applying. The membership committee will be in touch.");
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("fr", "fr")]
    [InlineData("fr-CH", "fr")]
    [InlineData("DE", "de")]
    [InlineData("it-CH", "en")]
    public void AMemberIsWrittenToInTheirOwnLanguage_ElseEnglish(string? preferred, string language)
    {
        AppLanguages.ForMember(preferred).Should().Be(language);
    }

    [Fact]
    public void AWorkspaceLanguageIsUsedOnlyWhereAskedFor_AndDatesTakeTheWorkspacesRegion()
    {
        AppLanguages.Resolve(null, "fr-CH").Should().Be("fr");
        AppLanguages.Resolve("de", "fr-CH").Should().Be("de");
        AppLanguages.Resolve(null, "it-CH").Should().Be("en");
        AppLanguages.DateCulture("fr", "de-CH").Name.Should().Be("fr-CH");
        AppLanguages.DateCulture("de", "ja-JP").Name.Should().Be("de", "there is no de-JP culture, so the language alone");
        AppLanguages.IsValidChoice("it").Should().BeFalse();
        AppLanguages.IsValidChoice(null).Should().BeTrue("no choice is a choice");
    }
}
