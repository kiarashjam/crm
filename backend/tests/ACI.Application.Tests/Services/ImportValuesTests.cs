using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// How the importer compares records and reads cells.
/// </summary>
public class ImportValuesTests
{
    [Theory]
    [InlineData("+41 79 123 45 67")]
    [InlineData("0041 79 123 45 67")]
    [InlineData("079 123 45 67")]
    [InlineData("(079) 123-45-67")]
    public void APhoneNumberIsTheSameNumberHoweverItIsWritten(string written)
    {
        ImportValues.PhoneKey(written).Should().Be("791234567");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12 34")]
    public void TooFewDigitsMatchNothing(string? written)
    {
        ImportValues.PhoneKey(written).Should().BeEmpty("a short number would match strangers");
    }

    [Theory]
    [InlineData("https://www.Acme.ch/about", "acme.ch")]
    [InlineData("acme.ch", "acme.ch")]
    [InlineData("WWW.ACME.CH", "acme.ch")]
    [InlineData("http://acme.ch:8080?x=1", "acme.ch")]
    public void ADomainIsReadOutOfAWebAddress(string written, string domain)
    {
        ImportValues.DomainKey(written).Should().Be(domain);
    }

    [Theory]
    [InlineData("ja", true)]
    [InlineData("Oui", true)]
    [InlineData("x", true)]
    [InlineData("nein", false)]
    [InlineData("0", false)]
    [InlineData("maybe", null)]
    public void YesAndNoAreReadInTheLanguagesPeopleWriteThem(string written, bool? expected)
    {
        ImportValues.YesNo(written).Should().Be(expected);
    }

    private static CustomFieldDefinition Field(string type, params string[] options) => new()
    {
        Key = "f",
        Label = "Field",
        Type = type,
        OptionsJson = options.Length == 0 ? null : JsonSerializer.Serialize(options),
    };

    [Theory]
    [InlineData("1234.5", 1234.5)]
    [InlineData("1'234.50", 1234.50)]
    [InlineData("1 234,50", 1234.50)]
    [InlineData("-12", -12)]
    public void ANumberIsReadAsSpreadsheetsWriteIt(string written, decimal expected)
    {
        var (value, error) = ImportValues.Custom(Field(CustomFieldTypes.Number), written);

        error.Should().BeNull();
        value!.Value.GetDecimal().Should().Be(expected);
    }

    [Theory]
    [InlineData("1000-2000")]
    [InlineData("about 5")]
    [InlineData("CHF 50")]
    public void TextWithANumberInItIsNotANumber(string written)
    {
        ImportValues.Custom(Field(CustomFieldTypes.Number), written).Error.Should().Be("Field must be a number");
    }

    [Theory]
    [InlineData("2025-12-31")]
    [InlineData("31.12.2025")]
    [InlineData("31/12/2025")]
    [InlineData("12/31/2025")]
    [InlineData("31.12.2025 09:30")]
    public void ADateIsReadWhenOnlyOneReadingIsPossible(string written)
    {
        var (value, error) = ImportValues.Custom(Field(CustomFieldTypes.Date), written);

        error.Should().BeNull();
        value!.Value.GetString().Should().Be("2025-12-31");
    }

    [Theory]
    [InlineData("03/04/2025")]
    [InlineData("31.02.2025")]
    [InlineData("soon")]
    // A typo with a fifth digit in the year is a bad cell, not a failed import.
    [InlineData("31/12/20255")]
    public void ADateThatCouldBeTwoDaysOrNoneIsRefused(string written)
    {
        ImportValues.Custom(Field(CustomFieldTypes.Date), written).Error.Should().Contain("must be a date");
    }

    [Fact]
    public void AnOptionIsStoredAsTheWorkspaceWritesIt()
    {
        var (value, _) = ImportValues.Custom(Field(CustomFieldTypes.Select, "Gold", "Silver"), "GOLD");

        value!.Value.GetString().Should().Be("Gold");
        ImportValues.Custom(Field(CustomFieldTypes.Select, "Gold", "Silver"), "Platinum").Error
            .Should().Be("Field must be one of: Gold, Silver");
    }

    [Fact]
    public void SeveralOptionsAreSplitOnCommasOrSemicolonsAndStoredAsTheWorkspaceWritesThem()
    {
        var sports = Field(CustomFieldTypes.MultiSelect, "Golf", "Tennis", "Padel");

        var (value, error) = ImportValues.Custom(sports, "golf; Tennis, golf");
        error.Should().BeNull();
        value!.Value.EnumerateArray().Select(v => v.GetString()).Should().Equal("Golf", "Tennis");
        ImportValues.Custom(sports, "Golf, Squash").Error.Should().Be("Field: \"Squash\" is not one of Golf, Tennis, Padel");
    }

    [Theory]
    [InlineData("CHF 1'200.50", 1200.50, "CHF")]
    [InlineData("1200 EUR", 1200, "EUR")]
    [InlineData("1 200,50", 1200.50, "CHF")]
    public void AnAmountTakesItsCurrencyFromTheCellOrElseTheField(string written, decimal amount, string currency)
    {
        var spend = Field(CustomFieldTypes.Money);
        spend.Currency = "CHF";

        var (value, error) = ImportValues.Custom(spend, written);

        error.Should().BeNull();
        value!.Value.GetProperty("amount").GetDecimal().Should().Be(amount);
        value.Value.GetProperty("currency").GetString().Should().Be(currency);
        ImportValues.Custom(spend, "about 5 francs").Error.Should().StartWith("Field must be an amount");
    }

    [Fact]
    public void ALinkIsTheIdOfTheRecordItPointsAt()
    {
        var referrer = Field(CustomFieldTypes.Link);
        referrer.LinkEntityType = RecordTypes.Contact;
        var id = Guid.NewGuid();

        ImportValues.Custom(referrer, id.ToString()).Value!.Value.GetProperty("id").GetGuid().Should().Be(id);
        ImportValues.Custom(referrer, "Anna Keller").Error.Should().Be("Field must point at a record");
    }

    [Fact]
    public void ATickIsAYesOrANo()
    {
        ImportValues.Custom(Field(CustomFieldTypes.Checkbox), "ja").Value!.Value.GetBoolean().Should().BeTrue();
        ImportValues.Custom(Field(CustomFieldTypes.Checkbox), "perhaps").Error.Should().Be("Field must be yes or no");
    }

    [Fact]
    public void HeadersInGermanAndFrenchSuggestTheirFields()
    {
        var fields = ImportFields.For(RecordTypes.Lead, Array.Empty<CustomFieldDefinition>());

        ImportFields.Suggest(new[] { "Vorname", "Nachname", "E-Mail-Adresse", "Téléphone", "Société", "Bemerkungen" }, fields)
            .Should().Equal("firstName", "lastName", "email", "phone", "company", "description");
        ImportFields.Suggest(new[] { "Prénom", "Nom", "Courriel" }, fields).Should().Equal("firstName", "lastName", "email");
    }

    [Fact]
    public void OnlyAWholeHeaderSuggestsAFieldAndEachFieldOnce()
    {
        var fields = ImportFields.For(RecordTypes.Lead, Array.Empty<CustomFieldDefinition>());

        ImportFields.Suggest(new[] { "Mailing list", "Email", "E-mail", "Phone" }, fields)
            .Should().Equal(null, "email", null, "phone");
    }

    [Fact]
    public void ACustomFieldIsSuggestedByItsLabel()
    {
        var tier = new CustomFieldDefinition { Key = "membership_tier", Label = "Membership tier", Type = CustomFieldTypes.Text };
        var fields = ImportFields.For(RecordTypes.Contact, new[] { tier });

        ImportFields.Suggest(new[] { "Membership Tier" }, fields).Should().Equal("custom:membership_tier");
    }
}
