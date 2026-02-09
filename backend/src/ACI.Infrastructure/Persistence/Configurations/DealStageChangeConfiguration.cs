using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class DealStageChangeConfiguration : IEntityTypeConfiguration<DealStageChange>
{
    public void Configure(EntityTypeBuilder<DealStageChange> builder)
    {
        builder.ToTable("DealStageChanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FromStageName).HasMaxLength(128);
        builder.Property(e => e.ToStageName).HasMaxLength(128);
        builder.HasOne(e => e.Deal).WithMany(d => d.StageChanges).HasForeignKey(e => e.DealId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.ChangedByUser).WithMany().HasForeignKey(e => e.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.DealId);
    }
}
