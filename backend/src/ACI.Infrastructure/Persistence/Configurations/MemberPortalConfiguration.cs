using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class MemberPortalSettingsConfiguration : IEntityTypeConfiguration<MemberPortalSettings>
{
    public void Configure(EntityTypeBuilder<MemberPortalSettings> builder)
    {
        builder.ToTable("MemberPortalSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PortalKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();
        // One row per organization, and one organization per portal address.
        builder.HasIndex(e => e.OrganizationId).IsUnique().HasDatabaseName("IX_MemberPortalSettings_OrganizationId");
        builder.HasIndex(e => e.PortalKey).IsUnique().HasDatabaseName("IX_MemberPortalSettings_PortalKey");
    }
}

internal sealed class MemberPortalAccountConfiguration : IEntityTypeConfiguration<MemberPortalAccount>
{
    public void Configure(EntityTypeBuilder<MemberPortalAccount> builder)
    {
        builder.ToTable("MemberPortalAccounts");
        builder.HasKey(e => e.Id);
        // One per member: two first sign-ins at the same moment make one, not two.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).IsUnique().HasDatabaseName("IX_MemberPortalAccounts_OrganizationId_MemberId");
    }
}

internal sealed class MemberPortalSessionConfiguration : IEntityTypeConfiguration<MemberPortalSession>
{
    public void Configure(EntityTypeBuilder<MemberPortalSession> builder)
    {
        builder.ToTable("MemberPortalSessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Via).HasMaxLength(16).IsRequired();
        builder.Property(e => e.RevokedBy).HasMaxLength(16);
        // A member's sessions, to end them all; and the old ones, to sweep them.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasDatabaseName("IX_MemberPortalSessions_OrganizationId_MemberId");
        builder.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("IX_MemberPortalSessions_ExpiresAtUtc");
    }
}

internal sealed class MemberPortalLinkConfiguration : IEntityTypeConfiguration<MemberPortalLink>
{
    public void Configure(EntityTypeBuilder<MemberPortalLink> builder)
    {
        builder.ToTable("MemberPortalLinks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Purpose).HasMaxLength(16).IsRequired();
        builder.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
        // A link is found by its secret's hash alone; two links never share one.
        builder.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("IX_MemberPortalLinks_TokenHash");
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasDatabaseName("IX_MemberPortalLinks_OrganizationId_MemberId");
        builder.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("IX_MemberPortalLinks_ExpiresAtUtc");
    }
}
