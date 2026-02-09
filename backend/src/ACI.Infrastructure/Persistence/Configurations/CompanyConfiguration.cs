using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Domain).HasMaxLength(256);
        builder.Property(e => e.Industry).HasMaxLength(128);
        builder.Property(e => e.Size).HasMaxLength(64);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Website).HasMaxLength(500);
        builder.Property(e => e.Location).HasMaxLength(300);
        builder.HasOne(e => e.User).WithMany(u => u.Companies).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        builder.HasIndex(e => e.OrganizationId);
    }
}
