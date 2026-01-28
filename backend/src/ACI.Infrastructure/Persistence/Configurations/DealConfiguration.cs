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
        builder.Property(e => e.Stage).HasMaxLength(128);
        builder.HasOne(e => e.User).WithMany(u => u.Deals).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Company).WithMany(c => c.Deals).HasForeignKey(e => e.CompanyId);
    }
}
