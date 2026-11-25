using System.Net;
using System.Reflection;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Tests;

/// <summary>
/// What bounds the site analytics' table: the tracker's rate limits, per address then everyone's,
/// chained; and the events one server stores in a Zurich day.
/// </summary>
public class SiteAnalyticsLimitsTests
{
    /// <summary>A request routed to the tracker's post, from <paramref name="address"/>.</summary>
    private static HttpContext Post(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        var action = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(SiteAnalyticsController).GetTypeInfo(),
            ActionName = nameof(SiteAnalyticsController.Events),
        };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(action), "events"));
        return context;
    }

    [Fact]
    public void An_address_over_its_own_limit_takes_nothing_from_everyones()
    {
        using var limiter = SiteAnalyticsController.Limiter(perAddress: 2, everyone: 3);
        bool Acquired(HttpContext context)
        {
            using var lease = limiter.AttemptAcquire(context);
            return lease.IsAcquired;
        }

        var loud = Post("198.51.100.1");
        Enumerable.Range(0, 10).Select(_ => Acquired(loud)).Should().Equal(new[] { true, true }.Concat(Enumerable.Repeat(false, 8)));
        // Of everyone's three, the loud address took two: one is left, then none.
        Acquired(Post("198.51.100.2")).Should().BeTrue();
        Acquired(Post("198.51.100.3")).Should().BeFalse();

        // Any other request is never limited.
        var other = new DefaultHttpContext();
        other.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.1");
        Enumerable.Range(0, 20).Select(_ => Acquired(other)).Should().OnlyContain(acquired => acquired);
        SiteAnalyticsController.IsIngest(other).Should().BeFalse();
        SiteAnalyticsController.IsIngest(Post("198.51.100.1")).Should().BeTrue();
    }

    [Fact]
    public void The_limits_are_30_a_minute_from_one_address_and_600_from_everyone()
    {
        SiteAnalyticsController.PerMinute.Should().Be(30);
        SiteAnalyticsController.EveryonePerMinute.Should().Be(600);
    }

    [Fact]
    public void The_days_cap_is_told_once_and_starts_again_the_next_Zurich_day()
    {
        var budget = new SiteEventBudget(Options.Create(new SiteAnalyticsSettings { DailyEventCap = 5 }));
        var monday = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 23, 12, 0, 0), SiteAnalyticsRules.Zone);

        budget.Take(3, monday).Should().Be((3, false));
        budget.Take(3, monday.AddHours(1)).Should().Be((2, true));
        budget.Take(1, monday.AddHours(2)).Should().Be((0, false), "the cap is told once a day");
        budget.Take(0, monday.AddHours(2)).Should().Be((0, false));
        // 00:30 on Tuesday in Zurich, still Monday in UTC: a new day.
        var tuesday = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 24, 0, 30, 0), SiteAnalyticsRules.Zone);
        tuesday.Day.Should().Be(23);
        budget.Take(4, tuesday).Should().Be((4, false));
        budget.Take(4, tuesday).Should().Be((1, true));
        new SiteAnalyticsSettings().DailyEventCap.Should().Be(200_000);
    }
}
