using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> builder)
    {
        builder.ToTable("JoinRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(e => e.Organization).WithMany(o => o.JoinRequests).HasForeignKey(e => e.OrganizationId);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.OrganizationId, e.UserId });
    }
}
