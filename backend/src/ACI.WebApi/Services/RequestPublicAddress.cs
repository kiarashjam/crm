using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Services;

/// <summary>
/// This API's public origin: Email:ApiBaseUrl when it is set to a real address, else the
/// host the request came in on, over https.
/// </summary>
/// <remarks>
/// Email:ApiBaseUrl defaults to localhost, and a deployment that never set it handed every
/// email image a localhost address (the upload "worked", and the image showed nowhere) and
/// gave the portal a callback it refuses (it reports back over https only). The request's
/// own host is the address the browser just reached, so it is the right one there. It is
/// https because a mail app and the portal need https, and a proxy that ends TLS (as Azure
/// App Service does) can pass the request on as http. On a developer's machine, reached at
/// localhost, the configured address stands.
/// </remarks>
public sealed class RequestPublicAddress : IPublicAddress
{
    private readonly IHttpContextAccessor _http;
    private readonly EmailSettings _settings;

    public RequestPublicAddress(IHttpContextAccessor http, IOptions<EmailSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public string ApiBaseUrl
    {
        get
        {
            var configured = (_settings.ApiBaseUrl ?? "").Trim().TrimEnd('/');
            if (PublicAddresses.IsPublic(configured)) return configured;
            var host = _http.HttpContext?.Request.Host;
            if (host is { HasValue: true } && !PublicAddresses.IsLoopback(host.Value.Host))
                return $"https://{host.Value.Value}";
            return configured.Length > 0 ? configured : "http://localhost:5160";
        }
    }
}
