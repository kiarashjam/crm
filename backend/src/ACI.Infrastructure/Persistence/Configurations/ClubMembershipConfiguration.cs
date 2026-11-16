using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Membership applications, the public form people apply on, and households (see
// ClubMembership.cs in ACI.Domain). The rules a household keeps (one primary, one household
// per person) are unique indexes here, so they hold on SQL Server whatever two requests do
// at the same moment; the services check them first to say why.

internal sealed class ClubApplicationConfiguration : IEntityTypeConfiguration<ClubApplication>
{
    public void Configure(EntityTypeBuilder<ClubApplication> builder)
    {
        builder.ToTable("ClubApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.RequestedTier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ReferredByName).HasMaxLength(200);
        builder.Property(e => e.Occupation).HasMaxLength(200);
        builder.Property(e => e.ReasonForJoining).HasMaxLength(2000);
        builder.Property(e => e.Language).HasMaxLength(5);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ReviewerNotes).HasMaxLength(2000);
        builder.Property(e => e.HouseholdRole).HasMaxLength(20);
        builder.Property(e => e.ApprovalTrigger).HasMaxLength(20);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.Status, e.SubmittedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasFilter("[MemberId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.LeadId }).HasFilter("[LeadId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ContactId }).HasFilter("[ContactId] IS NOT NULL");
        // A member's page lists the applications they referred, proposed and seconded.
        builder.HasIndex(e => new { e.OrganizationId, e.ReferredByMemberId }).HasFilter("[ReferredByMemberId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ProposerMemberId }).HasFilter("[ProposerMemberId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.SeconderMemberId }).HasFilter("[SeconderMemberId] IS NOT NULL");
    }
}

internal sealed class ClubApplicationFormConfiguration : IEntityTypeConfiguration<ClubApplicationForm>
{
    public void Configure(EntityTypeBuilder<ClubApplicationForm> builder)
    {
        builder.ToTable("ClubApplicationForms");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PublicToken).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Intro).HasMaxLength(1000);
        builder.Property(e => e.SuccessMessage).HasMaxLength(500);
        builder.Property(e => e.ConsentText).HasMaxLength(500);
        // The anonymous form finds its workspace by the token alone.
        builder.HasIndex(e => e.PublicToken).IsUnique();
        // One form per workspace: its link is the club's.
        builder.HasIndex(e => e.OrganizationId).IsUnique().HasFilter("[OrganizationId] IS NOT NULL");
    }
}

internal sealed class ClubHouseholdConfiguration : IEntityTypeConfiguration<ClubHousehold>
{
    public void Configure(EntityTypeBuilder<ClubHousehold> builder)
    {
        builder.ToTable("ClubHouseholds");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubHouseholdMemberConfiguration : IEntityTypeConfiguration<ClubHouseholdMember>
{
    public void Configure(EntityTypeBuilder<ClubHouseholdMember> builder)
    {
        builder.ToTable("ClubHouseholdMembers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Role).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.HasOne<ClubHousehold>().WithMany().HasForeignKey(e => e.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        // One primary per household, whatever two edits do at once.
        builder.HasIndex(e => e.HouseholdId).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("IX_ClubHouseholdMembers_OnePrimary");
        builder.HasIndex(e => new { e.HouseholdId, e.CreatedAtUtc });
        // A person is in one household: a member once, a contact once.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).IsUnique().HasFilter("[MemberId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ContactId }).IsUnique().HasFilter("[ContactId] IS NOT NULL");
    }
}
