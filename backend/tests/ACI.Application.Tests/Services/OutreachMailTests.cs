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
}
