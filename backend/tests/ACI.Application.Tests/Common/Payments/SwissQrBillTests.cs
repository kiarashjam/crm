using ACI.Application.Common.Payments;

namespace ACI.Application.Tests.Common.Payments;

/// <summary>
/// The Swiss QR-bill's data: IBAN and QR-IBAN checks, the two kinds of reference, and the
/// payload of the Swiss QR Code, against the examples of the SIX Implementation Guidelines
/// for the QR-bill (version 2.3, structured addresses).
/// </summary>
public class SwissQrBillTests
{
    private static readonly QrBillAddress Creditor = new("Robert Schneider AG", "Rue du Lac", "1268", "2501", "Biel", "CH");
    private static readonly QrBillAddress Debtor = new("Pia-Maria Rutschmann-Schnyder", "Grosse Marktgasse", "28", "9400", "Rorschach", "CH");

    [Theory]
    [InlineData("CH4431999123000889012", true)]
    [InlineData("CH44 3199 9123 0008 8901 2", true)]
    [InlineData("ch5800791123000889012", true)]
    [InlineData("CH9300762011623852957", true)]
    [InlineData("LI21088100002324013AA", true)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("CH4431999123000889013", false)]  // one digit off: the check digits catch it
    [InlineData("CH44319991230008890", false)]    // too short for Switzerland
    [InlineData("", false)]
    [InlineData("not an iban", false)]
    public void AnIbanIsValidOnlyWhenItsCheckDigitsAreRight(string iban, bool valid)
    {
        Iban.IsValid(iban).Should().Be(valid);
    }

    [Fact]
    public void AQrIbanIsASwissIbanWithABankNumberFrom30000To31999()
    {
        Iban.IsQrIban("CH44 3199 9123 0008 8901 2").Should().BeTrue("its institution id is 31999");
        Iban.IsQrIban("CH5800791123000889012").Should().BeFalse("its institution id is 00791, a plain IBAN");
        Iban.IsQrIban("DE89370400440532013000").Should().BeFalse("only Swiss and Liechtenstein IBANs take a QR-bill");
        Iban.IsSwiss("DE89370400440532013000").Should().BeFalse();
        Iban.Format("CH4431999123000889012").Should().Be("CH44 3199 9123 0008 8901 2");
    }

    [Fact]
    public void AQrReferenceEndsInItsModulo10RecursiveCheckDigit()
    {
        // The guidelines' own example reference.
        PaymentReferences.QrCheckDigit("21000000000313947143000901").Should().Be(7);
        PaymentReferences.IsQrReference("210000000003139471430009017").Should().BeTrue();
        PaymentReferences.IsQrReference("21 00000 00003 13947 14300 09017").Should().BeTrue("spaces as printed do not matter");
        PaymentReferences.IsQrReference("210000000003139471430009016").Should().BeFalse();
        PaymentReferences.Format("210000000003139471430009017").Should().Be("21 00000 00003 13947 14300 09017");
    }

    [Fact]
    public void AClubsQrReferenceIsItsPrefixThenTheNumberThenTheCheckDigit()
    {
        var reference = PaymentReferences.QrReference("123456", 42);
        reference.Should().HaveLength(27).And.StartWith("123456").And.Contain("42");
        PaymentReferences.IsQrReference(reference).Should().BeTrue();
        PaymentReferences.QrReference(null, 1).Should().Be("00000000000000000000000001" + PaymentReferences.QrCheckDigit("00000000000000000000000001"));
        PaymentReferences.QrReference("12345678901234567890123", 123456).Should().BeNull("a prefix and number longer than 26 digits do not fit");
        PaymentReferences.QrReference("12a", 1).Should().BeNull("a prefix is digits only");
    }

    [Fact]
    public void ACreditorReferenceIsIso11649()
    {
        // ISO 11649's own example.
        PaymentReferences.CreditorReference("539007547034").Should().Be("RF18539007547034");
        PaymentReferences.IsCreditorReference("RF18 5390 0754 7034").Should().BeTrue();
        PaymentReferences.IsCreditorReference("RF19539007547034").Should().BeFalse();
        PaymentReferences.IsCreditorReference(PaymentReferences.CreditorReference(42)).Should().BeTrue();
        PaymentReferences.CreditorReference(42).Should().EndWith("0000000042");
        PaymentReferences.Format("RF18539007547034").Should().Be("RF18 5390 0754 7034");
    }

