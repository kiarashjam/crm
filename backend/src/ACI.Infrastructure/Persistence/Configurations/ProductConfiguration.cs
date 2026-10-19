using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Sku).HasMaxLength(64);
        builder.Property(e => e.Currency).HasMaxLength(8);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 4);
        builder.HasIndex(e => e.OrganizationId);
    }
}
