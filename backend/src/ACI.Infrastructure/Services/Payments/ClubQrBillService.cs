using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
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
/// bank transfer quoting it is matched to the member (IClubBankFiles). An invoice for one
/// charge gets a reference of its own the same way, which also names the charge, so the
/// transfer pays that charge first. Numbers are the
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

    /// <summary>
    /// A merged duplicate's statement reference, where the member kept has one of their own for
    /// the same period: still found by a transfer that quotes it, never printed again.
    /// </summary>
    public const string MergedStatementPurpose = "statement-merged";

    private readonly AppDbContext _db;
    private readonly ClubPaymentSettingsService _settings;
    private readonly ILedgerReadObserver _reads;
    private readonly ILogger<ClubQrBillService> _logger;

    public ClubQrBillService(AppDbContext db, ClubPaymentSettingsService settings, ILedgerReadObserver reads, ILogger<ClubQrBillService> logger)
    {
        _db = db;
        _settings = settings;
        _reads = reads;
        _logger = logger;
    }

    public async Task<QrBill?> ForStatementAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, decimal amountDue, string currency,
        string? language = null, CancellationToken ct = default)
    {
        if (amountDue <= 0 || currency is not ("CHF" or "EUR")) return null;
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (config.QrBillProblem != null || config.Creditor == null) return null;
        var reference = await IssueAsync(organizationId, config, memberId, new Key(StatementPurpose, fromUtc, toUtc, null), amountDue, currency, ct);
        var zone = RegionalSettings.ZoneOf(config.TimeZone);
        // The message on the slip is in the statement's language; English reads as it always did.
        var message = AppLanguages.Normalise(language) is { } words && words != AppLanguages.English
            ? MemberTexts.T(words, "statement.qrMessage", ("period", MemberTexts.Period(words, config.Locale,
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), zone),
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), zone))))
            : $"Statement {PeriodWords(fromUtc, toUtc, zone)}";
        return Bill(config, reference, amountDue, currency, message, memberId);
    }

    public async Task<QrBill?> ForChargeAsync(Guid organizationId, Guid memberId, Guid chargeId, decimal amountDue, string currency, string message, CancellationToken ct = default)
    {
        if (amountDue <= 0 || currency is not ("CHF" or "EUR")) return null;
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (config.QrBillProblem != null || config.Creditor == null) return null;
        var reference = await IssueAsync(organizationId, config, memberId, new Key(ChargePurpose, null, null, chargeId), amountDue, currency, ct);
        return Bill(config, reference, amountDue, currency, message, memberId);
    }

    /// <summary>The bill for an issued reference; none when it would not validate (and why is logged).</summary>
    private QrBill? Bill(PaymentConfig config, ClubPaymentReference? reference, decimal amountDue, string currency, string message, Guid memberId)
    {
        if (reference == null || config.Creditor is not { } creditor) return null;
        var qr = config.QrIban != null;
        var bill = new QrBill(
            qr ? config.QrIban! : config.Iban!,
            creditor,
            Math.Round(amountDue, 2, MidpointRounding.AwayFromZero),
            currency,
            null,
            qr ? SwissQrBill.Qrr : SwissQrBill.Scor,
            qr ? reference.QrReference : reference.ScorReference,
            message);
        var problems = SwissQrBill.Validate(bill);
        if (problems.Count > 0)
        {
            _logger.LogWarning("The QR-bill for member {MemberId} was left off: {Problems}", memberId, string.Join(" ", problems));
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

    /// <summary>What a reference is for: a member's statement of a period, or one charge.</summary>
    private sealed record Key(string Purpose, DateTime? FromUtc, DateTime? ToUtc, Guid? ChargeId);

    /// <summary>The reference for a statement or a charge: the one issued before, or a new number.</summary>
    private async Task<ClubPaymentReference?> IssueAsync(Guid organizationId, PaymentConfig config, Guid memberId, Key key,
        decimal amountDue, string currency, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var issued = _db.ClubPaymentReferences.Where(r => r.OrganizationId == organizationId && r.Purpose == key.Purpose);
            issued = key.ChargeId is { } chargeId
                ? issued.Where(r => r.ChargeId == chargeId)
                : issued.Where(r => r.MemberId == memberId && r.PeriodStartUtc == key.FromUtc && r.PeriodEndUtc == key.ToUtc);
            var existing = await issued.FirstOrDefaultAsync(ct);
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
            await _reads.ReadAsync("payment-references", ct);
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
                Purpose = key.Purpose,
                PeriodStartUtc = key.FromUtc,
                PeriodEndUtc = key.ToUtc,
                ChargeId = key.ChargeId,
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
