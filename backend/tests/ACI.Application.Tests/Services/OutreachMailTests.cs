using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The foot of every email a workspace sends: how to stop it. There was nothing
/// there, so the only way out of a sequence was to reply and hope.
/// </summary>
public class OutreachMailTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private readonly Mock<IUnsubscribeLinks> _links = new();

    public OutreachMailTests() =>
        _links.Setup(l => l.CreateToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns((Guid _, string email) => $"tok-{email.Split('@')[0]}");

    private OutreachMail Mail(string frontendBaseUrl = "https://crm.example.ch/", string apiBaseUrl = "http://localhost:5160") => new(
        new Mock<IOrganizationRepository>().Object, new Mock<IUserRepository>().Object, _links.Object,
        Options.Create(new EmailSettings { FrontendBaseUrl = frontendBaseUrl, ApiBaseUrl = apiBaseUrl }), NullLogger<OutreachMail>.Instance);

    [Fact]
    public void TheTrackingImageIsOnlyAddedWhereItCanWork()
    {
        Mail(apiBaseUrl: "http://localhost:5160").TrackingUrl(Guid.NewGuid()).Should().BeNull("a recipient can never fetch localhost");
        Mail(apiBaseUrl: "http://api.example.ch").TrackingUrl(Guid.NewGuid()).Should().BeNull("plain http is blocked by most mail clients");
        var lead = Guid.NewGuid();
        Mail(apiBaseUrl: "https://api.example.ch/").TrackingUrl(lead).Should().Be($"https://api.example.ch/api/public/track/{lead}");

        ACI.Application.Common.PlainTextEmail.ToHtml("Hi", "https://api.example.ch/api/public/track/x").Should().Contain("<img src=\"https://api.example.ch/api/public/track/x\"");
        ACI.Application.Common.PlainTextEmail.ToHtml("Hi", null).Should().NotContain("<img");
    }

    [Fact]
    public async Task TheSenderNamesThePersonAndUsesTheWorkspacesAddressOnlyWhenItsDomainIsVerified()
    {
        var owner = Guid.NewGuid();
        var organizations = new Mock<IOrganizationRepository>();
        organizations.Setup(o => o.GetByIdAsync(Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ACI.Domain.Entities.Organization { Id = Org, Name = "Acme", OwnerUserId = owner, SendingAddress = "Hello@Acme.Example" });
        organizations.Setup(o => o.GetMemberRoleAsync(owner, Org, It.IsAny<CancellationToken>())).ReturnsAsync(ACI.Domain.Enums.OrgMemberRole.Owner);
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ACI.Domain.Entities.User { Id = owner, Email = "olga@acme.example", Name = "Olga" });
        OutreachMail With(string verified) => new(organizations.Object, users.Object, _links.Object,
            Options.Create(new EmailSettings { VerifiedSendingDomains = verified }), NullLogger<OutreachMail>.Instance);

        var unverified = await With("").SenderAsync(Org, null);
        unverified.Should().Be(new OutreachSender("Acme", "olga@acme.example", "Olga", null));
        (await With($"club.example={Guid.NewGuid()}, ACME.example={Org}").SenderAsync(Org, null)).FromAddress.Should().Be("Hello@Acme.Example");
        (await With($"acme.example={Guid.NewGuid()}").SenderAsync(Org, null)).FromAddress
            .Should().BeNull("the domain was verified for another workspace");
        (await With("acme.example").SenderAsync(Org, null)).FromAddress
            .Should().BeNull("a domain listed for no workspace would let every workspace send as it");
    }

    [Fact]
    public void OneRecipientGetsOneLink_InBothParts_AndForTheHeader()
    {
        var body = Mail().Body(Org, "Club Nautique", "Hi Jean", ["jean@example.ch"], "https://api.example.ch/api/public/track/x");

        const string url = "https://crm.example.ch/unsubscribe/tok-jean";
        body.Text.Should().Be($"Hi Jean\n\n--\nDon't want emails from Club Nautique? Unsubscribe: {url}\n");
        body.Html.Should().Contain($"<a href=\"{url}\">Unsubscribe</a>").And.Contain("Don&#39;t want emails from Club Nautique?");
        body.Html.Should().Contain("<img src=\"https://api.example.ch/api/public/track/x\"", "the tracking image still goes in the HTML part");
        body.UnsubscribeUrl.Should().Be(url);
        _links.Verify(l => l.CreateToken(Org, "jean@example.ch"));
    }

    [Fact]
    public void SeveralRecipientsEachGetTheirOwnNamedLink()
    {
        var body = Mail().Body(Org, "Club Nautique", "Hi", ["jean@example.ch", "anne@example.ch", "JEAN@example.ch"]);

        body.Text.Should().Contain("jean@example.ch: https://crm.example.ch/unsubscribe/tok-jean")
            .And.Contain("anne@example.ch: https://crm.example.ch/unsubscribe/tok-anne");
        _links.Verify(l => l.CreateToken(Org, It.IsAny<string>()), Times.Exactly(2));
        body.UnsubscribeUrl.Should().BeNull("one header cannot speak for several people");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://crm.example.ch")]
    public void WithoutAPublicAddressThereIsNoLinkToGive(string frontendBaseUrl)
    {
        var body = Mail(frontendBaseUrl).Body(Org, "Club Nautique", "Hi", ["jean@example.ch"]);

        body.Text.Should().Be("Hi");
        body.UnsubscribeUrl.Should().BeNull();
    }

    [Fact]
    public void TheWorkspaceNameIsEscapedInTheHtmlPart()
    {
        var body = Mail().Body(Org, "<b>Club</b>", "Hi", ["jean@example.ch"]);

        body.Html.Should().Contain("&lt;b&gt;Club&lt;/b&gt;").And.NotContain("<b>Club</b>");
    }

    [Fact]
    public void AMembersDesignedMarketingEmail_KeepsItsDesign_WithTheLinkThatWithdrawsConsentAtTheFootOfIt()
    {
        var member = Guid.NewGuid();
        _links.Setup(l => l.CreateMemberToken(Org, member, "anna@example.ch")).Returns("m.tok-anna");
        var design = ACI.Application.Common.ClubEmails.Letter("Pavillon 46", "fr", "La soirée d’hiver", "Bonjour Anna,\n\nVenez nombreux.", title: true);
        var (html, text) = ACI.Application.Common.EmailLayout.Render(design, "La soirée d’hiver");

        var marketing = Mail().MemberBody(Org, "Pavillon 46", text, member, "anna@example.ch", marketing: true, "fr", html);

        const string url = "https://crm.example.ch/unsubscribe/m.tok-anna";
        marketing.Text.Should().StartWith(text).And.EndWith($"Vous ne souhaitez plus recevoir les nouvelles et offres de Pavillon 46 par e-mail ? Se désinscrire: {url}\n");
        marketing.Html.Should().Contain("Venez nombreux.").And.Contain("#2c5545", "it is still the club's design");
        marketing.Html.Should().Contain($"<a href=\"{url}\"").And.Contain(">Se d&#233;sinscrire</a>");
        marketing.Html.IndexOf(url, StringComparison.Ordinal).Should().BeLessThan(marketing.Html.IndexOf("</body>", StringComparison.Ordinal))
            .And.BeGreaterThan(marketing.Html.IndexOf("Vous recevez cet e-mail en tant que membre", StringComparison.Ordinal), "under the design's own footer");
        marketing.UnsubscribeUrl.Should().Be(url);

        var service = Mail().MemberBody(Org, "Pavillon 46", text, member, "anna@example.ch", marketing: false, "fr", html);
        service.Html.Should().Be(html, "a service message carries no link: the member's choices do not stop it");
        service.Text.Should().Be(text);
    }

    [Fact]
    public void ALinkAtTheFootOfAnyOtherDesign_GoesUnderIt_AsBefore()
    {
        const string letter = "<!doctype html><html><body><div class=\"letter\">Founders' letter</div></body></html>";
        var html = ACI.Application.Common.PlainTextEmail.WithFooter(letter, null, "Acme", [new ACI.Application.Common.UnsubscribeLink("a@x.ch", "https://x.ch/u/1")]);

        html.Should().Contain("<div style=\"text-align:center;padding:0 16px 24px\">").And.Contain("<a href=\"https://x.ch/u/1\">Unsubscribe</a>");
    }
}
