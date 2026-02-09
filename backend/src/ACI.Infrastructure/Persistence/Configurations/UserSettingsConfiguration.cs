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

        // Profile
        builder.Property(e => e.BrandName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(128);
        builder.Property(e => e.AvatarUrl).HasMaxLength(512);
        builder.Property(e => e.Phone).HasMaxLength(32);
        builder.Property(e => e.Timezone).HasMaxLength(64).HasDefaultValue("UTC");
        builder.Property(e => e.Language).HasMaxLength(10).HasDefaultValue("en");
        builder.Property(e => e.Bio).HasMaxLength(500);

        // Brand
        builder.Property(e => e.EmailSignature).HasMaxLength(2000);
        builder.Property(e => e.DefaultEmailSubjectPrefix).HasMaxLength(100);

        // Defaults
        builder.Property(e => e.DefaultPipelineId).HasMaxLength(64);
        builder.Property(e => e.DefaultLeadStatusId).HasMaxLength(64);
        builder.Property(e => e.DefaultLeadSourceId).HasMaxLength(64);
        builder.Property(e => e.DefaultCurrency).HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(e => e.DefaultFollowUpDays).HasDefaultValue(3);

        // Relationship
        builder.HasOne(e => e.User).WithOne(u => u.Settings).HasForeignKey<UserSettings>(e => e.UserId);
    }
}
