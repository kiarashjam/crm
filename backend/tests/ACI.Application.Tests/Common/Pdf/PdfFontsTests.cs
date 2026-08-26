using ACI.Application.Common.Pdf;
using FluentAssertions;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// The font metrics, checked against the published values.
/// </summary>
/// <remarks>
/// These numbers are the reason line breaking works, and getting one wrong throws
/// nothing — text simply runs into the margin, on one page, in one font, for one
/// string. So they are checked directly: a sample against Adobe's published
/// widths, and then a sweep asserting no code in any font is silently zero.
/// </remarks>
public class PdfFontsTests
{
    [Theory]
    // Straight from the Adobe Font Metrics files for the core fonts. If a
    // transcription slipped a column, these are what catch it.
    [InlineData(PdfFont.Helvetica, " ", 278)]
    [InlineData(PdfFont.Helvetica, "A", 667)]
    [InlineData(PdfFont.Helvetica, "W", 944)]
    [InlineData(PdfFont.Helvetica, "i", 222)]
    [InlineData(PdfFont.Helvetica, ".", 278)]
    [InlineData(PdfFont.HelveticaBold, "A", 722)]
    [InlineData(PdfFont.TimesRoman, " ", 250)]
    [InlineData(PdfFont.TimesRoman, "A", 722)]
    [InlineData(PdfFont.TimesRoman, "W", 944)]
    [InlineData(PdfFont.TimesItalic, "A", 611)]
    [InlineData(PdfFont.TimesBold, "W", 1000)]
    [InlineData(PdfFont.Courier, "A", 600)]
    [InlineData(PdfFont.Courier, "i", 600)]
    public void MatchesThePublishedWidths(PdfFont font, string glyph, int thousandthsOfEm)
    {
        // Measured at 1000pt so a width in 1/1000 em reads as points directly.
        PdfFonts.Measure(font, glyph, 1000).Should().BeApproximately(thousandthsOfEm, 0.001);
    }

    [Theory]
    [InlineData(PdfFont.Helvetica)]
    [InlineData(PdfFont.HelveticaBold)]
    [InlineData(PdfFont.HelveticaOblique)]
    [InlineData(PdfFont.TimesRoman)]
    [InlineData(PdfFont.TimesBold)]
    [InlineData(PdfFont.TimesItalic)]
    [InlineData(PdfFont.Courier)]
    public void EveryPrintableWinAnsiCodeHasAWidth(PdfFont font)
    {
        // A zero width is worse than a wrong one: the glyph draws but advances
        // nothing, so the rest of the line piles up on top of it.
        var zero = new List<int>();
        for (var code = 32; code < 256; code++)
        {
            // Five codes are genuinely unassigned in WinAnsi, plus 0x7F.
            if (code is 127 or 129 or 141 or 143 or 144 or 157) continue;
            // Probed as a BYTE, not as a character: the width table is a fact about
            // the font, whereas the string overload also runs the encoder, which
            // deliberately drops some characters (a Unicode soft hyphen among them).
            if (PdfFonts.Measure(font, new[] { (byte)code }, 1000) <= 0) zero.Add(code);
        }
        zero.Should().BeEmpty("every assigned WinAnsi code must advance the pen");
    }

    [Fact]
    public void CourierIsTheOnlyFixedPitchFont()
    {
        var iInCourier = PdfFonts.Measure(PdfFont.Courier, "i", 100);
        var wInCourier = PdfFonts.Measure(PdfFont.Courier, "W", 100);
        iInCourier.Should().Be(wInCourier);

        PdfFonts.Measure(PdfFont.Helvetica, "i", 100)
            .Should().BeLessThan(PdfFonts.Measure(PdfFont.Helvetica, "W", 100));
    }

    [Fact]
    public void ScalesLinearlyWithPointSize()
    {
        var at10 = PdfFonts.Measure(PdfFont.TimesRoman, "Membership Agreement", 10);
        var at20 = PdfFonts.Measure(PdfFont.TimesRoman, "Membership Agreement", 20);
        at20.Should().BeApproximately(at10 * 2, 0.001);
    }

    [Fact]
    public void TrackingIsCountedOncePerGlyph()
    {
        const string text = "CLUB";
        var plain = PdfFonts.Measure(PdfFont.HelveticaBold, text, 9);
        var tracked = PdfFonts.Measure(PdfFont.HelveticaBold, text, 9, tracking: 2);

        // Four glyphs, two points each. Under-counting this is what lets a
        // letter-spaced heading measure narrower than it draws and overrun.
        tracked.Should().BeApproximately(plain + 8, 0.001);
    }

    [Fact]
    public void MeasuringAStringAgreesWithMeasuringItsEncodedBytes()
    {
        // The layout measures strings; the canvas draws bytes. If these ever
        // disagreed, every centred and right-aligned run would be slightly wrong.
        const string text = "Anais Berger — CHF 1'450.00";
        var viaString = PdfFonts.Measure(PdfFont.TimesRoman, text, 10.4);
        var viaBytes = PdfFonts.Measure(PdfFont.TimesRoman, WinAnsi.Encode(text).Bytes, 10.4);
        viaBytes.Should().Be(viaString);
    }

    [Fact]
    public void EveryFontHasADistinctResourceNameAndBaseFont()
    {
        PdfFonts.All.Select(PdfFonts.ResourceName).Should().OnlyHaveUniqueItems();
        PdfFonts.All.Select(PdfFonts.BaseFontName).Should().OnlyHaveUniqueItems();
        // The names are what a reader looks up in its built-in table; a typo means a
        // substituted font and different metrics from the ones we measured with.
        PdfFonts.All.Select(PdfFonts.BaseFontName).Should().BeEquivalentTo(new[]
        {
            "Helvetica", "Helvetica-Bold", "Helvetica-Oblique",
            "Times-Roman", "Times-Bold", "Times-Italic", "Courier",
        });
    }
}
