using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The club's price catalogue and what its billing records (see ClubCatalog.cs and
// ClubBilling.cs in ACI.Domain). Money is decimal(18,2) beside a currency code, a VAT rate
// decimal(5,2) in percent. The unique indexes are the database's own guard for what the
// services check first: one tier of a name per workspace (members carry the name), one
// daily run per workspace and day, one reminder per charge and step, one monthly statement
// per member and month. The in-memory provider the HTTP tests use enforces none of them.

internal sealed class ClubCatalogItemConfiguration : IEntityTypeConfiguration<ClubCatalogItem>
{
    public void Configure(EntityTypeBuilder<ClubCatalogItem> builder)
    {
        builder.ToTable("ClubCatalogItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Category).HasMaxLength(60);
        builder.Property(e => e.Code).HasMaxLength(64);
        builder.Property(e => e.Price).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.Property(e => e.Cadence).HasMaxLength(20);
        builder.Property(e => e.InitiationFee).HasPrecision(18, 2);
        builder.Property(e => e.LoyaltyMultiplier).HasPrecision(5, 2);
        builder.Property(e => e.ChargeKind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.Kind, e.SortOrder });
        // A member's tier is the tier's name, so a workspace has one tier of each name.
        builder.HasIndex(e => new { e.OrganizationId, e.Name }, "IX_ClubCatalogItems_OneTierPerName").IsUnique()
            .HasFilter("[Kind] = N'Membership'");
    }
}

internal sealed class ClubBillingRunConfiguration : IEntityTypeConfiguration<ClubBillingRun>
{
    public void Configure(EntityTypeBuilder<ClubBillingRun> builder)
    {
        builder.ToTable("ClubBillingRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Trigger).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.StartedByName).HasMaxLength(200);
        builder.Property(e => e.AmountBilled).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Steps).HasMaxLength(200);
        builder.HasIndex(e => new { e.OrganizationId, e.StartedAtUtc });
        // The daily run, once per workspace per local day: a second server finds the day taken.
        builder.HasIndex(e => new { e.OrganizationId, e.ScheduledFor }).IsUnique().HasFilter("[ScheduledFor] IS NOT NULL");
    }
}

internal sealed class ClubPaymentReminderConfiguration : IEntityTypeConfiguration<ClubPaymentReminder>
{
    public void Configure(EntityTypeBuilder<ClubPaymentReminder> builder)
    {
        builder.ToTable("ClubPaymentReminders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasOne<ClubCharge>().WithMany().HasForeignKey(e => e.ChargeId).OnDelete(DeleteBehavior.NoAction);
        // A charge is reminded of once at each step.
        builder.HasIndex(e => new { e.ChargeId, e.Step }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId });
    }
}

internal sealed class ClubStatementDeliveryConfiguration : IEntityTypeConfiguration<ClubStatementDelivery>
{
    public void Configure(EntityTypeBuilder<ClubStatementDelivery> builder)
    {
        builder.ToTable("ClubStatementDeliveries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.AmountDue).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.PeriodStartUtc });
        // The monthly run sends a member one statement for a month, however often it goes.
        builder.HasIndex(e => new { e.MemberId, e.PeriodStartUtc }, "IX_ClubStatementDeliveries_OneMonthly").IsUnique()
            .HasFilter("[Source] = N'monthly'");
    }
}
