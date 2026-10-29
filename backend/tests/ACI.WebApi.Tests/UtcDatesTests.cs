using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Dates leave the API marked as UTC.
/// </summary>
/// <remarks>
/// SQL Server returns datetime2 values with Kind=Unspecified, and System.Text.Json
/// writes those without a "Z". Browsers read such a string as local time, so every
/// date was shown shifted by the user's UTC offset, and edit forms moved task due
/// dates by the offset on every save. The row is written with Kind=Unspecified here
/// to stand in for what SQL Server hands back.
/// </remarks>
public class UtcDatesTests
{
    [Fact]
    public async Task AStoredDateComesBackWithItsUtcMarker()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");

        var id = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(new Company
            {
                Id = id, UserId = userId, OrganizationId = org, Name = "Banque Léman",
                CreatedAtUtc = new DateTime(2026, 10, 28, 14, 0, 0, DateTimeKind.Unspecified),
            });
            await db.SaveChangesAsync();
        }

        var res = await client.In(org).GetAsync($"/api/companies/{id}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("createdAtUtc").GetString().Should().Be("2026-10-28T14:00:00Z");
    }
}
