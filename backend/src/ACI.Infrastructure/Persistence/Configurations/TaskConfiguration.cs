using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");
        builder.HasKey(e => e.Id);
        
        // Core fields
        builder.Property(e => e.Title).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4096);
        builder.Property(e => e.Notes).HasMaxLength(4096);
        
        // Status and Priority
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(Domain.Enums.TaskStatus.Todo);
        
        builder.Property(e => e.Priority)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(TaskPriority.None);
        
        // Dates
        builder.Property(e => e.DueDateUtc);
        builder.Property(e => e.ReminderDateUtc);
        builder.Property(e => e.CreatedAtUtc);
        builder.Property(e => e.UpdatedAtUtc);
        builder.Property(e => e.CompletedAtUtc);
        
        // Relationships
        builder.HasOne(e => e.User).WithMany(u => u.TaskItems).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.Assignee).WithMany().HasForeignKey(e => e.AssigneeId).IsRequired(false);
        builder.HasOne(e => e.Lead).WithMany(l => l.TaskItems).HasForeignKey(e => e.LeadId);
        builder.HasOne(e => e.Deal).WithMany(d => d.TaskItems).HasForeignKey(e => e.DealId);
        builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId).IsRequired(false);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        
        // Indexes for common queries
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.ContactId);
        builder.HasIndex(e => e.AssigneeId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.DueDateUtc);
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
        builder.HasIndex(e => new { e.UserId, e.Status });
    }
}