    /// <summary>The guidelines' example of a QR-bill to a QR-IBAN with a QR reference.</summary>
    [Fact]
    public void TheQrIbanExampleGivesTheGuidelinesPayload()
    {
        var bill = new QrBill("CH4431999123000889012", Creditor, 1949.75m, "CHF", Debtor,
            SwissQrBill.Qrr, "210000000003139471430009017", "Order dated 18.06.2020");

        SwissQrBill.Validate(bill).Should().BeEmpty();
        SwissQrBill.Payload(bill).Should().Be(string.Join("\n",
            "SPC", "0200", "1", "CH4431999123000889012",
            "S", "Robert Schneider AG", "Rue du Lac", "1268", "2501", "Biel", "CH",
            "", "", "", "", "", "", "",
            "1949.75", "CHF",
            "S", "Pia-Maria Rutschmann-Schnyder", "Grosse Marktgasse", "28", "9400", "Rorschach", "CH",
            "QRR", "210000000003139471430009017", "Order dated 18.06.2020", "EPD"));
    }

    /// <summary>The guidelines' example of a QR-bill to a plain IBAN with a creditor reference.</summary>
    [Fact]
    public void TheCreditorReferenceExampleGivesTheGuidelinesPayload()
    {
        var bill = new QrBill("CH5800791123000889012", Creditor, 199.95m, "CHF", Debtor,
            SwissQrBill.Scor, "RF18539007547034", null);

        SwissQrBill.Validate(bill).Should().BeEmpty();
        SwissQrBill.Payload(bill).Should().Be(string.Join("\n",
            "SPC", "0200", "1", "CH5800791123000889012",
            "S", "Robert Schneider AG", "Rue du Lac", "1268", "2501", "Biel", "CH",
            "", "", "", "", "", "", "",
            "199.95", "CHF",
            "S", "Pia-Maria Rutschmann-Schnyder", "Grosse Marktgasse", "28", "9400", "Rorschach", "CH",
            "SCOR", "RF18539007547034", "", "EPD"));
    }

    [Fact]
    public void ABillWithoutAmountOrDebtorLeavesThoseElementsEmpty()
    {
        var bill = new QrBill("CH5800791123000889012", Creditor, null, "EUR", null, SwissQrBill.Non, null, "Donation");

        SwissQrBill.Validate(bill).Should().BeEmpty();
        var lines = SwissQrBill.Payload(bill).Split('\n');
        lines.Should().HaveCount(31, "every element is there, the empty ones as empty lines");
        lines[18].Should().BeEmpty("no amount: the payer fills it in");
        lines[19].Should().Be("EUR");
        lines.Skip(20).Take(7).Should().OnlyContain(l => l.Length == 0, "no debtor: the payer writes in their name");
        lines[^1].Should().Be("EPD", "nothing follows the trailer without billing information");
    }

    [Fact]
    public void TheWrongReferenceForTheAccountIsRefused()
    {
        SwissQrBill.Validate(new QrBill("CH5800791123000889012", Creditor, 10m, "CHF", null, SwissQrBill.Qrr, "210000000003139471430009017", null))
            .Should().Contain(p => p.Contains("QR-IBAN"), "a QR reference needs a QR-IBAN");
        SwissQrBill.Validate(new QrBill("CH4431999123000889012", Creditor, 10m, "CHF", null, SwissQrBill.Scor, "RF18539007547034", null))
            .Should().Contain(p => p.Contains("QR-IBAN"), "a QR-IBAN takes a QR reference only");
        SwissQrBill.Validate(new QrBill("CH4431999123000889012", Creditor, 10m, "USD", null, SwissQrBill.Qrr, "210000000003139471430009017", null))
            .Should().Contain(p => p.Contains("CHF or EUR"));
        SwissQrBill.Validate(new QrBill("CH4431999123000889012", Creditor with { PostalCode = "" }, 10m, "CHF", null, SwissQrBill.Qrr, "210000000003139471430009017", null))
            .Should().Contain(p => p.Contains("postal code"), "version 2.3 takes structured addresses only, with a postal code and town");
        SwissQrBill.Validate(new QrBill("CH4431999123000889012", Creditor, 0m, "CHF", null, SwissQrBill.Qrr, "210000000003139471430009017", null))
            .Should().Contain(p => p.Contains("amount"));
    }

    [Fact]
    public void TextIsKeptToTheLatinCharacterSetAndTheGuidelinesLengths()
    {
        SwissQrBill.Clean("Zürich · Genève – 50 €").Should().Be("Zürich · Genève . 50 €", "the en dash is outside the set; accents and the euro sign are in it");
        SwissQrBill.Clean("  Two\nlines\there  ").Should().Be("Two lines here");
        SwissQrBill.Clean("✓ paid").Should().Be(". paid");
        SwissQrBill.Clean(new string('a', 80), 70).Should().HaveLength(70);

        var bill = new QrBill("CH4431999123000889012", Creditor with { Name = new string('N', 90) }, 10m, "CHF", null,
            SwissQrBill.Qrr, "210000000003139471430009017", null);
        SwissQrBill.Payload(bill).Split('\n')[5].Should().HaveLength(70, "a name is cut to 70 characters");
    }
}
