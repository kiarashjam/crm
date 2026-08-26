using ACI.WebApi.Middleware;
using FluentAssertions;

namespace ACI.WebApi.Tests;

/// <summary>
/// The signing token must not reach the log.
/// </summary>
/// <remarks>
/// This was a real leak, found by an audit that reproduced it: Serilog's request
/// logging writes the whole request path at Information level, and a contract
/// signing link carries its token in the path. So every read, signature and decline
/// wrote the raw token — the entire authorisation to sign that contract — into the
/// application log in plaintext.
///
/// The token is 32 CSPRNG bytes and stored only as a SHA-256 hash so that a leaked
/// database cannot hand anybody the ability to sign. A log line holding the raw
/// value hands over exactly that, to a wider audience, and it does not expire.
/// </remarks>
public class SecretPathRedactionTests
{
    private const string Token = "SUPERSECRETTOKEN123abcXYZ";

    [Theory]
    [InlineData("/api/public/contracts/SUPERSECRETTOKEN123abcXYZ",
                "/api/public/contracts/[redacted]")]
    [InlineData("/api/public/contracts/SUPERSECRETTOKEN123abcXYZ/sign",
                "/api/public/contracts/[redacted]/sign")]
    [InlineData("/api/public/contracts/SUPERSECRETTOKEN123abcXYZ/decline",
                "/api/public/contracts/[redacted]/decline")]
    [InlineData("/api/public/contracts/SUPERSECRETTOKEN123abcXYZ/pdf",
                "/api/public/contracts/[redacted]/pdf")]
    // Case is not part of the route's meaning, and a log leak that depended on it
    // would be worse for being intermittent.
    [InlineData("/API/PUBLIC/CONTRACTS/SUPERSECRETTOKEN123abcXYZ",
                "/API/PUBLIC/CONTRACTS/[redacted]")]
    public void RemovesTheTokenFromEveryPublicContractRoute(string path, string expected)
    {
        var redacted = SecretPathRedactionMiddleware.RedactPath(path);
        redacted.Should().Be(expected);
        redacted.Should().NotContain(Token);
    }

    [Theory]
    // The CRM's own routes carry ids, not credentials. Redacting them would throw
    // away the only thing that makes a log line useful.
    [InlineData("/api/contracts/3f9a21c0-0000-0000-0000-000000000000")]
    [InlineData("/api/contracts/3f9a21c0-0000-0000-0000-000000000000/pdf")]
    [InlineData("/api/contracts/for-lead/3f9a21c0-0000-0000-0000-000000000000")]
    [InlineData("/api/leads")]
    [InlineData("/health")]
    [InlineData("/api/public/contracts")]
    [InlineData("/api/public/contracts/")]
    public void LeavesEverythingElseAlone(string path)
    {
        SecretPathRedactionMiddleware.RedactPath(path).Should().BeNull(
            "a path with no secret in it must be logged in full");
    }

    [Fact]
    public void IsSafeToApplyTwice()
    {
        // It runs in a finally block, so it has to tolerate being reached more than
        // once without turning "[redacted]" into a second round of redaction.
        var once = SecretPathRedactionMiddleware.RedactPath($"/api/public/contracts/{Token}/sign");
        once.Should().NotBeNull();
        SecretPathRedactionMiddleware.RedactPath(once!).Should().BeNull();
    }

    [Fact]
    public void RedactsATokenContainingTheCharactersTheEncodingProduces()
    {
        // CreateRawToken is base64url: it emits '-' and '_'. A pattern that stopped
        // at a word boundary would leave half the secret in the log.
        var token = "abc-DEF_123-xyz_456";
        SecretPathRedactionMiddleware.RedactPath($"/api/public/contracts/{token}")
            .Should().Be("/api/public/contracts/[redacted]")
            .And.NotContain("DEF");
    }

    [Fact]
    public async Task TheTokenIsNotInTheRequestLogAtAll()
    {
        // The unit tests above check the redaction function. This checks the thing
        // that actually went wrong: that the log line Serilog writes for a real
        // request does not contain the token. Testing the helper alone would have
        // passed happily while the middleware was registered in the wrong place —
        // it has to sit INSIDE UseSerilogRequestLogging to rewrite the path before
        // the completion event is written.
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        string log;
        try
        {
            using var factory = new CustomWebApplicationFactory();
            using var client = factory.CreateClient();
            await client.GetAsync($"/api/public/contracts/{Token}");
            Serilog.Log.CloseAndFlush();
            log = captured.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }

        log.Should().Contain("HTTP GET", "the request must still be logged");
        log.Should().Contain("/api/public/contracts/[redacted]");
        log.Should().NotContain(Token, "the raw signing token must never reach a log");
    }

}
