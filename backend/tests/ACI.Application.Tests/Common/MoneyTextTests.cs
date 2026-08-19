using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Reading amounts out of the free-text Deal.Value column.
/// </summary>
/// <remarks>
/// The reporting queries used to do <c>Replace("$","")</c> and fall through to
/// zero when the parse failed, so a deal priced "CHF 85,500" counted as worth
/// nothing and the pipeline total was silently short. Most of these tests are
/// about the shapes that used to be read as zero.
/// </remarks>
public class MoneyTextTests
{
    [Theory]
    // The shapes that previously became zero.
    [InlineData("CHF 85,500", 85500)]
    [InlineData("CHF85500", 85500)]
    [InlineData("€50,000", 50000)]
    [InlineData("50 000 EUR", 50000)]
    [InlineData("GBP 1,200.50", 1200.50)]
    // ...and the shapes that already worked, which must keep working.
    [InlineData("$1,234.56", 1234.56)]
    [InlineData("1234", 1234)]
    [InlineData("85,500", 85500)]
    [InlineData("$0", 0)]
    public void ReadsAnAmountWhateverShapeItIsIn(string raw, decimal expected)
    {
        MoneyText.TryParseAmount(raw, out var value).Should().BeTrue($"'{raw}' holds a number");
        value.Should().Be(expected);
    }

    [Theory]
    // Both separators present: the LAST one is the decimal point, which settles
    // US and European notation without knowing who typed it.
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1.234.567,89", 1234567.89)]
    [InlineData("1,234,567.89", 1234567.89)]
    public void ResolvesEitherNotation(string raw, decimal expected)
    {
        MoneyText.TryParseAmount(raw, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    // A lone comma is a decimal point only with one or two digits after it.
    [InlineData("85,500", 85500)]   // eighty-five thousand, not 85.5
    [InlineData("85,5", 85.5)]
    [InlineData("85,50", 85.50)]
    public void TreatsALoneCommaByWhatFollowsIt(string raw, decimal expected)
    {
        MoneyText.TryParseAmount(raw, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    // A lone dot with three digits after it and at most three before is grouped
    // thousands. The bound on the integer part is load-bearing: without it
    // "1234.567" parsed as 1234567.
    [InlineData("1.234", 1234)]
    [InlineData("85.500", 85500)]
    [InlineData("1234.567", 1234.567)]
    [InlineData("100.00", 100.00)]
    [InlineData("1.5", 1.5)]
    public void TreatsALoneDotByWhatSurroundsIt(string raw, decimal expected)
    {
        MoneyText.TryParseAmount(raw, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("-500", -500)]
    [InlineData("(500)", -500)]
    [InlineData("-CHF 1,000", -1000)]
    public void UnderstandsNegatives(string raw, decimal expected)
    {
        MoneyText.TryParseAmount(raw, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void DoesNotNegateOnAStrayDash()
    {
        // Testing for '-' anywhere rather than at the start would have turned a
        // range into a negative amount.
        MoneyText.TryParseAmount("1000-2000", out var value).Should().BeTrue();
        value.Should().BePositive();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TBC")]
    [InlineData("to be confirmed")]
    [InlineData("$")]
    [InlineData("CHF")]
    [InlineData(".")]
    [InlineData(",")]
    public void ReportsFailureRatherThanReturningZero(string? raw)
    {
        // The whole point. "We could not read this" and "this is worth nothing"
        // are different facts, and only one of them is a data-entry problem.
        MoneyText.TryParseAmount(raw, out var value).Should().BeFalse($"'{raw}' holds no number");
        value.Should().Be(0m);
    }

    [Fact]
    public void NormaliseCurrencyDefaultsToTheAssumptionAlreadyInTheSystem()
    {
        MoneyText.NormaliseCurrency(null).Should().Be("USD");
        MoneyText.NormaliseCurrency("").Should().Be("USD");
        MoneyText.NormaliseCurrency("  ").Should().Be("USD");
    }

    [Fact]
    public void NormaliseCurrencyUppercasesAndTrims()
    {
        MoneyText.NormaliseCurrency(" chf ").Should().Be("CHF");
        MoneyText.NormaliseCurrency("eur").Should().Be("EUR");
    }

    [Fact]
    public void NeverInfersCurrencyFromTheAmountText()
    {
        // A "€" in the value column does not make the deal's currency EUR — the
        // currency lives in its own column, and guessing would let a typo in a
        // free-text field silently reassign a deal's currency.
        MoneyText.NormaliseCurrency(null).Should().Be("USD");
        MoneyText.TryParseAmount("€50,000", out var value).Should().BeTrue();
        value.Should().Be(50000m);
    }
}
