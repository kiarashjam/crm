using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Payments;

/// <summary>
/// What Stripe tells the club back: a payment that went through, one that failed, a card saved
/// on Stripe's page, a refund made in Stripe's dashboard.
/// </summary>
/// <remarks>
/// <para>
/// A post is trusted only once its signature checks out with the workspace's own signing
/// secret, found by the key in the address it was posted to; its time must be recent, so an
/// old post played again is refused. Each event is kept by its id: Stripe posts an event again
/// until it is answered, and an event already acted on is answered without acting twice.
/// Everything it does is keyed by Stripe's own ids besides (a payment once by its payment
/// intent, a refund once by its refund id), so even an event that failed half-way and comes
/// again does nothing twice.
/// </para>
/// <para>
/// Money is recorded from payment_intent.succeeded, which carries what Stripe received. A
/// Checkout page saving a card (checkout.session.completed in setup mode, or
/// setup_intent.succeeded) saves the card's ids and what Stripe shows of it.
/// </para>
/// </remarks>
public sealed partial class ClubPaymentsService
{
    public async Task<WebhookOutcome> HandleStripeWebhookAsync(Guid endpointKey, byte[] body, string? signatureHeader, CancellationToken ct = default)
    {
        // Across workspaces, by the key in the address: whose post it is. Everything after acts
        // for that workspace alone.
        var organizationId = await _db.ClubPaymentSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.WebhookKey == endpointKey && s.OrganizationId != null)
            .Select(s => s.OrganizationId).FirstOrDefaultAsync(ct);
        if (organizationId is not { } org) return new WebhookOutcome(404, "unknown endpoint");
        using var acting = _db.ActFor(org, null);

        var config = await _settings.ConfigAsync(org, ct);
        if (config.WebhookSecret == null) return new WebhookOutcome(400, "no signing secret is set for this workspace");
        var stripeEvent = Stripe.ReadEvent(config.WebhookSecret, body, signatureHeader, DateTime.UtcNow, out var problem);
        if (stripeEvent == null)
        {
            _logger.LogWarning("A Stripe webhook post for workspace {OrganizationId} was refused: {Problem}", org, problem);
            return new WebhookOutcome(400, problem ?? "not a Stripe event");
        }

        var record = await ClaimEventAsync(org, stripeEvent, ct);
        if (record == null) return new WebhookOutcome(200, "already processed");
        try
        {
            var note = await DispatchAsync(org, config, stripeEvent, ct);
            record.Status = note.Ignored ? "ignored" : "processed";
            record.Note = Clip(note.Note, 500);
            record.ProcessedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new WebhookOutcome(200, record.Status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stripe event {EventId} ({Type}) could not be processed", stripeEvent.Id, stripeEvent.Type);
            _db.ChangeTracker.Clear();
            var failed = await _db.ClubProviderEvents.FirstOrDefaultAsync(e => e.Id == record.Id, ct);
            if (failed != null)
            {
                failed.Status = "failed";
                failed.Note = Clip(ex.Message, 500);
                await _db.SaveChangesAsync(ct);
            }
            // Stripe posts it again later; everything it does is keyed, so nothing happens twice.
            return new WebhookOutcome(500, "failed");
        }
    }

