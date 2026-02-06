using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for EmailSequence entity.
/// </summary>
internal sealed class EmailSequenceConfiguration : IEntityTypeConfiguration<EmailSequence>
{
    public void Configure(EntityTypeBuilder<EmailSequence> builder)
    {
        builder.ToTable("EmailSequences");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        
        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasMany(e => e.Steps)
            .WithOne(s => s.Sequence)
            .HasForeignKey(s => s.SequenceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(e => e.Enrollments)
            .WithOne(en => en.Sequence)
            .HasForeignKey(en => en.SequenceId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.OrganizationId, e.IsSharedWithOrganization });
    }
}

/// <summary>
/// Entity Framework configuration for EmailSequenceStep entity.
/// </summary>
internal sealed class EmailSequenceStepConfiguration : IEntityTypeConfiguration<EmailSequenceStep>
{
    public void Configure(EntityTypeBuilder<EmailSequenceStep> builder)
    {
        builder.ToTable("EmailSequenceSteps");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.CopyTypeId).HasConversion<string>().HasMaxLength(64);
        
        // Indexes
        builder.HasIndex(e => e.SequenceId);
        builder.HasIndex(e => new { e.SequenceId, e.StepOrder });
    }
}

/// <summary>
/// Entity Framework configuration for EmailSequenceEnrollment entity.
/// </summary>
internal sealed class EmailSequenceEnrollmentConfiguration : IEntityTypeConfiguration<EmailSequenceEnrollment>
{
    public void Configure(EntityTypeBuilder<EmailSequenceEnrollment> builder)
    {
        builder.ToTable("EmailSequenceEnrollments");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.RecipientEmail).HasMaxLength(256);
        builder.Property(e => e.RecipientName).HasMaxLength(256);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        
        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);
            
        builder.HasOne(e => e.Contact)
            .WithMany()
            .HasForeignKey(e => e.ContactId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
            
        builder.HasOne(e => e.Lead)
            .WithMany()
            .HasForeignKey(e => e.LeadId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
        
        // Indexes
        builder.HasIndex(e => e.SequenceId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.ContactId);
        builder.HasIndex(e => e.LeadId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.NextSendAtUtc);
    }
}
