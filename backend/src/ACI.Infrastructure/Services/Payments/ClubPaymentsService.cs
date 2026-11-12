using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IClubPayments"/>
/// <remarks>
/// <para>
/// Money only ever moves on Stripe's side, on Stripe's own pages (Checkout) or by charging a
/// card the member saved there; this service keeps where each attempt stands
/// (ClubOnlinePayment) and records a payment that went through on the ledger, once, under
/// Stripe's id for it ("stripe:pi_…"). Whichever comes first, the answer to a charge or
/// Stripe's webhook, records it; the other finds it recorded.
/// </para>
/// <para>
/// A charge of a saved card is claimed before Stripe is asked (a unique key per charge and
/// try), and Stripe is asked with that claim as its Idempotency-Key, so a double click, a
/// retry or two servers charge a card once.
/// </para>
/// </remarks>
public sealed partial class ClubPaymentsService : IClubPayments
{
    public const string Account = "account";
    public const string Charges = "charges";
    public const string Setup = "setup";

    private static readonly TimeSpan PayLinkLifetime = TimeSpan.FromDays(60);
    private static readonly TimeSpan SetupLinkLifetime = TimeSpan.FromDays(14);

    /// <summary>Stripe's page lasts at most a day; a member who opens it again later gets a new one.</summary>
    private static readonly TimeSpan CheckoutLifetime = TimeSpan.FromHours(23);

    /// <summary>Dues one run of the payment job charges, across workspaces; the rest go on the next run.</summary>
    public const int MaxAutoPayPerRun = 200;

    /// <summary>The most charges a pay link, or a charge of the saved card, may name: what a link's token and the attempt's list hold.</summary>
    public const int MaxChargesNamed = 50;

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubBilling _billing;
    private readonly ClubPaymentSettingsService _settings;
    private readonly IEnumerable<IPaymentProvider> _providers;
    private readonly PayLinkSigner _links;
    private readonly IOutbox _outbox;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserService _user;
    private readonly EmailSettings _email;
    private readonly ILogger<ClubPaymentsService> _logger;

    public ClubPaymentsService(AppDbContext db, IClubLedger ledger, IClubBilling billing, ClubPaymentSettingsService settings,
        IEnumerable<IPaymentProvider> providers, PayLinkSigner links, IOutbox outbox, ITenantContext tenant, ICurrentUserService user,
        IOptions<EmailSettings> email, ILogger<ClubPaymentsService> logger)
    {
        _db = db;
        _ledger = ledger;
        _billing = billing;
        _settings = settings;
        _providers = providers;
        _links = links;
        _outbox = outbox;
        _tenant = tenant;
        _user = user;
        _email = email.Value;
        _logger = logger;
    }

    private IPaymentProvider Stripe => _providers.First(p => p.Name == PaymentProviders.Stripe);

    // ── pay links ────────────────────────────────────────────────────────────

    public async Task<Result<PayLinkDto>> CreatePayLinkAsync(Guid organizationId, Guid userId, Guid memberId, PayLinkRequest request, CancellationToken ct = default)
    {
        var made = await MakeLinkAsync(organizationId, memberId, request, ct);
        if (!made.IsSuccess) return made.Error;
        return made.Value.Link;
    }

    public async Task<Result<PayLinkDto>> EmailPayLinkAsync(Guid organizationId, Guid userId, Guid memberId, PayLinkRequest request, CancellationToken ct = default)
    {
        var made = await MakeLinkAsync(organizationId, memberId, request, ct);
        if (!made.IsSuccess) return made.Error;
        var (link, member, config) = made.Value;
        var address = (member.Email ?? "").Trim();
        if (!ValidationHelper.IsValidEmail(address)) return DomainErrors.Club.MemberNoEmail;

        var culture = Culture(config.Locale);
        var money = $"{link.Currency} {link.Amount.ToString("N2", culture)}";
        var firstName = string.IsNullOrWhiteSpace(member.FirstName) ? NameOf(member) : member.FirstName.Trim();
        var setup = link.Purpose == Setup;
        var subject = setup ? $"Save a card with {config.ClubName}" : $"Pay your account with {config.ClubName}";
        var body = new StringBuilder();
        body.Append("Dear ").Append(firstName).Append(",\n\n");
        if (setup)
        {
            body.Append(request.AutoPay
                ? "You can save a card for your membership dues, which are then charged to it when they are billed. "
                : "You can save a card with the club for paying your account. ");
            body.Append("The card is entered on Stripe's secure page; the club never sees its number.\n\n");
        }
        else
        {
            body.Append("You can pay ").Append(money).Append(" online, by card")
                .Append(config.MethodsFor(link.Currency).Contains("twint") ? " or TWINT" : "")
                .Append(config.MethodsFor(link.Currency).Contains("sepa_debit") ? " or SEPA direct debit" : "")
                .Append(", on Stripe's secure page.\n\n");
        }
        body.Append(link.Url).Append("\n\n");
        body.Append("The link works until ").Append(link.ExpiresAtUtc.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)).Append(".\n\n");
        body.Append(config.ClubName);

