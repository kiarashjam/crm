using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class DealStageConfiguration : IEntityTypeConfiguration<DealStage>
{
    public void Configure(EntityTypeBuilder<DealStage> builder)
    {
        builder.ToTable("DealStages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.HasOne(e => e.Pipeline).WithMany(p => p.DealStages).HasForeignKey(e => e.PipelineId);
        builder.HasIndex(e => e.PipelineId);
    }
}
