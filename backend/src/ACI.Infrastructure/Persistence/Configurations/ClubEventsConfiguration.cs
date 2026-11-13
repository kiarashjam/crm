using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Club events, their guest lists, the club's bookable resources and their bookings (see
// ClubEvents.cs in ACI.Domain). Money is decimal(18,2) with a currency code beside it; what
// is charged is on the ledger, and these rows only point at it.

internal sealed class ClubEventConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> builder)
    {
        builder.ToTable("ClubEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.MemberPrice).HasPrecision(18, 2);
        builder.Property(e => e.GuestPrice).HasPrecision(18, 2);
        builder.Property(e => e.NoShowFee).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Visibility).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CoverEmoji).HasMaxLength(16).IsRequired();
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.StartAtUtc });
    }
}

internal sealed class ClubEventRegistrationConfiguration : IEntityTypeConfiguration<ClubEventRegistration>
{
    public void Configure(EntityTypeBuilder<ClubEventRegistration> builder)
    {
        builder.ToTable("ClubEventRegistrations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.TicketTier).HasMaxLength(20).IsRequired();
        builder.Property(e => e.HostName).HasMaxLength(200);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CheckedInByName).HasMaxLength(200);
        builder.Property(e => e.CancelledByName).HasMaxLength(200);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.TicketPrice).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.NoShowFee).HasPrecision(18, 2);
        builder.Property(e => e.ImportedFromId).HasMaxLength(100);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.HasOne<ClubEvent>().WithMany().HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.EventId, e.Status, e.RegisteredAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId });
        builder.HasIndex(e => new { e.OrganizationId, e.HostMemberId });
    }
}

internal sealed class ClubResourceConfiguration : IEntityTypeConfiguration<ClubResource>
{
    public void Configure(EntityTypeBuilder<ClubResource> builder)
    {
        builder.ToTable("ClubResources");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.BookingMode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DepositAmount).HasPrecision(18, 2);
        builder.Property(e => e.NoShowFee).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        // Every resource there is stays out of the member portal until the club offers it there.
        builder.Property(e => e.MemberBooking).HasMaxLength(10).IsRequired().HasDefaultValue(MemberBookingModes.Off);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubReservationConfiguration : IEntityTypeConfiguration<ClubReservation>
{
    public void Configure(EntityTypeBuilder<ClubReservation> builder)
    {
        builder.ToTable("ClubReservations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ResourceName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MemberEmail).HasMaxLength(320);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.DepositAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.DepositWaivedReason).HasMaxLength(500);
        builder.Property(e => e.DepositWaivedByName).HasMaxLength(200);
        builder.Property(e => e.NoShowFee).HasPrecision(18, 2);
        builder.Property(e => e.CancelledByName).HasMaxLength(200);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        // What a conflict check reads: one resource's bookings around a time.
        builder.HasIndex(e => new { e.OrganizationId, e.ResourceId, e.StartAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId });
    }
}
