using ACI.Application.Common.Pdf;
using FluentAssertions;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// The encoding, and specifically the things it must not quietly get wrong.
/// </summary>
/// <remarks>
/// A contract carries names, amounts and email addresses. Any of those can hold a
/// character that Latin-1 has no room for, and .NET 8 has no code page 1252 to fall
/// back on, so the mapping here is hand-written — which is exactly why it is tested
/// character by character.
/// </remarks>
public class WinAnsiTests
{
    private static string Round(string text) => WinAnsi.Literal(WinAnsi.Encode(text).Bytes);

    [Theory]
    // The characters this system actually generates and that Latin-1 cannot hold.
    // The em dash appears in every contract title the CRM builds.
    [InlineData("—", 0x97)] // em dash
    [InlineData("–", 0x96)] // en dash
    [InlineData("’", 0x92)] // right single quote, used as an apostrophe
    [InlineData("“", 0x93)] // left double quote
    [InlineData("€", 0x80)] // euro
    [InlineData("…", 0x85)] // ellipsis, which the truncation helpers append
    [InlineData("Œ", 0x8C)] // OE ligature
    public void MapsTheTypographicRangeLatin1Lacks(string text, int expected)
    {
        WinAnsi.Encode(text).Bytes.Should().Equal(new[] { (byte)expected });
    }

    [Theory]
    // Every accent a French, German or Italian name needs. These are the names this
    // is used for, so "renders correctly" is not optional.
    [InlineData("é", 0xE9)] // e acute
    [InlineData("ï", 0xEF)] // i diaeresis, as in Anais
    [InlineData("ü", 0xFC)] // u diaeresis, as in Zurich
    [InlineData("è", 0xE8)] // e grave
    [InlineData("ç", 0xE7)] // c cedilla
    [InlineData("ß", 0xDF)] // sharp s
    [InlineData("À", 0xC0)] // A grave
    public void MapsWesternEuropeanAccentsUnchanged(string text, int expected)
    {
        var encoded = WinAnsi.Encode(text);
        encoded.Bytes.Should().Equal(new[] { (byte)expected });
        encoded.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void ReportsWhatItCannotCarryRatherThanHidingIt()
    {
        // A member whose name renders as "??" must be knowable. Substituting in
        // silence would put a document with a mangled party name into somebody's
        // permanent records.
        var encoded = WinAnsi.Encode("张伟 Berger");
        encoded.Unmapped.Should().Equal(new[] { '张', '伟' });
        encoded.Bytes.Should().Contain((byte)'?');
    }

    [Fact]
    public void ReportsEachUnmappableCharacterOnlyOnce()
    {
        WinAnsi.Encode("张张张").Unmapped.Should().Equal(new[] { '张' });
    }

    [Fact]
    public void FoldsAnAccentItCannotCarryToTheLetterUnderneath()
    {
        // "a" is a better record of a name than "?", and folding only ever keeps a
        // base letter that is already plain ASCII, so it cannot invent one.
        var encoded = WinAnsi.Encode("Māori");
        System.Text.Encoding.ASCII.GetString(encoded.Bytes).Should().Be("Maori");
        encoded.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotFoldSomethingWithNoLatinLetterBehindIt()
    {
        // A currency sign has no base letter; substituting is right, folding is not.
        WinAnsi.Encode("₹").Unmapped.Should().Equal(new[] { '₹' });
    }

    [Theory]
    // Invisible characters paste in from Word and from web forms constantly. Turning
    // a no-break space into "?" would be a visible defect for an invisible cause.
    [InlineData("CHF 1'450", "CHF 1'450")]
    [InlineData("CHF 1'450", "CHF 1'450")]
    [InlineData("soft­hyphen", "softhyphen")]
    [InlineData("zero​width", "zerowidth")]
    [InlineData("﻿leading bom", "leading bom")]
    [InlineData("2026‒2027", "2026-2027")]
    public void RewritesInvisiblesAndLookalikesInsteadOfSubstituting(string input, string expected)
    {
        var encoded = WinAnsi.Encode(input);
        System.Text.Encoding.ASCII.GetString(encoded.Bytes).Should().Be(expected);
        encoded.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public void EscapesTheCharactersThatWouldEndAStringLiteralEarly()
    {
        // An unbalanced parenthesis in a counterparty's name would corrupt the rest
        // of the page — every operator after it read as part of the string.
        Round("Dupont (the \"Member\")").Should().Be("(Dupont \\(the \"Member\"\\))");
        Round("back\\slash").Should().Be("(back\\\\slash)");
        Round(")(").Should().Be("(\\)\\()");
    }

    [Fact]
    public void WritesNonAsciiAsOctalSoTheFileStaysReadable()
    {
        // Keeping generated PDFs ASCII means they can be diffed and grepped, which
        // is how several of these bugs were found in the first place.
        Round("café — CHF").Should().Be("(caf\\351 \\227 CHF)");
    }

    [Fact]
    public void HandlesNothingAtAll()
    {
        WinAnsi.Encode(null).Bytes.Should().BeEmpty();
        WinAnsi.Encode("").Bytes.Should().BeEmpty();
        WinAnsi.Literal(WinAnsi.Encode("").Bytes).Should().Be("()");
    }

    [Fact]
    public void PassesPlainAsciiThroughByteForByte()
    {
        const string text = "jean-michel.dupont@example.ch +41 22 555 01 34";
        System.Text.Encoding.ASCII.GetString(WinAnsi.Encode(text).Bytes).Should().Be(text);
    }
}
