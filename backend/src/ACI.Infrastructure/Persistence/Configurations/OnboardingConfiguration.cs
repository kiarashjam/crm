using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class OnboardingSettingsConfiguration : IEntityTypeConfiguration<OnboardingSettings>
{
    public void Configure(EntityTypeBuilder<OnboardingSettings> builder)
    {
        builder.ToTable("OnboardingSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WebhookUrl).HasMaxLength(500);
        builder.Property(e => e.WebhookSecret).HasMaxLength(200);
        builder.Property(e => e.EmailSubject).HasMaxLength(300);
        // EmailHtml is nvarchar(max): a designed email runs to tens of kilobytes.
        builder.Property(e => e.DefaultLanguage).HasMaxLength(5).IsRequired();
        // One row per organization.
        builder.HasIndex(e => e.OrganizationId).IsUnique().HasDatabaseName("IX_OnboardingSettings_OrganizationId");
    }
}

internal sealed class DealOnboardingConfiguration : IEntityTypeConfiguration<DealOnboarding>
{
    public void Configure(EntityTypeBuilder<DealOnboarding> builder)
    {
        builder.ToTable("DealOnboardings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Language).HasMaxLength(5).IsRequired();
        builder.Property(e => e.WelcomeMessage).HasMaxLength(4000);
        builder.Property(e => e.EmailSubject).HasMaxLength(300);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.Property(e => e.ExternalAccountId).HasMaxLength(200);
        builder.Property(e => e.StepsJson).IsRequired();
        // A deal's handoffs, newest first (the deal page and the convert dialog ask).
        builder.HasIndex(e => new { e.OrganizationId, e.DealId, e.CreatedAtUtc }).HasDatabaseName("IX_DealOnboardings_OrganizationId_DealId_CreatedAtUtc");
    }
}
