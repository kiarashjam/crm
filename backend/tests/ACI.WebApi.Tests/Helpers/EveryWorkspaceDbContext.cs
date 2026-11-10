using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The app's model without its workspace filters: what a test reads to see what was saved,
/// in whichever workspace it went. The app itself never reads this way; its code acts for
/// a workspace, or says why it reads across them (IgnoreQueryFilters).
/// </summary>
public sealed class EveryWorkspaceDbContext : AppDbContext
{
    public EveryWorkspaceDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var type in modelBuilder.Model.GetEntityTypes())
            type.SetQueryFilter(null);
    }
}
