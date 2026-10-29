using System.Net;

namespace ACI.WebApi.Tests;

/// <summary>
/// /db-status is anonymous. It used to answer with the whole start-up exception: SQL
/// server name, login errors, table names and stack frames.
/// </summary>
public class DbStatusTests
{
    [Fact]
    public async Task OutsideDevelopmentItSaysOnlyThatStartUpFailed()
    {
        // The test host's in-memory database cannot run the start-up DDL, so start-up
        // "fails" here exactly as it would after a SQL outage at boot.
        using var factory = new CustomWebApplicationFactory();

        var res = await factory.CreateClient().GetAsync("/db-status");
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"status\":\"error\"");
        body.Should().NotContain("Exception").And.NotContain(" at ").And.NotContain("Microsoft.");
    }
}
