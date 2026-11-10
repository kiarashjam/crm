using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.Payments;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IClubQrBills"/>
/// <remarks>
/// <para>
/// A statement's reference is issued the first time the statement is printed and kept
/// (ClubPaymentReference), so the same statement always carries the same reference, and a
/// bank transfer quoting it is matched to the member (IClubBankFiles). Numbers are the
/// workspace's own sequence; the database refuses a number, or a statement, taken twice, and
/// a print that loses that race reads the winner's row.
/// </para>
/// <para>
/// No slip is better than a wrong one: with the bank details incomplete, nothing due, or a
/// bill that would not validate, the statement is printed without one, and the settings page
/// says why.
/// </para>
/// </remarks>
public sealed class ClubQrBillService : IClubQrBills
{
    public const string StatementPurpose = "statement";
    public const string ChargePurpose = "charge";

    private readonly AppDbContext _db;
    private readonly ClubPaymentSettingsService _settings;
    private readonly ILogger<ClubQrBillService> _logger;

    public ClubQrBillService(AppDbContext db, ClubPaymentSettingsService settings, ILogger<ClubQrBillService> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    public async Task<QrBill?> ForStatementAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, decimal amountDue, string currency, CancellationToken ct = default)
    {
        if (amountDue <= 0 || currency is not ("CHF" or "EUR")) return null;
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (config.QrBillProblem != null || config.Creditor is not { } creditor) return null;

        var reference = await IssueAsync(organizationId, config, memberId, fromUtc, toUtc, amountDue, currency, ct);
        if (reference == null) return null;
        var qr = config.QrIban != null;
        var bill = new QrBill(
            qr ? config.QrIban! : config.Iban!,
            creditor,
            Math.Round(amountDue, 2, MidpointRounding.AwayFromZero),
            currency,
            null,
            qr ? SwissQrBill.Qrr : SwissQrBill.Scor,
            qr ? reference.QrReference : reference.ScorReference,
            $"Statement {PeriodWords(fromUtc, toUtc, RegionalSettings.ZoneOf(config.TimeZone))}");
        var problems = SwissQrBill.Validate(bill);
        if (problems.Count > 0)
        {
            _logger.LogWarning("The QR-bill for member {MemberId} was left off the statement: {Problems}", memberId, string.Join(" ", problems));
            return null;
        }
        return bill;
    }

    public async Task<Result<PaymentReferenceDto>> FindAsync(Guid organizationId, string reference, CancellationToken ct = default)
    {
        var row = await FindRowAsync(organizationId, reference, ct);
        if (row == null) return DomainErrors.Payments.ReferenceNotFound;
        var name = await _db.ClubMembers.AsNoTracking().Where(m => m.Id == row.MemberId && m.OrganizationId == organizationId)
            .Select(m => (m.FirstName + " " + m.LastName).Trim()).FirstOrDefaultAsync(ct);
        var compact = PaymentReferences.Compact(reference);
        return new PaymentReferenceDto(row.Id, compact == row.QrReference ? row.QrReference! : row.ScorReference,
            compact == row.QrReference ? SwissQrBill.Qrr : SwissQrBill.Scor, row.MemberId, name, row.Purpose,
            row.PeriodStartUtc, row.PeriodEndUtc, row.ChargeId, row.AmountDue, row.Currency, row.CreatedAtUtc);
    }

    /// <summary>The issued reference a bank transfer quotes, QR or creditor reference, spaces or not.</summary>
    public async Task<ClubPaymentReference?> FindRowAsync(Guid organizationId, string? reference, CancellationToken ct)
    {
        var compact = PaymentReferences.Compact(reference);
        if (compact.Length == 0) return null;
        return await _db.ClubPaymentReferences.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && (r.QrReference == compact || r.ScorReference == compact), ct);
    }

    /// <summary>The statement's reference: the one issued before, or a new number.</summary>
    private async Task<ClubPaymentReference?> IssueAsync(Guid organizationId, PaymentConfig config, Guid memberId, DateTime fromUtc, DateTime toUtc,
        decimal amountDue, string currency, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var existing = await _db.ClubPaymentReferences.FirstOrDefaultAsync(r => r.OrganizationId == organizationId
                && r.Purpose == StatementPurpose && r.MemberId == memberId && r.PeriodStartUtc == fromUtc && r.PeriodEndUtc == toUtc, ct);
            if (existing != null)
            {
                // Printed before the club had a QR-IBAN: it gets its QR reference now, from the same number.
                if (existing.QrReference == null && config.QrIban != null)
                {
                    existing.QrReference = PaymentReferences.QrReference(config.QrReferencePrefix, existing.Number);
                    if (existing.QrReference == null) return null;
                    if (!await TrySaveAsync(ct)) continue;
                }
                return existing;
            }

            var number = (await _db.ClubPaymentReferences.Where(r => r.OrganizationId == organizationId)
                .Select(r => (long?)r.Number).MaxAsync(ct) ?? 0) + 1;
            string? qrReference = null;
            if (config.QrIban != null)
            {
                qrReference = PaymentReferences.QrReference(config.QrReferencePrefix, number);
                if (qrReference == null)
                {
                    _logger.LogWarning("Workspace {OrganizationId}'s QR reference prefix leaves no room for reference {Number}", organizationId, number);
                    return null;
                }
            }
            var row = new ClubPaymentReference
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Number = number,
                QrReference = qrReference,
                ScorReference = PaymentReferences.CreditorReference(number),
                MemberId = memberId,
                Purpose = StatementPurpose,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = toUtc,
                AmountDue = amountDue,
                Currency = currency,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.ClubPaymentReferences.Add(row);
            if (await TrySaveAsync(ct)) return row;
        }
        _logger.LogWarning("No QR reference could be issued for member {MemberId}: the numbers kept being taken", memberId);
        return null;
    }

    /// <summary>Saves, or lets go of the save when another print took the number or the statement first.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var entry in _db.ChangeTracker.Entries<ClubPaymentReference>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>"October 2026" for a calendar month in the club's time zone; otherwise its first and last day.</summary>
    public static string PeriodWords(DateTime fromUtc, DateTime toUtc, TimeZoneInfo zone)
    {
        var from = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), zone);
        var to = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), zone);
        if (from.Day == 1 && from.TimeOfDay == TimeSpan.Zero && to == from.AddMonths(1))
            return from.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return $"{from.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} to {to.AddTicks(-1).ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }
}
