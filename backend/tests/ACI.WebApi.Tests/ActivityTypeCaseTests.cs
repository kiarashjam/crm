using System.Net;
using FluentAssertions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An activity type is accepted whatever its case.
/// </summary>
/// <remarks>
/// The log-an-activity form on deals, contacts and the pipeline sent "Note", "Call"
/// and so on. The request validator's pattern was lower case only and runs before the
/// service (which lower-cases the type anyway), so every one of those was refused
/// with a 400 and the page said "Failed to log activity".
/// </remarks>
public class ActivityTypeCaseTests
{
    [Theory]
    [InlineData("Note")]
    [InlineData("Meeting")]
    [InlineData("call")]
    public async Task AnActivityIsLoggedWhateverTheCaseOfItsType(string type)
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");

        var contact = await client.In(org).PostAsync("/api/contacts", Body(new { name = "Eva Muster", email = "eva@example.ch" }));
        contact.IsSuccessStatusCode.Should().BeTrue(await contact.Content.ReadAsStringAsync());
        using var created = System.Text.Json.JsonDocument.Parse(await contact.Content.ReadAsStringAsync());
        var contactId = created.RootElement.GetProperty("id").GetGuid();

        var res = await client.In(org).PostAsync("/api/activities", Body(new { type, subject = "Pricing call with the CFO", contactId }));

        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());
        (await res.Content.ReadAsStringAsync()).Should().Contain($"\"type\":\"{type.ToLowerInvariant()}\"");
    }

    [Fact]
    public async Task AnUnknownTypeIsStillRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");

        var res = await client.In(org).PostAsync("/api/activities", Body(new { type = "Party", subject = "x" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
