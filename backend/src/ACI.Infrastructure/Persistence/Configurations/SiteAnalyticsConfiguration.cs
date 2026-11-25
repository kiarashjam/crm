using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The site analytics of cadencee.ai (see SiteAnalytics.cs in ACI.Domain). Global tables: none
// has an OrganizationId, so no workspace filter applies (AppDbContext.Workspaces.cs), and they
// are read only by platform admins. Every report reads a range of days, so the events are
// indexed by day first; the live view reads the last minutes by time.

internal sealed class SiteEventConfiguration : IEntityTypeConfiguration<SiteEvent>
{
    public void Configure(EntityTypeBuilder<SiteEvent> builder)
    {
        builder.ToTable("SiteEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Type).HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(64);
        builder.Property(e => e.Path).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200);
        builder.Property(e => e.ReferrerHost).HasMaxLength(253);
        builder.Property(e => e.UtmSource).HasMaxLength(100);
        builder.Property(e => e.UtmMedium).HasMaxLength(100);
        builder.Property(e => e.UtmCampaign).HasMaxLength(100);
        builder.Property(e => e.UtmTerm).HasMaxLength(100);
        builder.Property(e => e.UtmContent).HasMaxLength(100);
        builder.Property(e => e.Source).HasMaxLength(100).IsRequired();
        builder.Property(e => e.VisitorHash).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(e => e.SessionKey).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(e => e.Device).HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.Property(e => e.Browser).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Os).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Language).HasMaxLength(8).IsUnicode(false);
        builder.Property(e => e.ScreenBucket).HasMaxLength(16).IsUnicode(false);

        // A range of days: the daily series, the top lists, the email, the clean-up.
        builder.HasIndex(e => new { e.Day, e.Type });
        // A day's sessions and their steps (the journeys view, the session figures).
        builder.HasIndex(e => new { e.Day, e.SessionKey, e.OccurredAtUtc });
        // The live view: the last few minutes, and the last events.
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}

internal sealed class SiteBotHitConfiguration : IEntityTypeConfiguration<SiteBotHit>
{
    public void Configure(EntityTypeBuilder<SiteBotHit> builder)
    {
        builder.ToTable("SiteBotHits");
        // One count per robot and day, added to with an UPDATE.
        builder.HasKey(e => new { e.Day, e.Bot });
        builder.Property(e => e.Bot).HasMaxLength(32).IsUnicode(false);
    }
}

internal sealed class SiteAnalyticsSecretConfiguration : IEntityTypeConfiguration<SiteAnalyticsSecret>
{
    public void Configure(EntityTypeBuilder<SiteAnalyticsSecret> builder)
    {
        builder.ToTable("SiteAnalyticsSecrets");
        // Always 1: the key is the guard against two servers each keeping a secret of their own.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ProtectedSecret).HasMaxLength(1000).IsRequired();
    }
}

internal sealed class SiteAnalyticsDigestConfiguration : IEntityTypeConfiguration<SiteAnalyticsDigest>
{
    public void Configure(EntityTypeBuilder<SiteAnalyticsDigest> builder)
    {
        builder.ToTable("SiteAnalyticsDigests");
        // The day is the claim: a second insert for it fails, so it goes once.
        builder.HasKey(e => e.Day);
        builder.Property(e => e.Day).ValueGeneratedNever();
    }
}
