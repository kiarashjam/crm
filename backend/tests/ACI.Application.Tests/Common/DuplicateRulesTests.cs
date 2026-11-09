using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Which records look like one, as the Duplicates page, the list banners and the contact
/// page's "Is this the same person?" all decide it.
/// </summary>
/// <remarks>
/// A contact's email is unique in its workspace, so the duplicate that actually happens is
/// one person under two addresses. The browser's rules ruled out any two contacts with
/// different emails, and so never found a duplicate on a real server; these cases are
/// written the way server data looks, every email different.
/// </remarks>
public class DuplicateRulesTests
{
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Globex = Guid.NewGuid();

    private static DuplicateRules.Person P(string name, string email, string? phone = null, Guid? company = null) =>
        DuplicateRules.PersonOf(Guid.NewGuid(), email, phone, name, company);

    [Theory]
    [InlineData("+41 79 123 45 67", "079 123 45 67")]
    [InlineData("0041 79 123 45 67", "+41791234567")]
    [InlineData("(079) 123-45-67", "079.123.45.67")]
    public void APhoneIsOneNumberHoweverItIsWritten(string a, string b) =>
        MatchKeys.Phone(a).Should().Be(MatchKeys.Phone(b)).And.Be("791234567");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("ext. 22")]
    public void AShortOrMissingPhoneSaysNothing(string? phone) => MatchKeys.Phone(phone).Should().BeNull();

    [Fact]
    public void ANameIsTheSameWithOrWithoutCaseAccentsAndSpaces()
    {
        MatchKeys.Name("  Zoë   Martin ").Should().Be("zoe martin");
        MatchKeys.Name("ZOE MARTIN").Should().Be("zoe martin");
        MatchKeys.Name("Müller-Lüdenscheidt").Should().Be("muller-ludenscheidt");
        MatchKeys.Name("A").Should().BeNull("one letter is no name to match on");
    }

    [Fact]
    public void ADomainIsTheSameWithOrWithoutItsWebAddress()
    {
        MatchKeys.Domain("https://www.Acme.ch/about").Should().Be("acme.ch");
        MatchKeys.Domain("acme.ch").Should().Be("acme.ch");
        MatchKeys.Domain("http://acme.ch:8080?x=1").Should().Be("acme.ch");
        MatchKeys.Domain("  ").Should().BeNull();
    }

    [Fact]
    public void OnePersonUnderTwoEmailsAndTwoPhoneFormatsIsFound()
    {
        var a = P("Marc Dubois", "marc@gmail.com", "+41 79 123 45 67");
        var b = P("Marc Dubois", "m.dubois@firm.ch", "079 123 45 67");

        var scan = DuplicateRules.Contacts([a, b], []);

        scan.Groups.Should().ContainSingle();
        scan.Groups[0].Ids.Should().BeEquivalentTo(new[] { a.Id, b.Id });
        scan.Groups[0].Reason.Should().Be("Same phone · Same name");
        DuplicateRules.Matches(a, b).Should().Equal("Same phone", "Same name");
    }

    [Fact]
    public void TheSameNameIsADuplicateUnlessThePhonesOrCompaniesDisagree()
    {
        var plain = P("Anna Keller", "anna@one.ch");
        var withPhone = P("Anna Keller", "anna@two.ch", "+41 79 000 11 22");
        DuplicateRules.Matches(plain, withPhone).Should().Equal(new[] { "Same name" }, "a phone only one of them has rules nothing out");

        var otherPhone = P("Anna Keller", "anna@three.ch", "+41 78 999 88 77");
        DuplicateRules.Matches(withPhone, otherPhone).Should().BeEmpty("two different numbers: two different people");

        var atAcme = P("Anna Keller", "anna@acme.ch", company: Acme);
        var atGlobex = P("Anna Keller", "anna@globex.ch", company: Globex);
        DuplicateRules.Matches(atAcme, atGlobex).Should().BeEmpty("at two different companies");
        DuplicateRules.Matches(atAcme, plain).Should().Equal("Same name");
    }

    [Fact]
    public void TheSameEmailAndTheSamePhoneAreDuplicatesWhateverTheName()
    {
        DuplicateRules.Matches(P("Ann Keller", " Anna@Example.ch"), P("Anna Keller-Huber", "anna@example.ch")).Should().Equal("Same email");
        DuplicateRules.Matches(P("Ann", "a@x.ch", "079 123 45 67"), P("Anna K.", "b@x.ch", "+41791234567")).Should().Equal("Same phone");
    }

    [Fact]
    public void RecordsLinkedThroughOneAnotherFormOneGroup()
    {
        var a = P("Lea Meier", "lea@one.ch", "079 111 22 33");
        var b = P("L. Meier", "lea@two.ch", "+41 79 111 22 33");
        var c = P("L. Meier", "lea@three.ch");

        var scan = DuplicateRules.Contacts([a, b, c], []);

        scan.Groups.Should().ContainSingle().Which.Ids.Should().BeEquivalentTo(new[] { a.Id, b.Id, c.Id });
        scan.Groups[0].Key.Should().Be(DuplicateRules.KeyOf([c.Id, a.Id, b.Id]), "the key is the same however the records come");
    }

    [Fact]
    public void APairMarkedNotTheSameIsNotOfferedAgain_AndIsListedAsHidden()
    {
        var father = P("Alex Meier", "alex@meier.ch", "044 123 45 67");
        var son = P("Alex Meier", "alex.junior@meier.ch", "+41 44 123 45 67");

        var scan = DuplicateRules.Contacts([father, son], [(son.Id, father.Id)]);

        scan.Groups.Should().BeEmpty();
        scan.NotSame.Should().ContainSingle().Which.Ids.Should().BeEquivalentTo(new[] { father.Id, son.Id });
    }

    [Fact]
    public void ANewLookalikeOfAPairMarkedNotTheSameIsStillOffered()
    {
        var father = P("Alex Meier", "alex@meier.ch", "044 123 45 67");
        var son = P("Alex Meier", "alex.junior@meier.ch", "+41 44 123 45 67");
        var again = P("Alex Meier", "a.meier@bank.ch", "044 123 45 67");

        var scan = DuplicateRules.Contacts([father, son, again], [(father.Id, son.Id)]);

        scan.Groups.Should().ContainSingle().Which.Ids.Should().BeEquivalentTo(new[] { father.Id, son.Id, again.Id },
            "the third record still looks like both of them");
        scan.NotSame.Should().BeEmpty("the group the decision was about is still shown whole");
    }

    [Fact]
    public void ANumberSharedByMoreThanFiftyPeopleIsAnOfficeLineNotADuplicate()
    {
        var people = Enumerable.Range(0, DuplicateRules.MaxShared + 1).Select(i => P($"Person {i}", $"p{i}@hotel.ch", "+41 22 700 00 00")).ToList();
        DuplicateRules.Contacts(people, []).Groups.Should().BeEmpty();
        DuplicateRules.Contacts(people.Take(3), []).Groups.Should().ContainSingle();
    }

    [Fact]
    public void CompaniesAreTheSameByDomain_OrByNameWhenTheDomainsDoNotDisagree()
    {
        var acme = new DuplicateRules.Firm(Guid.NewGuid(), MatchKeys.Domain("https://www.acme.ch"), MatchKeys.Name("Acme AG"));
        var acmeAgain = new DuplicateRules.Firm(Guid.NewGuid(), MatchKeys.Domain("acme.ch"), MatchKeys.Name("ACME"));
        var acmeNoSite = new DuplicateRules.Firm(Guid.NewGuid(), null, MatchKeys.Name("Acme AG"));
        var otherAcme = new DuplicateRules.Firm(Guid.NewGuid(), MatchKeys.Domain("acme.com"), MatchKeys.Name("Acme AG"));

        var scan = DuplicateRules.Companies([acme, acmeAgain, acmeNoSite], []);
        scan.Groups.Should().ContainSingle().Which.Ids.Should().HaveCount(3);
        scan.Groups[0].Reason.Should().Be("Same domain · Same name");

        DuplicateRules.Companies([acme, otherAcme], []).Groups.Should().BeEmpty("the same name on two different web domains");
    }
}
