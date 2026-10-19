using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ForecastSnapshotConfiguration : IEntityTypeConfiguration<ForecastSnapshot>
{
    public void Configure(EntityTypeBuilder<ForecastSnapshot> builder)
    {
        builder.ToTable("ForecastSnapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Currency).HasMaxLength(8).IsRequired();
        builder.Property(e => e.AssigneeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.WeightedValue).HasPrecision(18, 2);
        builder.Property(e => e.OpenValue).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.OrganizationId, e.MonthUtc });
    }
}

internal sealed class SentMessageConfiguration : IEntityTypeConfiguration<SentMessage>
{
    public void Configure(EntityTypeBuilder<SentMessage> builder)
    {
        builder.ToTable("SentMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.To).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Cc).HasMaxLength(2000);
        builder.Property(e => e.Bcc).HasMaxLength(2000);
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.SentAtUtc });
    }
}

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Trigger).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ActionsJson).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.Trigger });
    }
}

internal sealed class AutomationFireConfiguration : IEntityTypeConfiguration<AutomationFire>
{
    public void Configure(EntityTypeBuilder<AutomationFire> builder)
    {
        builder.ToTable("AutomationFires");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SourceKey).HasMaxLength(80).IsRequired();
        builder.HasIndex(e => new { e.RuleId, e.SourceKey }).IsUnique();
    }
}

internal sealed class ClubMemberConfiguration : IEntityTypeConfiguration<ClubMember>
{
    public void Configure(EntityTypeBuilder<ClubMember> builder)
    {
        builder.ToTable("ClubMembers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Tier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DuesFrequency).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DuesAmount).HasPrecision(18, 2);
        builder.Property(e => e.HouseAccountBalance).HasPrecision(18, 2);
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubVisitConfiguration : IEntityTypeConfiguration<ClubVisit>
{
    public void Configure(EntityTypeBuilder<ClubVisit> builder)
    {
        builder.ToTable("ClubVisits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MemberTier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Venue).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.MemberId);
    }
}

internal sealed class LoyaltyLedgerEntryConfiguration : IEntityTypeConfiguration<LoyaltyLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyLedgerEntry> builder)
    {
        builder.ToTable("LoyaltyLedger");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.MemberId);
    }
}
