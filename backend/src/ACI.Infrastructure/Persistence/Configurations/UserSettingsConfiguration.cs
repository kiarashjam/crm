using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(e => e.UserId);
        builder.Property(e => e.CompanyName).HasMaxLength(256).IsRequired();
        builder.HasOne(e => e.User).WithOne(u => u.Settings).HasForeignKey<UserSettings>(e => e.UserId);
    }
}
