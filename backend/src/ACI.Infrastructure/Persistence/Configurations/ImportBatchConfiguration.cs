using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        // Created by the DDL in Program.cs, which has to stay in step with this.
        builder.ToTable("ImportBatches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.DuplicateMode).HasMaxLength(16).IsRequired();
        // The import pages list a workspace's latest runs.
        builder.HasIndex(e => new { e.OrganizationId, e.StartedAtUtc });
    }
}
