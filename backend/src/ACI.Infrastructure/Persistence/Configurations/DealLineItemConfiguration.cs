using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class DealLineItemConfiguration : IEntityTypeConfiguration<DealLineItem>
{
    public void Configure(EntityTypeBuilder<DealLineItem> builder)
    {
        builder.ToTable("DealLineItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ProductId).HasMaxLength(64);
        // Hours and part-units are real quantities; prices need their cents.
        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 4);
        builder.HasIndex(e => e.DealId);
    }
}
