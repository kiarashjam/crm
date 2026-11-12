using ACI.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.WebApi.Tests;

/// <summary>
/// Pay link tokens: what a link says (whose account, what it pays, until when) comes back as
/// it was made, nobody can change it or use it past its date, and it stays short.
/// </summary>
/// <remarks>
/// Short matters twice. The token is the last part of the link emailed to the member, and
/// mail programs stop making a link clickable somewhere past 2,000 characters. And it used to
/// travel in the API's path, where IIS (the App Service the API runs on) refuses a path
/// segment longer than 260 characters with a 400, before the app ever sees the request.
/// </remarks>
public class PayLinkSignerTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-10T09:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PayLinkClaim Claim(string purpose, int charges, bool autoPay = false) => new(
        Guid.NewGuid(), Guid.NewGuid(), purpose, Enumerable.Range(0, charges).Select(_ => Guid.NewGuid()).ToList(), autoPay,
        new DateTime(2027, 1, 9, 9, 0, 0, DateTimeKind.Utc));

    [Theory]
    [InlineData("account", 0, false)]
    [InlineData("setup", 0, true)]
    [InlineData("charges", 3, false)]
    [InlineData("charges", 50, false)]
    public void ALinkSaysWhatItWasMadeFor(string purpose, int charges, bool autoPay)
    {
        var signer = new PayLinkSigner(new EphemeralDataProtectionProvider(), new Clock());
        var claim = Claim(purpose, charges, autoPay);

        signer.TryRead(signer.Create(claim), out var read).Should().BeTrue();

        read!.OrganizationId.Should().Be(claim.OrganizationId);
        read.MemberId.Should().Be(claim.MemberId);
        read.Purpose.Should().Be(purpose);
        read.ChargeIds.Should().Equal(claim.ChargeIds);
        read.AutoPay.Should().Be(autoPay);
        read.ExpiresAtUtc.Should().Be(claim.ExpiresAtUtc);
    }

    [Fact]
    public void ALinkStaysShort()
    {
        var signer = new PayLinkSigner(new EphemeralDataProtectionProvider(), new Clock());

        signer.Create(Claim("account", 0)).Length.Should().BeLessThan(260, "a link for what is owed is a short link");
        signer.Create(Claim("setup", 0, autoPay: true)).Length.Should().BeLessThan(260);
        signer.Create(Claim("charges", 50)).Length.Should().BeLessThan(1900,
            "a link for the most charges staff may tick still fits in a link a mail program makes clickable");
    }

    [Fact]
    public void AChangedOrExpiredLinkIsNotRead()
    {
        var clock = new Clock();
        var signer = new PayLinkSigner(new EphemeralDataProtectionProvider(), clock);
        var token = signer.Create(Claim("account", 0));

        signer.TryRead(token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA"), out _).Should().BeFalse("a changed token");
        signer.TryRead("not-a-token", out _).Should().BeFalse();
        signer.TryRead(new PayLinkSigner(new EphemeralDataProtectionProvider(), clock).Create(Claim("account", 0)), out _)
            .Should().BeFalse("a token made with another key ring");
        clock.Now = new DateTimeOffset(2027, 1, 9, 9, 0, 1, TimeSpan.Zero);
        signer.TryRead(token, out _).Should().BeFalse("a link past its date");
    }
}
