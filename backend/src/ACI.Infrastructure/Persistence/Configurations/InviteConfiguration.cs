using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("Invites");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        // The column kept its name when it started holding a hash instead of the token.
        builder.Property(e => e.TokenHash).HasColumnName("Token").HasMaxLength(128).IsRequired();
        builder.HasOne(e => e.Organization).WithMany(o => o.Invites).HasForeignKey(e => e.OrganizationId);
        builder.HasIndex(e => new { e.OrganizationId, e.Email });
        builder.HasIndex(e => e.TokenHash).IsUnique();
    }
}
