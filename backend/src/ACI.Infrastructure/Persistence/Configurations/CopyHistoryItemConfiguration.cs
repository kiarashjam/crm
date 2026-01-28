using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class CopyHistoryItemConfiguration : IEntityTypeConfiguration<CopyHistoryItem>
{
    public void Configure(EntityTypeBuilder<CopyHistoryItem> builder)
    {
        builder.ToTable("CopyHistoryItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Copy).IsRequired();
        builder.Property(e => e.RecipientName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.RecipientId).HasMaxLength(128).IsRequired();
        builder.HasOne(e => e.User).WithMany(u => u.CopyHistory).HasForeignKey(e => e.UserId);
    }
}
