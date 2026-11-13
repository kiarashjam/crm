using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// A club with its member portal turned on, and members signing in to it the way a person does:
/// asking for a link, reading it from the email the outbox queued, and using it.
/// </summary>
public static partial class PortalWorld
{
    public static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused with {(int)status} — got {(int)res.StatusCode}: {body}");
        return body;
    }

    /// <summary>A club's owner, signed in, in their club.</summary>
    public static async Task<(HttpClient Staff, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email, string name = "Pavillon46")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, name);
        client.In(org);
        return (client, userId, org);
    }

    /// <summary>Turns the club's portal on (as someone who manages its settings) and answers its address key.</summary>
    public static async Task<string> OpenPortalAsync(HttpClient staff, int? cancelHours = null)
    {
        var settings = await Ok(await staff.PutAsync("/api/member-portal/settings", Body(new { enabled = true, memberCancelHours = cancelHours })), "turn the portal on");
        settings.GetProperty("enabled").GetBoolean().Should().BeTrue();
        return KeyOf(settings.GetProperty("portalUrl").GetString()!);
    }

    /// <summary>The key at the end of a portal address: https://app/member-portal/{key}.</summary>
    public static string KeyOf(string portalUrl) => Uri.UnescapeDataString(portalUrl[(portalUrl.LastIndexOf('/') + 1)..]);

    public static async Task<Guid> MemberAsync(HttpClient staff, string first, string? email = null, string status = "Active", string tier = "Gold") =>
        (await Ok(await staff.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Muster", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier, status,
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>The sign-in links in the latest portal email to this address, in the order the email gives them.</summary>
    public static async Task<IReadOnlyList<string>> LinkTokensAsync(CustomWebApplicationFactory factory, string email)
    {
        var message = await factory.Services.Read(db => db.OutboundMessages
            .Where(m => m.Source == OutboundSources.Portal && m.To == email)
            .OrderByDescending(m => m.CreatedAtUtc)
            .FirstOrDefault());
        message.Should().NotBeNull($"a portal email should have been queued for {email}");
        return TokenPattern().Matches(message!.Body ?? "").Select(m => Uri.UnescapeDataString(m.Groups[1].Value)).ToList();
    }

    public static async Task<string> LinkTokenAsync(CustomWebApplicationFactory factory, string email) =>
        (await LinkTokensAsync(factory, email)).Single();

    /// <summary>Asks for a link at the portal's sign-in page, as a visitor with no account does.</summary>
    public static Task<HttpResponseMessage> AskForLinkAsync(CustomWebApplicationFactory factory, string key, string email) =>
        factory.CreateClient().PostAsync($"/api/public/portal/{key}/sign-in-link", Body(new { email }));

    /// <summary>Uses a sign-in link; the answer carries the member token on success.</summary>
    public static Task<HttpResponseMessage> RedeemAsync(CustomWebApplicationFactory factory, string key, string token) =>
        factory.CreateClient().PostAsync($"/api/public/portal/{key}/sign-in", Body(new { token }));

    /// <summary>A client signed in as the member at this address, by an emailed link.</summary>
    public static async Task<HttpClient> SignInMemberAsync(CustomWebApplicationFactory factory, string key, string email)
    {
        (await AskForLinkAsync(factory, key, email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var token = await LinkTokenAsync(factory, email);
        var session = await Ok(await RedeemAsync(factory, key, token), $"sign in as {email}");
        return MemberClient(factory, session.GetProperty("token").GetString()!);
    }

    public static HttpClient MemberClient(CustomWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [GeneratedRegex(@"#token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex TokenPattern();
}
