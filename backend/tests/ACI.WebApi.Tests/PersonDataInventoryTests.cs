using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every table of the model is placed: it holds data about people (and the inventory says what
/// a person's erasure does with it), or it does not, and says why.
/// </summary>
/// <remarks>
/// Erasure used to remove one row and leave the person in every table nobody remembered. A
/// table added next month that keeps a name or an address fails here until someone decides
/// what the export and the erasure do with it.
/// </remarks>
public class PersonDataInventoryTests
{
    [Fact]
    public void EveryTableIsPlacedAsAboutPeopleOrNot()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var types = db.Model.GetEntityTypes().Where(t => !t.IsOwned()).Select(t => t.ClrType).Distinct().ToList();
        types.Should().HaveCountGreaterThan(90, "the model's tables should have been found");

        types.Where(t => !PersonDataInventory.AboutPeople.ContainsKey(t) && !PersonDataInventory.NotAboutPeople.ContainsKey(t))
            .Select(t => t.Name)
            .Should().BeEmpty("a table needs a line in PersonDataInventory: what a person's erasure does with it, or why it holds nothing about anyone");
        PersonDataInventory.AboutPeople.Keys.Intersect(PersonDataInventory.NotAboutPeople.Keys).Should().BeEmpty("a table is one or the other");
        PersonDataInventory.AboutPeople.Keys.Concat(PersonDataInventory.NotAboutPeople.Keys).Where(t => !types.Contains(t))
            .Select(t => t.Name).Should().BeEmpty("a line for a table that is gone would excuse the next one");
    }
}
