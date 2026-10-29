using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The app with a mail server that keeps the links it was asked to send, so a test
/// can do what the person reading the email would: open the link.
/// </summary>
public sealed class Mailbox : CustomWebApplicationFactory
{
    private readonly List<(string Kind, string To, string Url)> _links = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            var sender = new Mock<IEmailSender>();
            sender.Setup(s => s.SendEmailVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string to, string _, string url, CancellationToken _) => Keep("verify", to, url))
                .ReturnsAsync(true);
            sender.Setup(s => s.SendOrganizationInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string to, string _, string _, string url, CancellationToken _) => Keep("invite", to, url))
                .ReturnsAsync(true);
            sender.Setup(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string to, string _, string url, CancellationToken _) => Keep("reset", to, url))
                .ReturnsAsync(true);
            services.AddSingleton(sender.Object);
        });
    }

    private void Keep(string kind, string to, string url)
    {
        lock (_links) _links.Add((kind, to, url));
    }

    /// <summary>How many links of this kind were sent to this address.</summary>
    public int Count(string kind, string to)
    {
        lock (_links) return _links.Count(l => l.Kind == kind && string.Equals(l.To, to, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The token from the latest link of this kind sent to this address.</summary>
    public string Token(string kind, string to)
    {
        string url;
        lock (_links)
        {
            url = _links.Last(l => l.Kind == kind && string.Equals(l.To, to, StringComparison.OrdinalIgnoreCase)).Url;
        }
        var query = new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri ? new Uri(url).Query : url[url.IndexOf('?')..];
        var pair = query.TrimStart('?').Split('&').Single(p => p.StartsWith("token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair["token=".Length..]);
    }
}
