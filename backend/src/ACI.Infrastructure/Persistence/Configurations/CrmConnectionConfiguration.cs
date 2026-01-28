using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class CrmConnectionConfiguration : IEntityTypeConfiguration<CrmConnection>
{
    public void Configure(EntityTypeBuilder<CrmConnection> builder)
    {
        builder.ToTable("CrmConnections");
        builder.HasKey(e => e.UserId);
        builder.Property(e => e.AccountEmail).HasMaxLength(256);
        builder.Property(e => e.EncryptedToken).HasMaxLength(2000);
        builder.HasOne(e => e.User).WithOne(u => u.CrmConnection).HasForeignKey<CrmConnection>(e => e.UserId);
    }
}
