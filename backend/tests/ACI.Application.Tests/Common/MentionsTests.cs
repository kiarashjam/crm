using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>Who a comment or note mentions, from what people actually type.</summary>
public class MentionsTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid AnnaLena = Guid.NewGuid();
    private static readonly Guid Luca = Guid.NewGuid();
    private static readonly Guid Marc = Guid.NewGuid();

    private static readonly Mentions.Person[] Team =
    [
        new(Anna, "Anna Keller", "anna@pavillon46.ch"),
        new(AnnaLena, "Anna-Lena Brun", "al.brun@pavillon46.ch"),
        new(Luca, "Luca Rossi", "luca@pavillon46.ch"),
        new(Marc, "Marc  Weber", "marc.weber@pavillon46.ch"),
    ];

    [Theory]
    [InlineData("@Anna Keller can you call them?")]
    [InlineData("Thanks @anna keller.")]
    [InlineData("cc @Anna")]
    [InlineData("(@anna)")]
    [InlineData("@anna@pavillon46.ch please")]
    public void ATeammateIsFoundByTheirNameFirstNameOrAddress(string text)
    {
        Mentions.Find(text, Team).Should().Equal(Anna);
    }

    [Fact]
    public void AFirstNameThatIsPartOfALongerOneIsNotIt()
    {
        Mentions.Find("@Anna-Lena has the list", Team).Should().Equal(AnnaLena);
        Mentions.Find("@Annabel will know", Team).Should().BeEmpty();
    }

    [Fact]
    public void TheLongerNameWinsWhereTwoFit()
    {
        var team = Team.Append(new Mentions.Person(Guid.NewGuid(), "Anna", "anna.s@pavillon46.ch")).ToArray();

        Mentions.Find("@Anna Keller please", team).Should().Equal(Anna);
        Mentions.Find("@Anna please", team).Should().NotContain(Anna, "\"Anna\" is the other Anna's whole name");
    }

    [Fact]
    public void AFirstNameTwoPeopleShareNamesNobody()
    {
        var team = Team.Append(new Mentions.Person(Guid.NewGuid(), "Luca Bianchi", "lb@pavillon46.ch")).ToArray();

        Mentions.Find("@Luca can you?", team).Should().BeEmpty("telling either Luca would be a guess");
        Mentions.Find("@Luca Rossi can you?", team).Should().Equal(Luca);
    }

    [Fact]
    public void AnEmailAddressInTheTextIsNotAMention()
    {
        Mentions.Find("Write to bob@anna.ch or luca@pavillon46.ch", Team).Should().BeEmpty("\"anna\" after bob@ is a mail server, not Anna");
    }

    [Fact]
    public void SpacesInANameAreReadAsOne()
    {
        Mentions.Find("@Marc Weber the offer is out", Team).Should().Equal(Marc);
        Mentions.Find("@marc.weber see above", Team).Should().Equal(Marc);
    }

    [Fact]
    public void EachPersonOnceInTheOrderTheyComeUp()
    {
        Mentions.Find("@Luca and @Anna, then @Luca again", Team).Should().Equal(Luca, Anna);
    }

    [Fact]
    public void NoAtNoMention()
    {
        Mentions.Find("Anna Keller will call them", Team).Should().BeEmpty();
        Mentions.Find(null, Team).Should().BeEmpty();
    }
}
