using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Corporate memberships and their nominees (see ClubCorporate.cs in ACI.Domain). The filtered
// unique index is the database's own guard for what the service checks first: a member holds
// one corporate seat at a time. The seat count is held by the membership's Version, which every
// nominee change saves first.

internal sealed class ClubCorporateMembershipConfiguration : IEntityTypeConfiguration<ClubCorporateMembership>
{
    public void Configure(EntityTypeBuilder<ClubCorporateMembership> builder)
    {
        builder.ToTable("ClubCorporateMemberships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.NomineeCharges).HasMaxLength(20).IsRequired();
        builder.Property(e => e.NotRenewingReason).HasMaxLength(500);
        builder.Property(e => e.BillingEmail).HasMaxLength(320);
        builder.Property(e => e.Language).HasMaxLength(5);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.EndedReason).HasMaxLength(500);
        builder.Property(e => e.EndedByName).HasMaxLength(200);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.CompanyId });
        builder.HasIndex(e => new { e.OrganizationId, e.Status, e.RenewsAtUtc });
    }
}

internal sealed class ClubCorporateNomineeConfiguration : IEntityTypeConfiguration<ClubCorporateNominee>
{
    public void Configure(EntityTypeBuilder<ClubCorporateNominee> builder)
    {
        builder.ToTable("ClubCorporateNominees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AddedByName).HasMaxLength(200);
        builder.Property(e => e.RemovedByName).HasMaxLength(200);
        builder.Property(e => e.RemovedReason).HasMaxLength(500);
        builder.HasOne<ClubCorporateMembership>().WithMany().HasForeignKey(e => e.CorporateMembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.OrganizationId, e.CorporateMembershipId });
        // One seat at a time: a member who is somebody's nominee is nobody else's until they leave it.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).IsUnique().HasFilter("[RemovedAtUtc] IS NULL");
    }
}
