using System.Net;
using System.Net.Sockets;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Posts one outbound webhook attempt to the workspace's own URL. The outbox decides
/// when, and what happens when it does not go through.
/// </summary>
public sealed class WebhookTransport : IWebhookTransport
{
    /// <summary>The named HttpClient, registered with <see cref="PublicOnlyHandler"/>.</summary>
    public const string ClientName = "OutboundWebhooks";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebhookTransport> _logger;

    public WebhookTransport(IHttpClientFactory http, ILogger<WebhookTransport> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
            // Some receivers behind a firewall refuse a request that does not say what it is.
            request.Headers.TryAddWithoutValidation("User-Agent", "Cadence-Webhooks/1.0");

            var client = _http.CreateClient(ClientName);
            // Headers only: the status is all that matters, and a receiver that answered
            // with a huge body would otherwise be read into memory whole.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return new WebhookPostResult((int)response.StatusCode, $"The receiver answered {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
            return new WebhookPostResult((int)response.StatusCode, null, await StartOfAsync(response, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Outbound webhook to {Host} was not delivered this time", url.Host);
            var why = ex is TaskCanceledException ? "The receiver did not answer in time." : $"The receiver could not be reached: {ex.Message}";
            return new WebhookPostResult(null, why);
        }
    }

    /// <summary>The first few kilobytes of an answer, never more: a receiver cannot fill memory with its reply.</summary>
    private static async Task<string?> StartOfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int Limit = 8 * 1024;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[Limit];
            var read = 0;
            while (read < Limit)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read, Limit - read), ct);
                if (n == 0) break;
                read += n;
            }
            return read == 0 ? null : Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
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
