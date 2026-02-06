using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(8);
        builder.Property(e => e.Stage).HasMaxLength(128);
        builder.HasOne(e => e.User).WithMany(u => u.Deals).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.Pipeline).WithMany(p => p.Deals).HasForeignKey(e => e.PipelineId).IsRequired(false);
        builder.HasOne(e => e.DealStage).WithMany(s => s.Deals).HasForeignKey(e => e.DealStageId).IsRequired(false);
        builder.HasOne(e => e.Company).WithMany(c => c.Deals).HasForeignKey(e => e.CompanyId);
        builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId);
        builder.HasOne(e => e.Assignee).WithMany().HasForeignKey(e => e.AssigneeId).IsRequired(false);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.PipelineId);
        builder.HasIndex(e => e.DealStageId);
    }
}
