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

    private OutreachMail Mail(string frontendBaseUrl = "https://crm.example.ch/") => new(
        new Mock<IOrganizationRepository>().Object, new Mock<IUserRepository>().Object, _links.Object,
        Options.Create(new EmailSettings { FrontendBaseUrl = frontendBaseUrl }), NullLogger<OutreachMail>.Instance);

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