    /// <summary>The event's row, claimed for this post; null when it was already acted on.</summary>
    private async Task<ClubProviderEvent?> ClaimEventAsync(Guid organizationId, ProviderEvent stripeEvent, CancellationToken ct)
    {
        var existing = await _db.ClubProviderEvents.FirstOrDefaultAsync(e => e.OrganizationId == organizationId
            && e.Provider == PaymentProviders.Stripe && e.EventId == stripeEvent.Id, ct);
        if (existing != null)
        {
            if (existing.Status is "processed" or "ignored") return null;
            existing.Attempts++;
            await _db.SaveChangesAsync(ct);
            return existing;
        }
        var row = new ClubProviderEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Provider = PaymentProviders.Stripe,
            EventId = Clip(stripeEvent.Id, 100)!,
            Type = Clip(stripeEvent.Type, 100)!,
            ObjectId = Clip(stripeEvent.ObjectId, 100),
            Status = "received",
            Attempts = 1,
            ReceivedAtUtc = DateTime.UtcNow,
        };
        _db.ClubProviderEvents.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The same event posted twice at once: the other post acts on it.
            _db.Entry(row).State = EntityState.Detached;
            return null;
        }
    }

    private sealed record Outcome(string Note, bool Ignored = false);

    private async Task<Outcome> DispatchAsync(Guid organizationId, PaymentConfig config, ProviderEvent stripeEvent, CancellationToken ct)
    {
        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
            case "payment_intent.processing":
            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            {
                var intent = stripeEvent.PaymentIntent;
                if (intent == null) return new Outcome("no payment intent in the event", true);
                var attempt = await AttemptForAsync(organizationId, intent.Metadata, intent.Id, ct);
                if (attempt == null) return new Outcome("not a payment Cadence started", true);
                await ApplyIntentAsync(organizationId, attempt, intent, ct);
                return new Outcome($"{attempt.Purpose} {attempt.Status}");
            }
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
            {
                var session = stripeEvent.Checkout;
                if (session == null) return new Outcome("no Checkout session in the event", true);
                var attempt = await AttemptForAsync(organizationId, session.Metadata, null, ct)
                              ?? await _db.ClubOnlinePayments.FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.CheckoutSessionId == session.Id, ct);
                if (attempt == null) return new Outcome("not a page Cadence started", true);
                if (session.Mode == "setup")
                {
                    if (stripeEvent.Type != "checkout.session.completed" || session.SetupId == null) return new Outcome("setup page not finished", true);
                    var setup = await Stripe.GetSetupAsync(config.StripeKey!, session.SetupId, ct);
                    if (setup.Failure is { } failure) throw new InvalidOperationException($"Stripe did not give the saved card: {failure.Message}");
                    await SaveCardAsync(organizationId, attempt, setup.Value!, session.Metadata, ct);
                    return new Outcome("card saved");
                }
                if (session.PaymentIntentId != null && attempt.PaymentIntentId == null) attempt.PaymentIntentId = Clip(session.PaymentIntentId, 100);
                if (stripeEvent.Type == "checkout.session.expired" && attempt.Status == OnlinePaymentStatuses.Open)
                    attempt.Status = OnlinePaymentStatuses.Expired;
                else if (stripeEvent.Type == "checkout.session.async_payment_failed" && attempt.Status != OnlinePaymentStatuses.Succeeded)
                {
                    attempt.Status = OnlinePaymentStatuses.Failed;
                    attempt.FailureMessage = "The direct debit did not go through.";
                }
                else if (session.PaymentStatus == "unpaid" && attempt.Status == OnlinePaymentStatuses.Open)
                    attempt.Status = OnlinePaymentStatuses.Processing;
                attempt.UpdatedAtUtc = DateTime.UtcNow;
                await SaveAttemptAsync(attempt, ct);
                // The money itself is recorded from payment_intent.succeeded, which says what Stripe received.
                return new Outcome($"checkout {attempt.Status}");
            }
            case "setup_intent.succeeded":
            {
                var setup = stripeEvent.Setup;
                if (setup == null) return new Outcome("no setup intent in the event", true);
                var attempt = await AttemptForAsync(organizationId, setup.Metadata, null, ct);
                if (attempt == null) return new Outcome("not a card Cadence asked for", true);
                if (setup.PaymentMethod?.Last4 == null)
                {
                    var read = await Stripe.GetSetupAsync(config.StripeKey!, setup.Id, ct);
                    if (read.Failure is { } failure) throw new InvalidOperationException($"Stripe did not give the saved card: {failure.Message}");
                    setup = read.Value!;
                }
                await SaveCardAsync(organizationId, attempt, setup, setup.Metadata, ct);
                return new Outcome("card saved");
            }
            case "charge.refunded":
            {
                var charge = stripeEvent.Charge;
                if (charge?.PaymentIntentId == null) return new Outcome("no payment on the charge", true);
                var payment = await _db.ClubPayments.AsNoTracking().FirstOrDefaultAsync(p => p.OrganizationId == organizationId
                    && p.Provider == PaymentProviders.Stripe && p.ProviderPaymentId == charge.PaymentIntentId && p.ReversesPaymentId == null, ct);
                if (payment == null) return new Outcome("the refunded payment is not on the ledger", true);
                var recorded = 0;
                foreach (var refund in charge.Refunds.Where(r => r.Status is "succeeded" or "pending"))
                {
                    var result = await _ledger.RecordProviderRefundAsync(organizationId, payment.Id,
                        new ProviderRefundEntry(refund.Amount, "Refunded in Stripe", PaymentProviders.Stripe, refund.Id, "Stripe"), ct);
                    if (!result.IsSuccess) throw new InvalidOperationException($"The refund {refund.Id} could not be recorded: {result.Error.Description}");
                    recorded++;
                }
                return new Outcome($"{recorded} refund(s) on the ledger");
            }
            default:
                return new Outcome("not an event Cadence acts on", true);
        }
    }

    /// <summary>The attempt Cadence made, named in the metadata it gave Stripe, or found by the payment's id.</summary>
    private async Task<ClubOnlinePayment?> AttemptForAsync(Guid organizationId, IReadOnlyDictionary<string, string> metadata, string? paymentIntentId, CancellationToken ct)
    {
        if (metadata.TryGetValue("cadence_payment", out var text) && Guid.TryParse(text, out var id)
            && (!metadata.TryGetValue("cadence_org", out var org) || org == organizationId.ToString()))
        {
            var byId = await _db.ClubOnlinePayments.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);
            if (byId != null) return byId;
        }
        return paymentIntentId == null ? null
            : await _db.ClubOnlinePayments.FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PaymentIntentId == paymentIntentId, ct);
    }

    /// <summary>
    /// Settles an attempt by what Stripe says of its payment: recorded on the ledger once it has
    /// succeeded (once, by Stripe's id), still clearing (SEPA), or failed with Stripe's reason.
    /// </summary>
    private async Task ApplyIntentAsync(Guid organizationId, ClubOnlinePayment attempt, ProviderPaymentIntent intent, CancellationToken ct)
    {
        attempt.PaymentIntentId ??= Clip(intent.Id, 100);
        attempt.ProviderChargeId = Clip(intent.LatestChargeId, 100) ?? attempt.ProviderChargeId;
        attempt.MethodType = Clip(intent.MethodType, 30) ?? attempt.MethodType;
        attempt.UpdatedAtUtc = DateTime.UtcNow;
        switch (intent.Status)
        {
            case "succeeded":
            {
                var received = intent.AmountReceived > 0 ? intent.AmountReceived : intent.Amount;
                attempt.AmountReceived = received;
                // Kept before the ledger saves: a ledger write that is refused lets go of whatever
                // else was waiting to be saved, and what Stripe said must not go with it.
                await SaveAttemptAsync(attempt, ct);
                var paid = await _ledger.RecordReceivedPaymentAsync(organizationId, new ReceivedPaymentRequest
                {
                    MemberId = attempt.MemberId,
                    Amount = received,
                    Currency = intent.Currency,
                    Method = MethodFor(attempt.MethodType, attempt.Purpose),
                    ReceivedAtUtc = DateTime.UtcNow,
                    Reference = intent.Id,
                    Notes = attempt.Description,
                    Provider = PaymentProviders.Stripe,
                    ProviderPaymentId = intent.Id,
                    PayFirst = (attempt.ChargeIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
                    IdempotencyKey = $"stripe:{intent.Id}",
                    RecordedBy = "Stripe",
                }, ct);
                attempt.Status = OnlinePaymentStatuses.Succeeded;
                attempt.CompletedAtUtc ??= DateTime.UtcNow;
                if (paid.IsSuccess)
                {
                    attempt.LedgerPaymentId = paid.Value.Id;
                    attempt.FailureCode = null;
                    attempt.FailureMessage = null;
                    await SaveAttemptAsync(attempt, ct);
                    await ReinstateAsync(organizationId, attempt.MemberId, ct);
                }
                else
                {
                    // Stripe has the money and the ledger would not take it (the member was
                    // deleted, the currency differs): staff see why, and record or refund it.
                    attempt.FailureCode = "not_recorded";
                    attempt.FailureMessage = Clip($"Stripe took the payment, but it could not be recorded: {paid.Error.Description} Record it by hand, or refund it in Stripe.", 500);
                    await SaveAttemptAsync(attempt, ct);
                }
                return;
            }
            case "processing":
                if (attempt.Status != OnlinePaymentStatuses.Succeeded) attempt.Status = OnlinePaymentStatuses.Processing;
                break;
            case "canceled":
                if (attempt.Status != OnlinePaymentStatuses.Succeeded) attempt.Status = OnlinePaymentStatuses.Canceled;
                break;
            default:
                // requires_payment_method (declined), requires_action (the bank wants the member to
                // confirm, which a charge without them present cannot do), and the like: it failed.
                if (attempt.Status != OnlinePaymentStatuses.Succeeded)
                {
                    attempt.Status = OnlinePaymentStatuses.Failed;
                    attempt.FailureCode = Clip(intent.FailureCode ?? intent.Status, 100);
                    attempt.FailureMessage = Clip(intent.Status == "requires_action"
                        ? "The card's bank wants the member to confirm this payment. Send them a pay link instead."
                        : intent.FailureMessage ?? "The payment did not go through.", 500);
                    attempt.CompletedAtUtc ??= DateTime.UtcNow;
                }
                break;
        }
        await SaveAttemptAsync(attempt, ct);
    }

    /// <summary>Keeps the card the member saved on Stripe's page: its ids and what Stripe shows of it, never its number.</summary>
    private async Task SaveCardAsync(Guid organizationId, ClubOnlinePayment attempt, ProviderSetup setup, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        if (setup.Status != "succeeded" || setup.PaymentMethod == null) return;
        var profile = await ProfileRowAsync(organizationId, attempt.MemberId, ct);
        var method = setup.PaymentMethod;
        var autoPay = metadata.TryGetValue("cadence_autopay", out var flag) && flag == "true";
        if (profile.PaymentMethodId != method.Id)
        {
            profile.CustomerId = Clip(setup.CustomerId, 100) ?? profile.CustomerId;
            profile.PaymentMethodId = Clip(method.Id, 100);
            profile.PaymentMethodType = Clip(method.Type, 20);
            profile.Brand = Clip(method.Brand, 20);
            profile.Last4 = Clip(method.Last4, 4);
            profile.ExpMonth = method.ExpMonth;
            profile.ExpYear = method.ExpYear;
            profile.SavedAtUtc = DateTime.UtcNow;
            // The member agreed on Stripe's page itself, under the words it showed them.
            profile.AutoPayDues = autoPay;
            profile.AutoPayAgreedAtUtc = autoPay ? DateTime.UtcNow : null;
            profile.AutoPayAgreedBy = autoPay ? "The member, on Stripe's card page" : null;
            profile.UpdatedAtUtc = DateTime.UtcNow;
        }
        attempt.SetupIntentId ??= Clip(setup.Id, 100);
        attempt.Status = OnlinePaymentStatuses.Succeeded;
        attempt.MethodType = Clip(method.Type, 30);
        attempt.CompletedAtUtc ??= DateTime.UtcNow;
        attempt.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>The ledger's word for how the money came: a saved card, a card, TWINT or a SEPA direct debit.</summary>
    private static string MethodFor(string? methodType, string purpose) => methodType switch
    {
        "twint" => "Twint",
        "sepa_debit" => "DirectDebit",
        _ => purpose is OnlinePaymentPurposes.AutoPay or OnlinePaymentPurposes.SavedCard ? "CardOnFile" : "Card",
    };

    private async Task ReinstateAsync(Guid organizationId, Guid memberId, CancellationToken ct)
    {
        try
        {
            await _billing.ReinstateIfSettledAsync(organizationId, memberId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Member {MemberId} could not be put back after an online payment; the next billing run will", memberId);
        }
    }
}
