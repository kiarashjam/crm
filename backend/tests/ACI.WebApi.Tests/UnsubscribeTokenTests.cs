using ACI.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.WebApi.Tests;

/// <summary>
/// An unsubscribe token fits in one segment of a URL path on IIS, and links already sent keep working.
/// </summary>
/// <remarks>
/// IIS on a Windows App Service answers 400 to a path segment longer than 260 characters
/// (UrlSegmentMaxLength) before the app sees it. A member's token used to pass that from an
/// address of about 30 characters, so the unsubscribe link in their marketing email could
/// never work.
/// </remarks>
public class UnsubscribeTokenTests
{
    private const int UrlSegmentMaxLength = 260;
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();

    private static UnsubscribeLinkSigner Signer(IDataProtectionProvider provider) => new(provider);

    [Theory]
    [InlineData("anna@example.ch")]
    [InlineData("jean-michel.dupont-de-la-tour@pavillon46-lausanne.ch")]
    // 62 characters: the longest address a member's token takes within one path segment.
    [InlineData("first.middle.lastnm@a-very-long-company-domain-name.example.ch")]
    public void AMembersTokenFitsInOnePathSegment_AndReadsBack(string email)
    {
        var signer = Signer(new EphemeralDataProtectionProvider());
        var token = signer.CreateMemberToken(Org, Member, email);

        token.Length.Should().BeLessThanOrEqualTo(UrlSegmentMaxLength, $"a {email.Length}-character address should give a token IIS accepts");
        token.Should().MatchRegex("^[A-Za-z0-9_-]+$", "it goes into a URL as it is");
        signer.TryReadMember(token, out var org, out var member, out var address).Should().BeTrue();
        (org, member, address).Should().Be((Org, Member, email.ToLowerInvariant()));
        signer.TryRead(token, out _, out _).Should().BeFalse("a member's token is never read as an outreach one");
    }

    [Fact]
    public void AnOutreachTokenFitsAndReadsBack_AndATamperedOneDoesNot()
    {
        var signer = Signer(new EphemeralDataProtectionProvider());
        var email = new string('a', 52) + "@example-company-domain.ch"; // 78 characters, the longest that fits
        var token = signer.CreateToken(Org, email);

        token.Length.Should().BeLessThanOrEqualTo(UrlSegmentMaxLength);
        signer.TryRead(token, out var org, out var address).Should().BeTrue();
        (org, address).Should().Be((Org, email));
        signer.TryRead(token[..^2] + (token[^2] == 'A' ? "BA" : "AA"), out _, out _).Should().BeFalse();
        signer.TryReadMember(token, out _, out _, out _).Should().BeFalse("an outreach token is never read as a member's");
    }

    [Fact]
    public void ALinkSentBeforeTheShorterTokens_StillUnsubscribes()
    {
        // Tokens in emails already sent carry the ids as text, protected the same way.
        var provider = new EphemeralDataProtectionProvider();
        var oldMember = provider.CreateProtector("ACI.Email.Unsubscribe.Member.v1").Protect($"{Org:N}|{Member:N}|anna@example.ch");
        var oldOutreach = provider.CreateProtector("ACI.Email.Unsubscribe.v1").Protect($"{Org:N}|jean@example.ch");
        var signer = Signer(provider);

        signer.TryReadMember(oldMember, out var org, out var member, out var email).Should().BeTrue();
        (org, member, email).Should().Be((Org, Member, "anna@example.ch"));
        signer.TryRead(oldOutreach, out var outreachOrg, out var outreachEmail).Should().BeTrue();
        (outreachOrg, outreachEmail).Should().Be((Org, "jean@example.ch"));
        signer.CreateMemberToken(Org, Member, "anna@example.ch").Length.Should().BeLessThan(oldMember.Length, "the new form is shorter");
    }
}
