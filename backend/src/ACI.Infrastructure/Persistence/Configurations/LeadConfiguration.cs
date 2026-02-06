using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Source).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.LifecycleStage).HasMaxLength(64);

        // Relationships
        builder.HasOne(e => e.User).WithMany(u => u.Leads).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.Company).WithMany(c => c.Leads).HasForeignKey(e => e.CompanyId);
        builder.HasOne(e => e.LeadSource).WithMany().HasForeignKey(e => e.LeadSourceId).IsRequired(false);
        builder.HasOne(e => e.LeadStatus).WithMany().HasForeignKey(e => e.LeadStatusId).IsRequired(false);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        // ConvertedToContact is configured in ContactConfiguration (one-to-one)
        builder.HasOne(e => e.ConvertedToDeal).WithMany().HasForeignKey(e => e.ConvertedToDealId).IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.IsConverted);
    }
}
