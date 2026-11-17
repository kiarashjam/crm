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
        builder.Property(e => e.SenderName).HasMaxLength(100);
        builder.Property(e => e.SenderEmail).HasMaxLength(256);
        builder.Property(e => e.ReplyTo).HasMaxLength(256);
        builder.Property(e => e.BookingPageUrl).HasMaxLength(500);
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

internal sealed class EmailImageConfiguration : IEntityTypeConfiguration<EmailImage>
{
    public void Configure(EntityTypeBuilder<EmailImage> builder)
    {
        builder.ToTable("EmailImages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        // The workspace's library, newest first.
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc }).HasDatabaseName("IX_EmailImages_OrganizationId_CreatedAtUtc");
    }
}

internal sealed class LeadNewsletterConfiguration : IEntityTypeConfiguration<LeadNewsletter>
{
    public void Configure(EntityTypeBuilder<LeadNewsletter> builder)
    {
        builder.ToTable("LeadNewsletters");
        // The id is the browser's, made when Send was pressed: it is the send's key, so the
        // same press sent twice adds nothing.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Language).HasMaxLength(5).IsRequired();
        builder.Property(e => e.AudienceJson).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc }).HasDatabaseName("IX_LeadNewsletters_OrganizationId_CreatedAtUtc");
    }
}

internal sealed class MeetingBookingConfiguration : IEntityTypeConfiguration<MeetingBooking>
{
    public void Configure(EntityTypeBuilder<MeetingBooking> builder)
    {
        builder.ToTable("MeetingBookings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BookingId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(16);
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        // One row per booking the website names: its retry finds the row and changes nothing.
        builder.HasIndex(e => new { e.OrganizationId, e.BookingId }).IsUnique().HasDatabaseName("UX_MeetingBookings_OrganizationId_BookingId");
        builder.HasIndex(e => e.LeadId).HasDatabaseName("IX_MeetingBookings_LeadId");
    }
}
