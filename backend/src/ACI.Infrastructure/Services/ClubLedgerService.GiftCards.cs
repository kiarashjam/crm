using System.Security.Cryptography;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Gift cards: each card's value is the sum of its transactions (issue, redeem, top-up,
/// expiry, void), so what was left on it at any date can be read back.
/// </summary>
/// <remarks>
/// The card row is touched by every transaction, and its Version makes two terminals
/// spending the same card at the same moment one redemption and a 409: both read 50 left,
/// and only the first save lands.
/// </remarks>
public sealed partial class ClubLedgerService
{
    public async Task<IReadOnlyList<GiftCardDto>> GiftCardsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var cards = await _db.ClubGiftCards.AsNoTracking().Where(c => c.OrganizationId == organizationId).ToListAsync(ct);
        var transactions = await _db.ClubGiftCardTransactions.AsNoTracking().Where(t => t.OrganizationId == organizationId).ToListAsync(ct);
        var byCard = transactions.GroupBy(t => t.GiftCardId).ToDictionary(g => g.Key, g => g.ToList());
        var now = DateTime.UtcNow;
        return cards.Select(c => ToDto(c, byCard.GetValueOrDefault(c.Id) ?? [], now))
            .OrderByDescending(c => c.IssuedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<GiftCardRedemptionDto>> GiftCardRedemptionsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _db.ClubGiftCardTransactions.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.Type == GiftCardTransactionTypes.Redeem)
            .Join(_db.ClubGiftCards, t => t.GiftCardId, c => c.Id, (t, c) => new { Transaction = t, c.Code })
            .ToListAsync(ct);
        return rows.Select(r => ToRedemption(r.Transaction, r.Code)).OrderByDescending(r => r.RedeemedAtUtc).ToList();
    }

    /// <summary>A card by its code, for a till that is handed one: no list of every code has to reach it.</summary>
    public async Task<Result<GiftCardDto>> FindGiftCardAsync(Guid? organizationId, string code, CancellationToken ct = default)
    {
        var wanted = (code ?? "").Trim().ToUpperInvariant();
        if (wanted.Length == 0) return DomainErrors.Club.GiftCardCodeRequired;
        var card = await _db.ClubGiftCards.AsNoTracking().FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Code == wanted, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        return ToDto(card, await TransactionsAsync(card.Id, ct), DateTime.UtcNow);
    }

    public async Task<Result<GiftCardDto>> IssueGiftCardAsync(Guid? organizationId, IssueGiftCardRequest request, CancellationToken ct = default)
    {
        if (Money(request.FaceValue) is not { } face) return DomainErrors.Club.AmountInvalid;
        if (!ReadDate(request.ExpiresAtUtc, out var expires)) return DomainErrors.Club.ExpiryInvalid;
        if (expires is { } e && e <= DateTime.UtcNow) return DomainErrors.Club.ExpiryInvalid;
        var purchaser = (request.PurchaserName ?? "").Trim();
        var recipient = (request.RecipientName ?? "").Trim();
        if (purchaser.Length > 200) return DomainErrors.Club.TooLong("A purchaser's name", 200);
        if (recipient.Length > 200) return DomainErrors.Club.TooLong("A recipient's name", 200);
        var email = Trimmed(request.RecipientEmail);
        if (email?.Length > 256) return DomainErrors.Club.TooLong("An email", 256);
        var message = Trimmed(request.Message);
        if (message?.Length > 1000) return DomainErrors.Club.TooLong("A message", 1000);
        if (request.PurchaserMemberId is { } purchaserId && await MemberAsync(organizationId, purchaserId, ct) == null)
            return DomainErrors.Club.MemberNotFound;

        var now = DateTime.UtcNow;
        var currency = await WorkspaceCurrencyAsync(organizationId, ct);
        var card = new ClubGiftCard
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Code = await NewGiftCodeAsync(organizationId, ct),
            FaceValue = face,
            Currency = currency,
            PurchaserMemberId = request.PurchaserMemberId,
            PurchaserName = purchaser,
            RecipientName = recipient,
            RecipientEmail = email,
            Message = message,
            IssuedAtUtc = now,
            ExpiresAtUtc = expires,
            Status = "Active",
            LastTransactionAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        var issue = NewTransaction(card, GiftCardTransactionTypes.Issue, face);
        _db.ClubGiftCards.Add(card);
        _db.ClubGiftCardTransactions.Add(issue);
        var saved = await SaveAsync(DomainErrors.Club.GiftCardChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(card, [issue], now);
    }

    public async Task<Result<GiftCardRedemptionDto>> RedeemGiftCardAsync(Guid? organizationId, Guid giftCardId, RedeemGiftCardRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (ReadKey(idempotencyKey, out var key) is { } badKey) return badKey;
        // A retry after a timeout, say, carries the key of the try that already went through.
        if (key != null && await RedemptionByKeyAsync(organizationId, giftCardId, key, ct) is { } earlier) return earlier;

        var card = await _db.ClubGiftCards.FirstOrDefaultAsync(c => c.Id == giftCardId && c.OrganizationId == organizationId, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        if (card.Status == "Voided") return DomainErrors.Club.GiftCardNotActive("Voided");
        // Only the clock says a card has expired, so it is checked here rather than trusted to a status.
        if (card.ExpiresAtUtc is { } expires && expires <= DateTime.UtcNow) return DomainErrors.Club.GiftCardExpired(expires);
        if (Money(request.Amount) is not { } asked) return DomainErrors.Club.AmountInvalid;
        var appliedTo = Trimmed(request.AppliedTo);
        if (appliedTo?.Length > 500) return DomainErrors.Club.TooLong("What it paid for", 500);
        var redeemedBy = Trimmed(request.RedeemedByName) ?? Who();
        if (redeemedBy?.Length > 200) return DomainErrors.Club.TooLong("A name", 200);

        var left = (await TransactionsAsync(card.Id, ct)).Sum(t => t.Amount);
        var amount = Math.Min(asked, left);
        if (amount <= 0) return DomainErrors.Club.GiftCardNotActive("Redeemed");
        await _reads.ReadAsync("gift-cards", ct);

        var redemption = NewTransaction(card, GiftCardTransactionTypes.Redeem, -amount);
        redemption.AppliedTo = appliedTo;
        redemption.RedeemedByName = redeemedBy;
        redemption.IdempotencyKey = key;
        // The card is saved first, checked against the card this request read: of two
        // terminals spending it at once, the second is refused before it spends anything.
        card.LastTransactionAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(DomainErrors.Club.GiftCardChanged, ct, () =>
        {
            _db.ClubGiftCardTransactions.Add(redemption);
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess)
        {
            if (key != null && await RedemptionByKeyAsync(organizationId, giftCardId, key, ct) is { } raced) return raced;
            return saved.Error;
        }
        return ToRedemption(redemption, card.Code);
    }

    public async Task<Result<GiftCardDto>> TopUpGiftCardAsync(Guid? organizationId, Guid giftCardId, TopUpGiftCardRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (ReadKey(idempotencyKey, out var key) is { } badKey) return badKey;
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var reason = Trimmed(request.Reason);
        if (reason?.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        var card = await _db.ClubGiftCards.FirstOrDefaultAsync(c => c.Id == giftCardId && c.OrganizationId == organizationId, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        if (key != null && await _db.ClubGiftCardTransactions.AnyAsync(t => t.OrganizationId == organizationId && t.IdempotencyKey == key, ct))
            return ToDto(card, await TransactionsAsync(card.Id, ct), DateTime.UtcNow);
        if (card.Status == "Voided") return DomainErrors.Club.GiftCardNotActive("Voided");
        if (card.ExpiresAtUtc is { } expires && expires <= DateTime.UtcNow) return DomainErrors.Club.GiftCardExpired(expires);
        await _reads.ReadAsync("gift-cards", ct);

        var topUp = NewTransaction(card, GiftCardTransactionTypes.TopUp, amount);
        topUp.Reason = reason;
        topUp.IdempotencyKey = key;
        card.LastTransactionAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(DomainErrors.Club.GiftCardChanged, ct, () =>
        {
            _db.ClubGiftCardTransactions.Add(topUp);
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(card, await TransactionsAsync(card.Id, ct), DateTime.UtcNow);
    }

    public async Task<Result<GiftCardDto>> VoidGiftCardAsync(Guid? organizationId, Guid giftCardId, string? reason, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        if (ReadReason(reason, out var why) is { } noReason) return noReason;
        var card = await _db.ClubGiftCards.FirstOrDefaultAsync(c => c.Id == giftCardId && c.OrganizationId == organizationId, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        if (card.Status == "Voided") return DomainErrors.Club.GiftCardVoided;
        if (expectedVersion is { } version && version != card.Version) return DomainErrors.Club.GiftCardChanged;
        var left = (await TransactionsAsync(card.Id, ct)).Sum(t => t.Amount);
        await _reads.ReadAsync("gift-cards", ct);

        // What was left on the card is written off, and the void says how much.
        var writeOff = NewTransaction(card, GiftCardTransactionTypes.Void, -Math.Max(0, left));
        writeOff.Reason = why;
        card.Status = "Voided";
        card.LastTransactionAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(DomainErrors.Club.GiftCardChanged, ct, () =>
        {
            _db.ClubGiftCardTransactions.Add(writeOff);
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(card, await TransactionsAsync(card.Id, ct), DateTime.UtcNow);
    }

    public async Task<Result<decimal>> GiftCardBalanceAsync(Guid? organizationId, Guid giftCardId, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var card = await _db.ClubGiftCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == giftCardId && c.OrganizationId == organizationId, ct);
        if (card == null) return DomainErrors.Club.GiftCardNotFound;
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var transactions = (await TransactionsAsync(card.Id, ct)).Where(t => t.AtUtc <= asOf).ToList();
        // A card past its expiry is worth nothing from then on, whether or not the expiry job
        // has written that off yet.
        if (card.ExpiresAtUtc is { } expires && expires <= asOf && transactions.All(t => t.Type != GiftCardTransactionTypes.Expiry))
            return 0m;
        return transactions.Sum(t => t.Amount);
    }

    public async Task<int> ExpireGiftCardsAsync(DateTime asOfUtc, CancellationToken ct = default)
    {
        // Across every workspace: this is the expiry job's, which acts for none. Each
        // transaction is written in its card's own organization.
        var due = await _db.ClubGiftCards.IgnoreQueryFilters()
            .Where(c => c.Status == "Active" && c.ExpiresAtUtc != null && c.ExpiresAtUtc <= asOfUtc)
            .Where(c => !_db.ClubGiftCardTransactions.IgnoreQueryFilters().Any(t => t.GiftCardId == c.Id && t.Type == GiftCardTransactionTypes.Expiry))
            .OrderBy(c => c.ExpiresAtUtc)
            .Take(500)
            .ToListAsync(ct);
        var expired = 0;
        foreach (var card in due)
        {
            var left = await _db.ClubGiftCardTransactions.IgnoreQueryFilters().Where(t => t.GiftCardId == card.Id).SumAsync(t => t.Amount, ct);
            var expiry = NewTransaction(card, GiftCardTransactionTypes.Expiry, -Math.Max(0, left));
            expiry.AtUtc = card.ExpiresAtUtc!.Value;
            expiry.CreatedByUserId = null;
            expiry.CreatedByName = null;
            card.LastTransactionAtUtc = DateTime.UtcNow;
            // One card at a time: a card redeemed at the same moment is left for the next run.
            var saved = await SaveAsync(DomainErrors.Club.GiftCardChanged, ct, () =>
            {
                _db.ClubGiftCardTransactions.Add(expiry);
                return Task.CompletedTask;
            });
            if (saved.IsSuccess) expired++;
        }
        return expired;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<List<ClubGiftCardTransaction>> TransactionsAsync(Guid giftCardId, CancellationToken ct) =>
        await _db.ClubGiftCardTransactions.AsNoTracking().Where(t => t.GiftCardId == giftCardId).ToListAsync(ct);

    private async Task<GiftCardRedemptionDto?> RedemptionByKeyAsync(Guid? organizationId, Guid giftCardId, string key, CancellationToken ct)
    {
        var row = await _db.ClubGiftCardTransactions.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.GiftCardId == giftCardId && t.IdempotencyKey == key && t.Type == GiftCardTransactionTypes.Redeem)
            .Join(_db.ClubGiftCards, t => t.GiftCardId, c => c.Id, (t, c) => new { Transaction = t, c.Code })
            .FirstOrDefaultAsync(ct);
        return row == null ? null : ToRedemption(row.Transaction, row.Code);
    }

    private ClubGiftCardTransaction NewTransaction(ClubGiftCard card, string type, decimal amount)
    {
        var now = DateTime.UtcNow;
        return new ClubGiftCardTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = card.OrganizationId,
            GiftCardId = card.Id,
            Type = type,
            Amount = amount,
            Currency = card.Currency,
            AtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
    }

    /// <summary>A code no other card in this workspace has.</summary>
    private async Task<string> NewGiftCodeAsync(Guid? organizationId, CancellationToken ct)
    {
        string code;
        do code = GiftCode();
        while (await _db.ClubGiftCards.AnyAsync(c => c.OrganizationId == organizationId && c.Code == code, ct));
        return code;
    }

    private static string GiftCode()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> raw = stackalloc char[12];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return $"GIFT-{new string(raw[..4])}-{new string(raw[4..8])}-{new string(raw[8..])}";
    }

    private static GiftCardDto ToDto(ClubGiftCard card, IReadOnlyList<ClubGiftCardTransaction> transactions, DateTime now)
    {
        var left = transactions.Sum(t => t.Amount);
        var voided = transactions.FirstOrDefault(t => t.Type == GiftCardTransactionTypes.Void);
        var expiry = transactions.FirstOrDefault(t => t.Type == GiftCardTransactionTypes.Expiry);
        var status = card.Status == "Voided" ? "Voided"
            : expiry != null || card.ExpiresAtUtc is { } expires && expires <= now ? "Expired"
            : left <= 0 ? "Redeemed"
            : "Active";
        return new GiftCardDto
        {
            Id = card.Id,
            Code = card.Code,
            PurchaserMemberId = card.PurchaserMemberId,
            PurchaserName = card.PurchaserName,
            RecipientName = card.RecipientName,
            RecipientEmail = card.RecipientEmail,
            Message = card.Message,
            FaceValue = card.FaceValue,
            RemainingValue = left,
            ToppedUpValue = transactions.Where(t => t.Type == GiftCardTransactionTypes.TopUp).Sum(t => t.Amount),
            Currency = card.Currency,
            Status = status,
            IssuedAtUtc = card.IssuedAtUtc,
            ExpiresAtUtc = card.ExpiresAtUtc,
            WrittenOffValue = voided != null ? -voided.Amount : null,
            ExpiredValue = expiry != null ? -expiry.Amount : null,
            VoidReason = voided?.Reason,
            VoidedAtUtc = voided?.AtUtc,
            VoidedByUserId = voided?.CreatedByUserId,
            VoidedByName = voided?.CreatedByName,
            CreatedByName = card.CreatedByName,
            Version = card.Version,
        };
    }

    private static GiftCardRedemptionDto ToRedemption(ClubGiftCardTransaction t, string code) => new()
    {
        Id = t.Id,
        GiftCardId = t.GiftCardId,
        GiftCardCode = code,
        Amount = -t.Amount,
        RedeemedByName = t.RedeemedByName ?? t.CreatedByName ?? string.Empty,
        RedeemedAtUtc = t.AtUtc,
        AppliedTo = t.AppliedTo,
    };
}
