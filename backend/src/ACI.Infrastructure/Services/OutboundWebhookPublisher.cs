using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>Posts lead and deal events to the workspace's own URL. A failure never fails the save.</summary>
public sealed class OutboundWebhookPublisher : IOutboundWebhookPublisher
{
    private readonly IOrganizationRepository _organizations;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<OutboundWebhookPublisher> _logger;

    public OutboundWebhookPublisher(IOrganizationRepository organizations, IHttpClientFactory http, ILogger<OutboundWebhookPublisher> logger)
    {
        _organizations = organizations;
        _http = http;
        _logger = logger;
    }

    public async Task PublishAsync(Guid? organizationId, string eventName, object payload, CancellationToken ct = default)
    {
        if (organizationId == null) return;
        try
        {
            var org = await _organizations.GetByIdAsync(organizationId.Value, ct);
            if (org == null || string.IsNullOrWhiteSpace(org.OutboundWebhookUrl)) return;
            if (!Uri.TryCreate(org.OutboundWebhookUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !PublicAddress.LooksPublic(uri))
            {
                _logger.LogWarning("Outbound webhook for {OrganizationId} is not an https URL", organizationId);
                return;
            }

            var body = JsonSerializer.Serialize(new { eventName, payload, sentAtUtc = DateTime.UtcNow });
            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrWhiteSpace(org.OutboundWebhookSecret))
            {
                var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(org.OutboundWebhookSecret), Encoding.UTF8.GetBytes(body));
                request.Headers.Add("X-Cadence-Signature", Convert.ToHexString(hash).ToLowerInvariant());
            }

            var client = _http.CreateClient(nameof(OutboundWebhookPublisher));
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Outbound webhook {Event} for {OrganizationId} returned {Status}", eventName, organizationId, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outbound webhook {Event} for {OrganizationId} was not delivered", eventName, organizationId);
        }
    }

    /// <summary>
    /// The connection handler for outbound webhooks. It connects only to public
    /// addresses, checked on the address actually dialled after DNS, so a name that
    /// resolves to this server's own network is refused. Redirects are not followed:
    /// one could lead anywhere.
    /// </summary>
    public static SocketsHttpHandler PublicOnlyHandler() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = async (context, ct) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0 || !addresses.All(PublicAddress.IsPublic))
                throw new HttpRequestException($"Outbound webhooks only go to public addresses; {host} is not one.");
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}
