using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EntityLabel).HasMaxLength(200);
        builder.Property(e => e.Action).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(200).IsRequired();
        // The page reads a workspace's newest lines first, and a record's history by id.
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
