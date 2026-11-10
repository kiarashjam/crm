using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The tables are created by the start-up DDL in Program.cs, which declares the same
// columns and indexes; the two are kept in step (SchemaCoverageTests checks the indexes).

internal sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable("OutboundMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(1000);
        builder.Property(e => e.FromName).HasMaxLength(200);
        builder.Property(e => e.FromAddress).HasMaxLength(256);
        builder.Property(e => e.ReplyTo).HasMaxLength(600);
        // To, Cc and Bcc are nvarchar(max): twenty long addresses do not fit in 2000.
        builder.Property(e => e.RecipientName).HasMaxLength(200);
        builder.Property(e => e.Subject).HasMaxLength(300);
        builder.Property(e => e.UnsubscribeUrl).HasMaxLength(1000);
        builder.Property(e => e.Purpose).HasMaxLength(16);
        builder.Property(e => e.MessageIdHeader).HasMaxLength(255);
        builder.Property(e => e.ReplyToken).HasMaxLength(64);
        builder.Property(e => e.DeliveryStatus).HasMaxLength(32);
        builder.Property(e => e.DeliveryDetail).HasMaxLength(500);
        builder.Property(e => e.EventName).HasMaxLength(64);

        // The dispatcher's question, every few seconds on every instance: what is due?
        builder.HasIndex(e => new { e.Status, e.NextAttemptAtUtc }).HasDatabaseName("IX_OutboundMessages_Status_NextAttemptAtUtc");
        // A workspace's messages, newest first (the webhook delivery log).
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc }).HasDatabaseName("IX_OutboundMessages_OrganizationId_CreatedAtUtc");
        // Whether a sequence step's email has gone yet.
        builder.HasIndex(e => new { e.SourceId, e.Status }).HasDatabaseName("IX_OutboundMessages_SourceId_Status");
        // Replies and the mail service's reports name the message by these.
        builder.HasIndex(e => e.MessageIdHeader).IsUnique().HasFilter("[MessageIdHeader] IS NOT NULL")
            .HasDatabaseName("IX_OutboundMessages_MessageIdHeader");
        builder.HasIndex(e => e.ReplyToken).IsUnique().HasFilter("[ReplyToken] IS NOT NULL")
            .HasDatabaseName("IX_OutboundMessages_ReplyToken");
    }
}

internal sealed class EmailSuppressionConfiguration : IEntityTypeConfiguration<EmailSuppression>
{
    public void Configure(EntityTypeBuilder<EmailSuppression> builder)
    {
        builder.ToTable("EmailSuppressions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(500);
        // One row per address per workspace: a second report about it changes nothing.
        builder.HasIndex(e => new { e.OrganizationId, e.Email }).IsUnique().HasDatabaseName("IX_EmailSuppressions_OrganizationId_Email");
    }
}

internal sealed class InboundEmailConfiguration : IEntityTypeConfiguration<InboundEmail>
{
    public void Configure(EntityTypeBuilder<InboundEmail> builder)
    {
        builder.ToTable("InboundEmails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MessageIdHeader).HasMaxLength(255);
        builder.Property(e => e.FromAddress).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300);
        builder.Property(e => e.MatchedBy).HasMaxLength(16).IsRequired();
        // The same reply delivered twice is the same row.
        builder.HasIndex(e => new { e.OrganizationId, e.MessageIdHeader }).IsUnique().HasFilter("[MessageIdHeader] IS NOT NULL")
            .HasDatabaseName("IX_InboundEmails_OrganizationId_MessageIdHeader");
        builder.HasIndex(e => e.OutboundMessageId).HasDatabaseName("IX_InboundEmails_OutboundMessageId");
    }
}