        var log = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Email = address,
            Channel = CampaignChannels.Email,
            Category = "Dues",
            Purpose = ConsentPurposes.Service,
            Subject = subject,
            Preview = Clip(body.ToString(), 500),
            Status = MemberMessageStatuses.Queued,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.MemberMessages.Add(log);
        await _db.SaveChangesAsync(ct);
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.PaymentLink,
            SourceId = member.Id,
            To = [address],
            RecipientName = NameOf(member),
            Subject = subject,
            Body = body.ToString(),
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            MemberId = member.Id,
            Purpose = ConsentPurposes.Service,
        }, ct);
        if (queued.IsSuccess) log.OutboundMessageId = queued.Value.Id;
        else
        {
            log.Status = MemberMessageStatuses.Suppressed;
            log.Reason = Clip(queued.Error.Description, 500);
        }
        await _db.SaveChangesAsync(ct);
        return link with { EmailStatus = queued.IsSuccess ? "queued" : "suppressed", EmailReason = queued.IsSuccess ? null : queued.Error.Description };
    }

    public async Task<Result<PublicPayPageDto>> ReadPayLinkAsync(string token, CancellationToken ct = default)
    {
        if (!_links.TryRead(token, out var claim) || claim == null) return DomainErrors.Payments.LinkInvalid;
        // The link names its workspace; a public page acts for it alone.
        using var acting = _tenant.ActFor(claim.OrganizationId, null);
        var member = await MemberAsync(claim.OrganizationId, claim.MemberId, ct);
        if (member == null) return DomainErrors.Payments.LinkInvalid;
        var config = await _settings.ConfigAsync(claim.OrganizationId, ct);
        var currency = await AccountCurrencyAsync(claim.OrganizationId, member.Id, config, ct);
        var firstName = string.IsNullOrWhiteSpace(member.FirstName) ? NameOf(member) : member.FirstName.Trim();

        if (claim.Purpose == Setup)
            return new PublicPayPageDto(config.ClubName, firstName, Setup,
                claim.AutoPay ? "Save a card for your membership dues" : "Save a card for paying your account",
                0, currency, config.OnlinePayments ? config.SaveableFor(currency) : [], config.OnlinePayments,
                config.OnlinePayments ? null : "The club cannot take cards online just now. Please ask the club.", claim.AutoPay);

        var (amount, description) = await WhatIsOwedAsync(claim.OrganizationId, member.Id, claim, config, ct);
        var reason = !config.OnlinePayments ? "The club cannot take payments online just now. Please ask the club."
            : amount <= 0 ? "There is nothing to pay: everything this link was for is paid."
            : currency is not { Length: 3 } ? "The account's currency cannot be paid online." : null;
        return new PublicPayPageDto(config.ClubName, firstName, claim.Purpose, description, amount, currency,
            config.OnlinePayments ? config.MethodsFor(currency) : [], reason == null, reason, false);
    }

    public async Task<Result<CheckoutStartedDto>> StartCheckoutAsync(string token, CancellationToken ct = default)
    {
        if (!_links.TryRead(token, out var claim) || claim == null) return DomainErrors.Payments.LinkInvalid;
        using var acting = _tenant.ActFor(claim.OrganizationId, null);
        var member = await MemberAsync(claim.OrganizationId, claim.MemberId, ct);
        if (member == null) return DomainErrors.Payments.LinkInvalid;
        var config = await _settings.ConfigAsync(claim.OrganizationId, ct);
        if (!config.OnlinePayments) return config.StripeKeyUnreadable ? DomainErrors.Payments.KeyUnreadable : DomainErrors.Payments.NotSetUp;
        var currency = await AccountCurrencyAsync(claim.OrganizationId, member.Id, config, ct);

        decimal amount = 0;
        string description;
        IReadOnlyList<Guid> chargeIds = [];
        if (claim.Purpose == Setup)
        {
            description = $"{config.ClubName}: a card for {(claim.AutoPay ? "membership dues" : "your account")}";
        }
        else
        {
            (amount, description) = await WhatIsOwedAsync(claim.OrganizationId, member.Id, claim, config, ct);
            if (amount <= 0) return DomainErrors.Payments.NothingToPay;
            // A link for what is owed names no charges: the ledger pays the open ones oldest first
            // when the money comes in. Listing every one overflowed the column for a member with
            // a season of bar tabs, and Pay failed.
            if (claim.Purpose == Charges) chargeIds = claim.ChargeIds;
        }
        var purpose = claim.Purpose == Setup ? OnlinePaymentPurposes.SaveCard : OnlinePaymentPurposes.Checkout;
        var chargeText = Join(chargeIds);

        // Pressing Pay twice, or opening the link again within the hour, finds the page already
        // made for the same amount rather than making Stripe a new one each time.
        var now = DateTime.UtcNow;
        var open = await _db.ClubOnlinePayments.AsNoTracking()
            .Where(p => p.OrganizationId == claim.OrganizationId && p.MemberId == member.Id && p.Purpose == purpose
                && p.Status == OnlinePaymentStatuses.Open && p.CheckoutUrl != null && p.Amount == amount && p.CreatedAtUtc > now.AddHours(-1))
            .OrderByDescending(p => p.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (open != null && (open.ChargeIds ?? "") == chargeText && open.ExpiresAtUtc > now.AddMinutes(10)) return new CheckoutStartedDto(open.CheckoutUrl!);

        var customer = await CustomerAsync(config, member, ct);
        if (!customer.IsSuccess) return customer.Error;

        var attempt = NewAttempt(claim.OrganizationId, member, purpose, amount, currency, description, chargeText);
        _db.ClubOnlinePayments.Add(attempt);
        await _db.SaveChangesAsync(ct);

        var metadata = Metadata(claim.OrganizationId, member.Id, attempt.Id, claim.AutoPay);
        var page = $"{FrontendBase()}/pay/{Uri.EscapeDataString(token)}";
        var started = await Stripe.CreateCheckoutAsync(config.StripeKey!, new ProviderCheckoutRequest(
            claim.Purpose == Setup ? "setup" : "payment",
            amount,
            currency,
            description,
            customer.Value,
            member.Email,
            claim.Purpose == Setup ? config.SaveableFor(currency) : config.MethodsFor(currency),
            $"{page}?status=done",
            $"{page}?status=cancelled",
            now.Add(CheckoutLifetime),
            metadata,
            attempt.Id.ToString(),
            $"cadence-checkout-{attempt.Id:N}",
            claim.Purpose == Setup && claim.AutoPay
                ? $"By saving your card you agree that {config.ClubName} charges your membership dues to it when they are billed. You can ask the club to stop at any time."
                : null), ct);
        if (started.Failure is { } failure)
        {
            attempt.Status = OnlinePaymentStatuses.Failed;
            attempt.FailureCode = Clip(failure.Code, 100);
            attempt.FailureMessage = Clip(failure.Message, 500);
            attempt.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return failure.Unreachable ? DomainErrors.Payments.ProviderUnreachable(failure.Message) : DomainErrors.Payments.ProviderRefused(failure.Message);
        }
        attempt.CheckoutSessionId = Clip(started.Value!.Id, 200);
        attempt.CheckoutUrl = Clip(started.Value.Url, 2000);
        attempt.ExpiresAtUtc = started.Value.ExpiresAtUtc ?? now.Add(CheckoutLifetime);
        attempt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return started.Value.Url is { } url ? new CheckoutStartedDto(url) : DomainErrors.Payments.ProviderRefused("Stripe did not give a page to pay on.");
    }

    // ── a member's saved card ────────────────────────────────────────────────

    public async Task<Result<MemberPaymentProfileDto>> ProfileAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var member = await MemberAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var config = await _settings.ConfigAsync(organizationId, ct);
        var profile = await _db.ClubPaymentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.Provider == PaymentProviders.Stripe, ct);
        var payments = await _db.ClubOnlinePayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.MemberId == memberId)
            .OrderByDescending(p => p.CreatedAtUtc).Take(20).ToListAsync(ct);
        return ToDto(memberId, config.OnlinePayments, profile, payments);
    }

    public async Task<Result<MemberPaymentProfileDto>> RemoveCardAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default)
    {
        var profile = await _db.ClubPaymentProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.Provider == PaymentProviders.Stripe, ct);
        if (profile?.PaymentMethodId == null) return await ProfileAsync(organizationId, memberId, ct);
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (config.OnlinePayments)
        {
            var detached = await Stripe.DetachPaymentMethodAsync(config.StripeKey!, profile.PaymentMethodId, ct);
            // Already gone at Stripe (removed in its dashboard, say): forgetting it here is all that is left.
            if (detached.Failure is { } failure && failure.Code != "resource_missing")
                return failure.Unreachable ? DomainErrors.Payments.ProviderUnreachable(failure.Message) : DomainErrors.Payments.ProviderRefused(failure.Message);
        }
        ClearCard(profile);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Payments.ProfileChanged;
        }
        return await ProfileAsync(organizationId, memberId, ct);
    }

    public async Task<Result<MemberPaymentProfileDto>> SetAutoPayAsync(Guid organizationId, Guid userId, Guid memberId, AutoPayRequest request, CancellationToken ct = default)
    {
        var profile = await _db.ClubPaymentProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.Provider == PaymentProviders.Stripe, ct);
        if (request.Enabled)
        {
            if (profile?.PaymentMethodId == null) return DomainErrors.Payments.NoSavedCard;
            if (!request.MemberAgreed) return DomainErrors.Payments.AgreementRequired;
        }
        if (profile == null) return await ProfileAsync(organizationId, memberId, ct);
        if (request.Version is { } version && version != profile.Version) return DomainErrors.Payments.ProfileChanged;
        if (profile.AutoPayDues != request.Enabled)
        {
            profile.AutoPayDues = request.Enabled;
            profile.AutoPayAgreedAtUtc = request.Enabled ? DateTime.UtcNow : null;
            profile.AutoPayAgreedBy = request.Enabled ? Clip($"Recorded by {(_user.UserName ?? "staff").Trim()}", 200) : null;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return DomainErrors.Payments.ProfileChanged;
            }
        }
        return await ProfileAsync(organizationId, memberId, ct);
    }

    public async Task<Result<OnlinePaymentDto>> ChargeSavedCardAsync(Guid organizationId, Guid userId, Guid memberId, ChargeSavedCardRequest request, CancellationToken ct = default)
    {
        var member = await MemberAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (!config.OnlinePayments) return config.StripeKeyUnreadable ? DomainErrors.Payments.KeyUnreadable : DomainErrors.Payments.NotSetUp;
        var profile = await _db.ClubPaymentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.Provider == PaymentProviders.Stripe, ct);
        if (profile?.PaymentMethodId == null || profile.CustomerId == null) return DomainErrors.Payments.NoSavedCard;

        if (request.ChargeIds is { Count: > MaxChargesNamed }) return DomainErrors.Payments.TooManyCharges;
        var open = await OpenChargesAsync(organizationId, memberId, ct);
        var named = request.ChargeIds is { Count: > 0 };
        var wanted = named ? open.Where(c => request.ChargeIds!.Contains(c.Id)).ToList() : open;
        // Never more than the account owes: credit the member holds is theirs to pay with first.
        var amount = Math.Min(wanted.Sum(c => c.Amount - c.PaidAmount), await OwedAsync(organizationId, memberId, ct));
        if (wanted.Count == 0 || amount <= 0) return DomainErrors.Payments.NothingToPay;
        var currency = await AccountCurrencyAsync(organizationId, memberId, config, ct);

        // One try at a time for the same charges: a double click is told, not charged twice.
        var chargeText = Join(wanted.Select(c => c.Id));
        var claim = $"card:{Hash(chargeText)}";
        if (await _db.ClubOnlinePayments.AnyAsync(p => p.OrganizationId == organizationId && p.AttemptKey != null && p.AttemptKey.StartsWith(claim)
                && (p.Status == OnlinePaymentStatuses.Open || p.Status == OnlinePaymentStatuses.Processing), ct))
            return DomainErrors.Payments.ChargeInProgress;
        var tries = await _db.ClubOnlinePayments.CountAsync(p => p.OrganizationId == organizationId && p.AttemptKey != null && p.AttemptKey.StartsWith(claim), ct);
        var description = wanted.Count == 1 ? $"{config.ClubName}: {wanted[0].Description}" : $"{config.ClubName}: {wanted.Count} charges";
        // Charges named are paid first; for everything open none are kept, as the ledger pays
        // the open charges oldest first anyway, and a long list of them would not fit.
        var attempt = NewAttempt(organizationId, member, OnlinePaymentPurposes.SavedCard, amount, currency, description, named ? chargeText : null);
        attempt.AttemptKey = $"{claim}:{tries + 1}";
        if (!await TryClaimAsync(attempt, ct)) return DomainErrors.Payments.ChargeInProgress;

        await ChargeAsync(config, profile, attempt, ct);
        return ToDto(attempt);
    }

    public async Task<IReadOnlyList<OnlinePaymentDto>> OnlinePaymentsAsync(Guid organizationId, Guid? memberId, string? status, CancellationToken ct = default)
    {
        var query = _db.ClubOnlinePayments.AsNoTracking().Where(p => p.OrganizationId == organizationId);
        if (memberId is { } id) query = query.Where(p => p.MemberId == id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status.Trim().ToLowerInvariant());
        return (await query.OrderByDescending(p => p.CreatedAtUtc).Take(500).ToListAsync(ct)).Select(ToDto).ToList();
    }

    // ── refunds ──────────────────────────────────────────────────────────────

    public async Task<Result<PaymentDto>> RefundAsync(Guid organizationId, Guid userId, Guid paymentId, RefundPaymentRequest request, CancellationToken ct = default)
    {
        var payment = await _db.ClubPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.OrganizationId == organizationId && p.ReversesPaymentId == null, ct);
        if (payment == null) return DomainErrors.Club.PaymentNotFound;
        if (payment.Provider != PaymentProviders.Stripe || payment.ProviderPaymentId == null) return DomainErrors.Payments.NotAProviderPayment;
        var why = (request.Reason ?? "").Trim();
        if (why.Length == 0) return DomainErrors.Club.ReasonRequiredForRefund;
        if (why.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        if (request.Amount is not { } asked || asked <= 0 || asked >= ClubLedgerService.MaxMoney) return DomainErrors.Club.AmountInvalid;
        var amount = Math.Round(asked, 2, MidpointRounding.AwayFromZero);
        var refunded = -await _db.ClubPayments.Where(p => p.ReversesPaymentId == paymentId).SumAsync(p => p.Amount, ct);
        if (amount > payment.Amount - refunded) return DomainErrors.Club.RefundTooMuch(payment.Amount - refunded, amount);
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (!config.OnlinePayments) return config.StripeKeyUnreadable ? DomainErrors.Payments.KeyUnreadable : DomainErrors.Payments.NotSetUp;

        // The same refund asked twice (a double click) is one refund at Stripe, by this key.
        var refund = await Stripe.RefundAsync(config.StripeKey!, new ProviderRefundRequest(
            payment.ProviderPaymentId, amount, payment.Currency, why,
            new Dictionary<string, string> { ["cadence_org"] = organizationId.ToString(), ["cadence_ledger_payment"] = paymentId.ToString() },
            $"cadence-refund-{paymentId:N}-{refunded.ToString("0.00", CultureInfo.InvariantCulture)}-{amount.ToString("0.00", CultureInfo.InvariantCulture)}"), ct);
        if (refund.Failure is { } failure)
            return failure.Unreachable ? DomainErrors.Payments.ProviderUnreachable(failure.Message) : DomainErrors.Payments.ProviderRefused(failure.Message);
        if (refund.Value!.Status is "failed" or "canceled")
            return DomainErrors.Payments.ProviderRefused($"Stripe could not refund it (the refund {refund.Value.Status}).");
        return await _ledger.RecordProviderRefundAsync(organizationId, paymentId,
            new ProviderRefundEntry(amount, why, PaymentProviders.Stripe, refund.Value.Id, _user.UserName), ct);
    }

    // ── dues charged to saved cards ──────────────────────────────────────────

    public async Task<int> CollectAutoPayAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Across every workspace, as only this job reads: whose members asked for their dues to
        // be charged to a saved card. Each workspace is then worked acting for it alone.
        var organizations = await _db.ClubPaymentProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.AutoPayDues && p.PaymentMethodId != null && p.OrganizationId != null)
            .Select(p => p.OrganizationId!.Value).Distinct().ToListAsync(ct);
        var tried = 0;
        foreach (var organizationId in organizations)
        {
            ct.ThrowIfCancellationRequested();
            if (tried >= MaxAutoPayPerRun) break;
            // A workspace that has turned its club off takes no money on its own: with the club's
            // pages and endpoints gone, nobody could see a payment taken, or follow up the decline
            // they are meant to. Its dues stay open and are charged once the club is on again.
            if (await _db.Organizations.AsNoTracking().AnyAsync(o => o.Id == organizationId && o.ClubModuleOff, ct)) continue;
            using var acting = _db.ActFor(organizationId, null);
            var config = await _settings.ConfigAsync(organizationId, ct);
            if (!config.OnlinePayments) continue;
            var profiles = await _db.ClubPaymentProfiles.AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && p.AutoPayDues && p.PaymentMethodId != null && p.CustomerId != null)
                .ToListAsync(ct);
            foreach (var profile in profiles)
            {
                var member = await MemberAsync(organizationId, profile.MemberId, ct);
                if (member == null) continue;
                // Only dues billed since the member agreed: agreeing today does not charge last year's.
                var agreed = profile.AutoPayAgreedAtUtc ?? profile.SavedAtUtc ?? DateTime.MaxValue;
                var dues = (await OpenChargesAsync(organizationId, profile.MemberId, ct))
                    .Where(c => c.Kind == "Dues" && c.EntryType == ClubEntryTypes.Charge && c.PostedAtUtc >= agreed.AddMinutes(-1))
                    .ToList();
                if (dues.Count == 0) continue;
                // What the account owes, after any credit the member holds. Credit is spent on
                // charges by hand (AppliedCredit), so dues can stand open on an account in credit,
                // and the card is only charged for what that credit does not cover. A charge it
                // covers is not claimed, so a later run charges it if the credit goes elsewhere.
                var owed = await OwedAsync(organizationId, member.Id, ct);
                foreach (var charge in dues)
                {
                    if (tried >= MaxAutoPayPerRun) break;
                    var amount = Math.Min(charge.Amount - charge.PaidAmount, owed);
                    if (amount <= 0) break;
                    var key = $"autopay:{charge.Id}:1";
                    if (await _db.ClubOnlinePayments.AnyAsync(p => p.OrganizationId == organizationId && p.AttemptKey == key, ct)) continue;
                    var currency = await AccountCurrencyAsync(organizationId, member.Id, config, ct);
                    var attempt = NewAttempt(organizationId, member, OnlinePaymentPurposes.AutoPay, amount, currency,
                        $"{config.ClubName}: {charge.Description}", charge.Id.ToString());
                    attempt.AttemptKey = key;
                    // Another server, or a run before this one, took it.
                    if (!await TryClaimAsync(attempt, ct)) continue;
                    tried++;
                    try
                    {
                        await ChargeAsync(config, profile, attempt, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Charging dues {ChargeId} to a saved card failed", charge.Id);
                    }
                    if (attempt.Status != OnlinePaymentStatuses.Failed) owed -= amount;
                }
            }
            _db.ChangeTracker.Clear();
        }
        return tried;
    }

    /// <summary>Charges the saved card for the attempt, already claimed, and settles the attempt by Stripe's answer.</summary>
    private async Task ChargeAsync(PaymentConfig config, ClubPaymentProfile profile, ClubOnlinePayment attempt, CancellationToken ct)
    {
        var answer = await Stripe.ChargeSavedMethodAsync(config.StripeKey!, new ProviderChargeRequest(
            profile.CustomerId!, profile.PaymentMethodId!, profile.PaymentMethodType ?? "card",
            attempt.Amount, attempt.Currency, attempt.Description,
            Metadata(config.OrganizationId, attempt.MemberId, attempt.Id, false),
            $"cadence-charge-{attempt.Id:N}"), ct);
        if (answer.Failure is { } failure)
        {
            attempt.Status = OnlinePaymentStatuses.Failed;
            attempt.FailureCode = Clip(failure.Code, 100);
            // Unanswered is not refused: a request that timed out, or got a 5xx, may have charged
            // the card before Stripe's answer was lost. Stripe's webhook settles the attempt then.
            attempt.FailureMessage = Clip(failure.Unreachable
                ? "Stripe did not answer, so whether the card was charged is not known yet; Stripe's confirmation records it if it was. Check the member's payments before trying again."
                : failure.Message, 500);
            attempt.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAttemptAsync(attempt, ct);
            return;
        }
        await ApplyIntentAsync(config.OrganizationId, attempt, answer.Value!, ct);
    }

    // ── shared ───────────────────────────────────────────────────────────────

    private async Task<Result<(PayLinkDto Link, ClubMember Member, PaymentConfig Config)>> MakeLinkAsync(Guid organizationId, Guid memberId, PayLinkRequest request, CancellationToken ct)
    {
        var purpose = (request.Purpose ?? Account).Trim().ToLowerInvariant();
        if (purpose is not (Account or Charges or Setup)) return DomainErrors.Payments.PurposeInvalid;
        var member = await MemberAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var config = await _settings.ConfigAsync(organizationId, ct);
        if (!config.OnlinePayments) return config.StripeKeyUnreadable ? DomainErrors.Payments.KeyUnreadable : DomainErrors.Payments.NotSetUp;
        var currency = await AccountCurrencyAsync(organizationId, memberId, config, ct);

        var chargeIds = purpose == Charges ? (request.ChargeIds ?? []).Distinct().ToList() : [];
        if (purpose == Charges && chargeIds.Count == 0) return DomainErrors.Payments.PurposeInvalid;
        if (chargeIds.Count > MaxChargesNamed) return DomainErrors.Payments.TooManyCharges;
        var expires = DateTime.UtcNow.Add(purpose == Setup ? SetupLinkLifetime : PayLinkLifetime);
        var claim = new PayLinkClaim(organizationId, memberId, purpose, chargeIds, purpose == Setup && request.AutoPay, expires);
        decimal amount = 0;
        if (purpose != Setup)
        {
            if (purpose == Charges)
            {
                var open = await OpenChargesAsync(organizationId, memberId, ct);
                if (chargeIds.Any(id => open.All(c => c.Id != id))) return DomainErrors.Payments.NothingToPay;
            }
            (amount, _) = await WhatIsOwedAsync(organizationId, memberId, claim, config, ct);
            if (amount <= 0) return DomainErrors.Payments.NothingToPay;
        }
        var url = $"{FrontendBase()}/pay/{Uri.EscapeDataString(_links.Create(claim))}";
        return (new PayLinkDto(url, purpose, amount, currency, expires), member, config);
    }

    /// <summary>What a pay link asks for now: the account's balance owed, or what the named charges still owe.</summary>
    private async Task<(decimal Amount, string Description)> WhatIsOwedAsync(Guid organizationId, Guid memberId, PayLinkClaim claim, PaymentConfig config, CancellationToken ct)
    {
        if (claim.Purpose == Charges)
        {
            var open = (await OpenChargesAsync(organizationId, memberId, ct)).Where(c => claim.ChargeIds.Contains(c.Id)).ToList();
            var owed = open.Sum(c => c.Amount - c.PaidAmount);
            var words = open.Count switch
            {
                0 => "Charges already paid",
                1 => open[0].Description,
                _ => $"{open[0].Description} and {open.Count - 1} more",
            };
            return (owed, $"{config.ClubName}: {words}");
        }
        return (await OwedAsync(organizationId, memberId, ct), $"{config.ClubName}: what the account owes");
    }

    /// <summary>What the member's account owes now, after any credit they hold; nothing when it is in credit.</summary>
    private async Task<decimal> OwedAsync(Guid organizationId, Guid memberId, CancellationToken ct)
    {
        var balance = await _ledger.BalanceAsync(organizationId, memberId, null, ct);
        return balance.IsSuccess ? Math.Max(0, -balance.Value.Balance) : 0;
    }

    private async Task<List<ChargeDto>> OpenChargesAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        (await _ledger.ChargesAsync(organizationId, memberId, ct))
        .Where(c => c.Status is "Pending" or "PartiallyPaid" or "Overdue" && c.Amount - c.PaidAmount > 0)
        .OrderBy(c => c.DueAtUtc ?? c.PostedAtUtc).ToList();

    /// <summary>The member's customer at Stripe, made the first time and kept on their profile.</summary>
    private async Task<Result<string>> CustomerAsync(PaymentConfig config, ClubMember member, CancellationToken ct)
    {
        var profile = await ProfileRowAsync(config.OrganizationId, member.Id, ct);
        if (profile.CustomerId != null) return profile.CustomerId;
        var made = await Stripe.CreateCustomerAsync(config.StripeKey!, new ProviderCustomerRequest(
            NameOf(member), ValidationHelper.IsValidEmail((member.Email ?? "").Trim()) ? (member.Email ?? "").Trim() : null,
            new Dictionary<string, string> { ["cadence_org"] = config.OrganizationId.ToString(), ["cadence_member"] = member.Id.ToString() },
            $"cadence-customer-{config.OrganizationId:N}-{member.Id:N}"), ct);
        if (made.Failure is { } failure)
            return failure.Unreachable ? DomainErrors.Payments.ProviderUnreachable(failure.Message) : DomainErrors.Payments.ProviderRefused(failure.Message);
        profile.CustomerId = Clip(made.Value, 100);
        profile.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Made at the same moment by another request, which kept the same customer (Stripe
            // answered both from the one Idempotency-Key): read what it saved.
            _db.ChangeTracker.Clear();
            var saved = await ProfileRowAsync(config.OrganizationId, member.Id, ct);
            if (saved.CustomerId != null) return saved.CustomerId;
            return DomainErrors.Payments.ProfileChanged;
        }
        return profile.CustomerId!;
    }

    /// <summary>The member's profile row, added (not yet saved) when they have none.</summary>
    private async Task<ClubPaymentProfile> ProfileRowAsync(Guid organizationId, Guid memberId, CancellationToken ct)
    {
        var profile = await _db.ClubPaymentProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.Provider == PaymentProviders.Stripe, ct);
        if (profile != null) return profile;
        profile = new ClubPaymentProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = memberId,
            Provider = PaymentProviders.Stripe,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.ClubPaymentProfiles.Add(profile);
        return profile;
    }

    internal static void ClearCard(ClubPaymentProfile profile)
    {
        profile.PaymentMethodId = null;
        profile.PaymentMethodType = null;
        profile.Brand = null;
        profile.Last4 = null;
        profile.ExpMonth = null;
        profile.ExpYear = null;
        profile.SavedAtUtc = null;
        profile.AutoPayDues = false;
        profile.AutoPayAgreedAtUtc = null;
        profile.AutoPayAgreedBy = null;
        profile.UpdatedAtUtc = DateTime.UtcNow;
    }

    private ClubOnlinePayment NewAttempt(Guid organizationId, ClubMember member, string purpose, decimal amount, string currency, string description, string? chargeIds) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        MemberId = member.Id,
        MemberName = NameOf(member),
        Provider = PaymentProviders.Stripe,
        Purpose = purpose,
        Status = OnlinePaymentStatuses.Open,
        Amount = amount,
        Currency = currency,
        Description = Clip(description, 500) ?? "",
        ChargeIds = string.IsNullOrEmpty(chargeIds) ? null : chargeIds,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedByUserId = _user.UserId,
        CreatedByName = Clip(_user.UserName, 200),
    };

    /// <summary>Saves a new attempt whose claim key the database holds once; false when another took it.</summary>
    private async Task<bool> TryClaimAsync(ClubOnlinePayment attempt, CancellationToken ct)
    {
        _db.ClubOnlinePayments.Add(attempt);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _db.Entry(attempt).State = EntityState.Detached;
            return false;
        }
    }

    private async Task SaveAttemptAsync(ClubOnlinePayment attempt, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Stripe's webhook settled it at the same moment; what it wrote stands, as the
            // payment itself is recorded once either way.
            foreach (var entry in _db.ChangeTracker.Entries<ClubOnlinePayment>().Where(e => e.State == EntityState.Modified).ToList())
                await entry.ReloadAsync(ct);
        }
    }

    private static Dictionary<string, string> Metadata(Guid organizationId, Guid memberId, Guid attemptId, bool autoPay)
    {
        var metadata = new Dictionary<string, string>
        {
            ["cadence_org"] = organizationId.ToString(),
            ["cadence_member"] = memberId.ToString(),
            ["cadence_payment"] = attemptId.ToString(),
        };
        if (autoPay) metadata["cadence_autopay"] = "true";
        return metadata;
    }

    private async Task<ClubMember?> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);

    private async Task<string> AccountCurrencyAsync(Guid organizationId, Guid memberId, PaymentConfig config, CancellationToken ct) =>
        await _db.ClubHouseAccounts.AsNoTracking().Where(a => a.OrganizationId == organizationId && a.MemberId == memberId)
            .Select(a => a.Currency).FirstOrDefaultAsync(ct) ?? config.Currency;

    private string FrontendBase() => (_email.FrontendBaseUrl ?? "").TrimEnd('/');

    private static string Join(IEnumerable<Guid> ids) => string.Join(",", ids.OrderBy(id => id));

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24].ToLowerInvariant();

    private static string NameOf(ClubMember member) => Clip($"{member.FirstName} {member.LastName}".Trim(), 200) ?? "";

    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private static CultureInfo Culture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static OnlinePaymentDto ToDto(ClubOnlinePayment p) => new()
    {
        Id = p.Id,
        MemberId = p.MemberId,
        MemberName = p.MemberName,
        Provider = p.Provider,
        Purpose = p.Purpose,
        Status = p.Status,
        Amount = p.Amount,
        Currency = p.Currency,
        Description = p.Description,
        MethodType = p.MethodType,
        PaymentIntentId = p.PaymentIntentId,
        FailureCode = p.FailureCode,
        FailureMessage = p.FailureMessage,
        AmountReceived = p.AmountReceived,
        LedgerPaymentId = p.LedgerPaymentId,
        CreatedByName = p.CreatedByName,
        CreatedAtUtc = p.CreatedAtUtc,
        CompletedAtUtc = p.CompletedAtUtc,
    };

    private static MemberPaymentProfileDto ToDto(Guid memberId, bool online, ClubPaymentProfile? profile, IReadOnlyList<ClubOnlinePayment> payments) => new()
    {
        MemberId = memberId,
        OnlinePayments = online,
        Card = profile?.PaymentMethodId == null ? null
            : new SavedCardDto(profile.PaymentMethodType ?? "card", profile.Brand, profile.Last4, profile.ExpMonth, profile.ExpYear, profile.SavedAtUtc),
        AutoPayDues = profile?.AutoPayDues ?? false,
        AutoPayAgreedAtUtc = profile?.AutoPayAgreedAtUtc,
        AutoPayAgreedBy = profile?.AutoPayAgreedBy,
        Payments = payments.Select(ToDto).ToList(),
        Version = profile?.Version ?? Guid.Empty,
    };
}
