using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Donations: gifts to the club's causes. They used to be deleted, so a gift entered twice,
/// or one that bounced, simply vanished from the giving totals; now a void takes one back
/// and the record stays, saying why. Saying thank you is the one thing that changes on one.
/// </summary>
public sealed partial class ClubLedgerService
{
    public async Task<IReadOnlyList<DonationDto>> DonationsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _db.ClubDonations.AsNoTracking().Where(d => d.OrganizationId == organizationId).ToListAsync(ct);
        var voids = rows.Where(r => r.ReversesDonationId != null).GroupBy(r => r.ReversesDonationId!.Value).ToDictionary(g => g.Key, g => g.First());
        return rows.Where(r => r.ReversesDonationId == null)
            .Select(r => ToDto(r, voids.GetValueOrDefault(r.Id)))
            .OrderByDescending(d => d.ReceivedAtUtc)
            .ToList();
    }

    public async Task<Result<DonationDto>> RecordDonationAsync(Guid? organizationId, RecordDonationRequest request, CancellationToken ct = default)
    {
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        ClubMember? member = null;
        if (request.MemberId is { } memberId)
        {
            member = await MemberAsync(organizationId, memberId, ct);
            if (member == null) return DomainErrors.Club.MemberNotFound;
        }
        var cause = string.IsNullOrWhiteSpace(request.Cause) ? "Other" : request.Cause.Trim();
        if (cause.Length > 60) return DomainErrors.Club.CauseInvalid;
        if (!ReadDate(request.ReceivedAtUtc, out var received)) return DomainErrors.Club.DateInvalid("The date received");
        var message = Trimmed(request.Message);
        if (message?.Length > 1000) return DomainErrors.Club.TooLong("A message", 1000);

        var now = DateTime.UtcNow;
        var donation = new ClubDonation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member?.Id,
            MemberName = member != null ? NameOf(member) : null,
            EntryType = ClubEntryTypes.Donation,
            Cause = cause,
            Amount = amount,
            Currency = member != null ? await AccountCurrencyAsync(organizationId, member.Id, ct) : await WorkspaceCurrencyAsync(organizationId, ct),
            ReceivedAtUtc = received ?? now,
            IsAnonymous = request.IsAnonymous ?? false,
            Message = message,
            // Saying thank you is its own step, not something a new record arrives with.
            Acknowledged = false,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        _db.ClubDonations.Add(donation);
        var saved = await SaveAsync(DomainErrors.Club.DonationChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(donation, null);
    }

    public async Task<Result<DonationDto>> AcknowledgeDonationAsync(Guid? organizationId, Guid donationId, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var donation = await _db.ClubDonations.FirstOrDefaultAsync(d => d.Id == donationId && d.OrganizationId == organizationId && d.ReversesDonationId == null, ct);
        if (donation == null) return DomainErrors.Club.DonationNotFound;
        var reversal = await _db.ClubDonations.AsNoTracking().FirstOrDefaultAsync(d => d.ReversesDonationId == donationId, ct);
        if (reversal != null) return DomainErrors.Club.DonationVoided;
        if (donation.Acknowledged) return ToDto(donation, null);
        if (expectedVersion is { } version && version != donation.Version) return DomainErrors.Club.DonationChanged;
        donation.Acknowledged = true;
        donation.AcknowledgedAtUtc = DateTime.UtcNow;
        donation.AcknowledgedByName = Who();
        var saved = await SaveAsync(DomainErrors.Club.DonationChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(donation, null);
    }

    public async Task<Result<DonationDto>> VoidDonationAsync(Guid? organizationId, Guid donationId, string? reason, CancellationToken ct = default)
    {
        if (ReadReason(reason, out var why) is { } noReason) return noReason;
        var donation = await _db.ClubDonations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == donationId && d.OrganizationId == organizationId && d.ReversesDonationId == null, ct);
        if (donation == null) return DomainErrors.Club.DonationNotFound;
        if (await _db.ClubDonations.AnyAsync(d => d.ReversesDonationId == donationId, ct)) return DomainErrors.Club.DonationVoided;

        var now = DateTime.UtcNow;
        var reversal = new ClubDonation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = donation.MemberId,
            MemberName = donation.MemberName,
            EntryType = ClubEntryTypes.Void,
            Cause = donation.Cause,
            Amount = -donation.Amount,
            Currency = donation.Currency,
            ReceivedAtUtc = now,
            IsAnonymous = donation.IsAnonymous,
            ReversesDonationId = donation.Id,
            Reason = why,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        _db.ClubDonations.Add(reversal);
        // The unique index on what a void reverses refuses a second void made at the same moment.
        var saved = await SaveAsync(DomainErrors.Club.DonationVoided, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(donation, reversal);
    }

    private static DonationDto ToDto(ClubDonation d, ClubDonation? reversal) => new()
    {
        Id = d.Id,
        MemberId = d.MemberId,
        MemberName = d.MemberName,
        Cause = d.Cause,
        Amount = d.Amount,
        Currency = d.Currency,
        ReceivedAtUtc = d.ReceivedAtUtc,
        IsAnonymous = d.IsAnonymous,
        Message = d.Message,
        Acknowledged = d.Acknowledged,
        AcknowledgedAtUtc = d.AcknowledgedAtUtc,
        Status = reversal != null ? "Voided" : null,
        VoidReason = reversal?.Reason,
        VoidedAtUtc = reversal?.ReceivedAtUtc,
        VoidedByName = reversal?.CreatedByName,
        CreatedByName = d.CreatedByName,
        Version = d.Version,
    };
}
