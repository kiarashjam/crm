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
        builder.HasOne(e => e.User).WithMany(u => u.Leads).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Company).WithMany(c => c.Leads).HasForeignKey(e => e.CompanyId);
    }
}
