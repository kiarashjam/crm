using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Infrastructure.Services;

// A credit the club gives a member: an entry of its own, never twice for one key (see
// IClubLedger.CreditAsync). Written under the member's house account, as every entry is, so it
// lands whole beside whatever else is posted for them at the same moment.
public sealed partial class ClubLedgerService
{
    public async Task<Result<PaymentDto>> CreditAsync(Guid? organizationId, Guid memberId, decimal amount, string description, string idempotencyKey,
        CancellationToken ct = default)
    {
        if (Money(amount) is not { } credit) return DomainErrors.Club.AmountInvalid;
        var key = Trimmed(idempotencyKey);
        if (key == null || key.Length > 100) return DomainErrors.Club.AmountInvalid;
        // Given already: the first credit is the answer, so a retry or a second approval adds nothing.
        if (await PaymentByKeyAsync(organizationId, key, ct) is { } given) return await PaymentDtoAsync(organizationId, given, ct);

        var member = await MemberAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var account = await AccountAsync(organizationId, member.Id, ct);
        Touch(account);
        var entry = new ClubPayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            EntryType = ClubEntryTypes.Adjustment,
            Amount = credit,
            Currency = account.Currency,
            ReceivedAtUtc = DateTime.UtcNow,
            Description = Truncate(description.Trim(), 500),
            IdempotencyKey = key,
        };
        Stamp(entry);
        var saved = await SaveAsync(DomainErrors.Club.AccountChanged, ct, async () =>
        {
            _db.ClubPayments.Add(entry);
            await StageBalanceAsync(organizationId, member, credit, ct);
        });
        if (!saved.IsSuccess)
        {
            // Lost to the same key at the same moment (the unique index on SQL Server): the
            // credit that won is the answer. Lost to another write for the member: say so.
            return await PaymentByKeyAsync(organizationId, key, ct) is { } winner
                ? await PaymentDtoAsync(organizationId, winner, ct)
                : saved.Error;
        }
        return await PaymentDtoAsync(organizationId, entry, ct);
    }
}
