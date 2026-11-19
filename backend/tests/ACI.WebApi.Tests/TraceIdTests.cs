using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using ACI.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.WebApi.Tests;

/// <summary>
/// An error response's traceId is the request's W3C trace id: the Operation ID Application
/// Insights shows, so one copied from an error can be searched for there.
/// </summary>
public class TraceIdTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string TraceParent = "00-" + TraceId + "-b7ad6b7169203331-01";

    private static async Task<JsonElement> HandleAsync(HttpContext context)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Production"));
        context.Response.Body = new MemoryStream();
        await handler.TryHandleAsync(context, new KeyNotFoundException("gone"), CancellationToken.None);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        return problem.RootElement.Clone();
    }

    [Fact]
    public async Task TheExceptionHandlerGivesTheW3CTraceId()
    {
        using var request = new Activity("request")
            .SetParentId(ActivityTraceId.CreateFromString(TraceId), ActivitySpanId.CreateFromString("b7ad6b7169203331"), ActivityTraceFlags.Recorded)
            .Start();

        var problem = await HandleAsync(new DefaultHttpContext { TraceIdentifier = "0HN7:00000002" });

        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("traceId").GetString().Should().Be(TraceId);
    }

    [Fact]
    public async Task WithoutAnActivityItFallsBackToTheRequestsOwnId()
    {
        Activity.Current = null;

        var problem = await HandleAsync(new DefaultHttpContext { TraceIdentifier = "0HN7:00000002" });

        problem.GetProperty("traceId").GetString().Should().Be("0HN7:00000002");
    }

    [Fact]
    public async Task MvcsOwnProblemResponsesGiveItTooTheOneTheCallerSent()
    {
        using var app = new CustomWebApplicationFactory();
        var client = app.CreateClient();

        // [ApiController]'s 400 and a controller's Problem(), both from MVC's problem details factory.
        foreach (var body in new[] { "{}", JsonSerializer.Serialize(new { message = new string('x', 20_000) }) })
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/client-errors") { Content = new StringContent(body, Encoding.UTF8, "text/plain") };
            request.Headers.Add("traceparent", TraceParent);

            var res = await client.SendAsync(request);

            res.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
            using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("traceId").GetString().Should().Be(TraceId);
        }
    }
}
