using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");
        builder.HasKey(e => new { e.OrganizationId, e.UserId });
        builder.HasOne(e => e.Organization).WithMany(o => o.Members).HasForeignKey(e => e.OrganizationId);
        builder.HasOne(e => e.User).WithMany(u => u.OrganizationMemberships).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(32);
    }
}
