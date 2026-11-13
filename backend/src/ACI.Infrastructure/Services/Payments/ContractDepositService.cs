using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Common.Payments;
using ACI.Application.Common.Pdf;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IContractDeposits"/>
/// <remarks>
/// <para>
/// Every write to a deposit's row is saved against its version, so Stripe's webhook, a bank
/// file and a countersignature arriving at the same moment never write over each other: the
/// one that loses reads the row again and decides once more. The money itself is recorded on
/// the ledger under the provider's id, which the database holds once per workspace, so even a
/// race that got past the row would record the deposit once.
/// </para>
/// <para>
/// Nothing here admits anyone. The member is made by the club's approval (a countersignature
/// or a reviewer's approve); the deposit waits, paid, until there is one, and is put on their
/// account then. A deposit paid after the member was admitted goes on their account as soon as
/// Stripe or the bank file says so.
/// </para>
/// </remarks>
public sealed class ContractDepositService : IContractDeposits
{
    /// <summary>Stripe's page lasts at most a day; a signer who comes back later gets a new one.</summary>
    private static readonly TimeSpan CheckoutLifetime = TimeSpan.FromHours(23);

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubCatalog _catalog;
    private readonly IClubRules _rules;
    private readonly IClubApplications _admissions;
    private readonly ClubPaymentSettingsService _settings;
    private readonly ClubQrBillService _qrBills;
    private readonly IEnumerable<IPaymentProvider> _providers;
    private readonly IPermissionService _permissions;
    private readonly IWorkspaceModules _modules;
    private readonly ILogger<ContractDepositService> _logger;

    public ContractDepositService(AppDbContext db, IClubLedger ledger, IClubCatalog catalog, IClubRules rules, IClubApplications admissions,
        ClubPaymentSettingsService settings, ClubQrBillService qrBills, IEnumerable<IPaymentProvider> providers, IPermissionService permissions,
        IWorkspaceModules modules, ILogger<ContractDepositService> logger)
    {
        _db = db;
        _ledger = ledger;
        _catalog = catalog;
        _rules = rules;
        _admissions = admissions;
        _settings = settings;
        _qrBills = qrBills;
        _providers = providers;
        _permissions = permissions;
        _modules = modules;
        _logger = logger;
    }

    private IPaymentProvider Stripe => _providers.First(p => p.Name == PaymentProviders.Stripe);

    // ── asking ───────────────────────────────────────────────────────────────

    public async Task<Result<DepositAsk>> CheckAskAsync(Guid organizationId, Guid userId, decimal amount, CancellationToken ct = default)
    {
        if (!await _modules.IsOnAsync(organizationId, WorkspaceModules.Club, ct)) return DomainErrors.Contract.ClubOff;
        // Asking someone for money is taking a payment: the same right a pay link needs.
        var held = await _permissions.PermissionsAsync(userId, organizationId, ct);
        if (!held.Contains(MemberPermissions.ManageMoney))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageMoney, "ask for a deposit");
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0 || rounded >= ClubLedgerService.MaxMoney) return DomainErrors.ContractDeposit.AmountInvalid;
        var config = await _settings.ConfigAsync(organizationId, ct);
        var currency = await _catalog.CurrencyAsync(organizationId, ct);
        // No way to pay is no deposit: the signer would be asked for money with nowhere to send it.
        if (!config.OnlinePayments && !QrBillsFor(config, currency)) return DomainErrors.ContractDeposit.NoWayToPay;
        return new DepositAsk(rounded, currency);
    }

    public async Task OpenAsync(Contract contract, CancellationToken ct = default)
    {
        if (contract.DepositAmount is not > 0) return;
        if (await _db.ContractDeposits.AnyAsync(d => d.OrganizationId == contract.OrganizationId && d.ContractId == contract.Id, ct)) return;
        var row = new ContractDeposit
        {
            Id = Guid.NewGuid(),
            OrganizationId = contract.OrganizationId,
            ContractId = contract.Id,
            Amount = contract.DepositAmount.Value,
            Currency = contract.DepositCurrency ?? await _catalog.CurrencyAsync(contract.OrganizationId, ct),
            Status = ContractDepositStatuses.Awaiting,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.ContractDeposits.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Sent twice at once: the other send made it.
            _db.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task CloseAsync(Contract contract, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var deposit = await TrackedAsync(contract.OrganizationId, contract.Id, ct);
            if (deposit is { Status: ContractDepositStatuses.Paid, LedgerPaymentId: null })
            {
                // Paid already, and no agreement stands behind it now: it is never put on an
                // account by itself (SettleAsync), so whoever looks at the contract is told.
                await LogEventAsync(contract.Id, "deposit_to_refund",
                    $"The deposit of {Money(deposit.AmountPaid ?? deposit.Amount, deposit.Currency)} was paid ({deposit.ProviderPaymentId}) before the contract was withdrawn. Pay it back{RefundWhere(deposit)}.", ct);
                return;
            }
            if (deposit is not { Status: ContractDepositStatuses.Awaiting }) return;
            deposit.Status = ContractDepositStatuses.Cancelled;
            deposit.CheckoutUrl = null;
            deposit.CheckoutExpiresAtUtc = null;
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            if (await TrySaveAsync(deposit, ct)) return;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, ContractDepositDto>> ForContractsAsync(Guid organizationId, IReadOnlyCollection<Guid> contractIds, CancellationToken ct = default)
    {
        if (contractIds.Count == 0) return new Dictionary<Guid, ContractDepositDto>();
        var rows = await _db.ContractDeposits.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && contractIds.Contains(d.ContractId)).ToListAsync(ct);
        var memberIds = rows.Where(r => r.MemberId != null).Select(r => r.MemberId!.Value).Distinct().ToList();
        var names = await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && memberIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => (m.FirstName + " " + m.LastName).Trim(), ct);
        var referenceIds = rows.Where(r => r.PaymentReferenceId != null).Select(r => r.PaymentReferenceId!.Value).ToList();
        var references = await _db.ClubPaymentReferences.AsNoTracking().Where(r => referenceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => PaymentReferences.Format(r.QrReference ?? r.ScorReference), ct);
        return rows.ToDictionary(r => r.ContractId, r => new ContractDepositDto
        {
            Amount = r.Amount,
            Currency = r.Currency,
            Status = r.Status,
            AmountPaid = r.AmountPaid,
            AmountRefunded = r.AmountRefunded,
            PaidAtUtc = Utc(r.PaidAtUtc),
            Provider = r.Provider,
            MethodType = r.MethodType,
            FailureMessage = r.FailureMessage,
            MemberId = r.MemberId,
            MemberName = r.MemberId is { } m ? names.GetValueOrDefault(m) : null,
            LedgerPaymentId = r.LedgerPaymentId,
            RecordedAtUtc = Utc(r.RecordedAtUtc),
            Reference = r.PaymentReferenceId is { } p ? references.GetValueOrDefault(p) : null,
        });
    }

    // ── the signing page ─────────────────────────────────────────────────────

    public async Task<PublicDepositDto?> PublicAsync(Contract contract, CancellationToken ct = default)
    {
        var deposit = await _db.ContractDeposits.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrganizationId == contract.OrganizationId && d.ContractId == contract.Id, ct);
        if (deposit == null) return null;
        var config = await _settings.ConfigAsync(contract.OrganizationId, ct);
        var payable = Payable(contract, deposit);
        var qr = QrBillsFor(config, deposit.Currency);
        string? reference = null;
        if (qr && payable && deposit.PaymentReferenceId is { } referenceId)
        {
            var row = await _db.ClubPaymentReferences.AsNoTracking().FirstOrDefaultAsync(r => r.Id == referenceId, ct);
            if (row != null) reference = PaymentReferences.Format(config.QrIban != null ? row.QrReference ?? row.ScorReference : row.ScorReference);
        }
        return new PublicDepositDto(
            deposit.Amount, deposit.Currency, deposit.Status, Utc(deposit.PaidAtUtc), payable,
            config.OnlinePayments ? config.MethodsFor(deposit.Currency) : [],
            qr,
            qr && payable ? Iban(config) : null,
            reference,
            qr && payable ? config.Creditor?.Name : null,
            payable ? deposit.FailureMessage : null,
            config.Locale);
    }

    public async Task<Result<CheckoutStartedDto>> StartCheckoutAsync(Contract contract, string signingPageUrl, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var deposit = await TrackedAsync(contract.OrganizationId, contract.Id, ct);
            if (deposit == null) return DomainErrors.ContractDeposit.NotAsked;
            if (Refusal(contract, deposit) is { } refused) return refused;
            var config = await _settings.ConfigAsync(contract.OrganizationId, ct);
            if (!config.OnlinePayments) return config.StripeKeyUnreadable ? DomainErrors.Payments.KeyUnreadable : DomainErrors.Payments.NotSetUp;
            var now = DateTime.UtcNow;
            // Pressing Pay twice, or coming back within the page's life, finds the page already made.
            if (deposit.CheckoutUrl != null && deposit.CheckoutExpiresAtUtc > now.AddMinutes(10)) return new CheckoutStartedDto(deposit.CheckoutUrl);

            // The attempt's number is claimed before Stripe is asked, and keys the request there:
            // two presses at once make one page, and a retried request gets the same page back.
            deposit.CheckoutAttempts++;
            deposit.UpdatedAtUtc = now;
            if (!await TrySaveAsync(deposit, ct)) continue;

            var club = config.ClubName;
            var started = await Stripe.CreateCheckoutAsync(config.StripeKey!, new ProviderCheckoutRequest(
                "payment",
                deposit.Amount,
                deposit.Currency,
                Clip($"{club}: deposit, {contract.Title}", 250)!,
                null,
                contract.CounterpartyEmail,
                config.MethodsFor(deposit.Currency),
                $"{signingPageUrl}?deposit=done",
                $"{signingPageUrl}?deposit=cancelled",
                now.Add(CheckoutLifetime),
                Metadata(contract, deposit),
                deposit.Id.ToString(),
                $"cadence-deposit-{deposit.Id:N}-{deposit.CheckoutAttempts}"), ct);
            if (started.Failure is { } failure)
            {
                deposit.FailureMessage = Clip(failure.Message, 500);
                deposit.UpdatedAtUtc = DateTime.UtcNow;
                await TrySaveAsync(deposit, ct);
                return failure.Unreachable ? DomainErrors.Payments.ProviderUnreachable(failure.Message) : DomainErrors.Payments.ProviderRefused(failure.Message);
            }
            deposit.CheckoutSessionId = Clip(started.Value!.Id, 200);
            deposit.CheckoutUrl = Clip(started.Value.Url, 2000);
            deposit.CheckoutExpiresAtUtc = started.Value.ExpiresAtUtc ?? now.Add(CheckoutLifetime);
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            await TrySaveAsync(deposit, ct);
            return started.Value.Url is { } url ? new CheckoutStartedDto(url) : DomainErrors.Payments.ProviderRefused("Stripe did not give a page to pay on.");
        }
        return DomainErrors.Payments.ProviderRefused("The deposit was changed at the same moment. Try again.");
    }

    public async Task<Result<ContractDocument>> QrBillAsync(Contract contract, string? language, CancellationToken ct = default)
    {
        var deposit = await TrackedAsync(contract.OrganizationId, contract.Id, ct);
        if (deposit == null) return DomainErrors.ContractDeposit.NotAsked;
        if (Refusal(contract, deposit) is { } refused) return refused;
        var config = await _settings.ConfigAsync(contract.OrganizationId, ct);
        if (!QrBillsFor(config, deposit.Currency)) return DomainErrors.ContractDeposit.NoQrBill;
        var words = AppLanguages.Normalise(language) ?? AppLanguages.English;
        var number = "D-" + ContractService.Reference(contract.Id);
        var (slip, reference) = await _qrBills.ForDepositAsync(contract.OrganizationId, contract.Id, deposit.Amount, deposit.Currency,
            MemberTexts.T(words, "deposit.qrMessage", ("number", number)), ct);
        if (slip == null) return DomainErrors.ContractDeposit.NoQrBill;
        if (reference != null && deposit.PaymentReferenceId != reference.Id)
        {
            deposit.PaymentReferenceId = reference.Id;
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            await TrySaveAsync(deposit, ct);
        }
        var bytes = ChargeInvoicePdf.Render(new ChargeInvoicePdfRequest(
            config.ClubName,
            contract.CounterpartyName,
            contract.CounterpartyEmail,
            contract.MembershipTier,
            number,
            MemberTexts.T(words, "deposit.description", ("title", contract.Title)),
            "Deposit",
            contract.ClientSignedAtUtc ?? DateTime.UtcNow,
            null,
            deposit.Amount,
            0,
            false,
            null,
            deposit.Currency,
            config.Locale,
            config.TimeZone,
            // The deposit's own time rather than the clock, so the same slip gives the same file.
            deposit.CreatedAtUtc,
            slip,
            null,
            words));
        return new ContractDocument($"deposit-{number}.pdf", "application/pdf", bytes);
    }

    // ── money coming in ──────────────────────────────────────────────────────

    public async Task<string?> HandleStripeEventAsync(Guid organizationId, ProviderEvent stripeEvent, CancellationToken ct = default)
    {
        var metadata = stripeEvent.PaymentIntent?.Metadata ?? stripeEvent.Checkout?.Metadata;
        if (metadata == null || !metadata.TryGetValue("cadence_deposit", out var text) || !Guid.TryParse(text, out var depositId)) return null;
        if (metadata.TryGetValue("cadence_org", out var org) && org != organizationId.ToString()) return "a deposit of another workspace";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var deposit = await _db.ContractDeposits.FirstOrDefaultAsync(d => d.Id == depositId && d.OrganizationId == organizationId, ct);
            if (deposit == null) return "the deposit is no longer here";
            var now = DateTime.UtcNow;
            string note;
            var settle = false;
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                {
                    var intent = stripeEvent.PaymentIntent!;
                    var received = intent.AmountReceived > 0 ? intent.AmountReceived : intent.Amount;
                    if (deposit.Status == ContractDepositStatuses.Paid)
                    {
                        if (deposit.ProviderPaymentId == intent.Id)
                        {
                            // Stripe's word again (posted anew after a failure, or another event
                            // about the payment): the next time an earlier try never came to. A
                            // deposit not on an account yet is put there now if the signer has been
                            // admitted; the ledger's key keeps it from being recorded twice.
                            if (deposit.LedgerPaymentId == null) await SettleAsync(organizationId, deposit.ContractId, null, ct);
                            return "deposit already paid";
                        }
                        // Paid twice (two of Stripe's pages, both paid): real money, said where staff look.
                        await LogEventAsync(deposit.ContractId, "deposit_paid_twice",
                            $"A second payment of {Money(received, deposit.Currency)} came in for the deposit ({intent.Id}). Refund it in Stripe, or record it on the member's account by hand.", ct);
                        return "a second payment for a deposit paid already";
                    }
                    if (!string.Equals(intent.Currency, deposit.Currency, StringComparison.OrdinalIgnoreCase))
                    {
                        deposit.FailureMessage = Clip($"Stripe took {received} {intent.Currency.ToUpperInvariant()}, not {deposit.Currency}: record or refund it by hand.", 500);
                        deposit.UpdatedAtUtc = now;
                        if (!await TrySaveAsync(deposit, ct)) continue;
                        return "deposit paid in another currency";
                    }
                    MarkPaid(deposit, PaymentProviders.Stripe, intent.Id, received, intent.MethodType ?? deposit.MethodType ?? "card", now);
                    note = "deposit paid";
                    settle = true;
                    break;
                }
                case "payment_intent.processing":
                    if (deposit.Status != ContractDepositStatuses.Awaiting) return $"deposit {deposit.Status}";
                    deposit.Status = ContractDepositStatuses.Processing;
                    deposit.ProviderPaymentId ??= Clip(stripeEvent.PaymentIntent?.Id, 200);
                    note = "deposit processing";
                    break;
                case "payment_intent.payment_failed":
                case "payment_intent.canceled":
                case "checkout.session.async_payment_failed":
                    // A word that comes after the money is no reason to doubt the money.
                    if (deposit.Status is not (ContractDepositStatuses.Awaiting or ContractDepositStatuses.Processing)) return $"deposit {deposit.Status}";
                    deposit.Status = ContractDepositStatuses.Awaiting;
                    deposit.FailureMessage = Clip(stripeEvent.PaymentIntent?.FailureMessage ?? "The payment did not go through.", 500);
                    note = "deposit payment failed";
                    break;
                case "checkout.session.completed":
                case "checkout.session.async_payment_succeeded":
                    // The money itself is recorded from payment_intent.succeeded, which says what Stripe received.
                    if (stripeEvent.Checkout?.PaymentStatus == "unpaid" && deposit.Status == ContractDepositStatuses.Awaiting)
                        deposit.Status = ContractDepositStatuses.Processing;
                    note = $"deposit checkout {deposit.Status}";
                    break;
                case "checkout.session.expired":
                    if (deposit.CheckoutSessionId == stripeEvent.Checkout?.Id)
                    {
                        deposit.CheckoutUrl = null;
                        deposit.CheckoutExpiresAtUtc = null;
                    }
                    note = "deposit checkout expired";
                    break;
                default:
                    return "not an event Cadence acts on for a deposit";
            }
            deposit.UpdatedAtUtc = now;
            if (!await TrySaveAsync(deposit, ct)) continue;
            if (settle)
            {
                await LogEventAsync(deposit.ContractId, "deposit_paid", $"Deposit of {Money(deposit.AmountPaid ?? deposit.Amount, deposit.Currency)} paid through Stripe", ct);
                // Paid on Stripe's page after the contract was withdrawn (the page lives on at
                // Stripe): the money is real, so it is kept as paid, and staff are told to pay it
                // back rather than it going on anyone's account.
                if (await WithdrawnAsync(organizationId, deposit.ContractId, ct))
                {
                    await LogEventAsync(deposit.ContractId, "deposit_to_refund",
                        $"A payment of {Money(deposit.AmountPaid ?? deposit.Amount, deposit.Currency)} came in for the deposit ({deposit.ProviderPaymentId}) after the contract was withdrawn. Pay it back in Stripe.", ct);
                    return "deposit paid on a withdrawn contract";
                }
                await SettleAsync(organizationId, deposit.ContractId, null, ct);
            }
            return note;
        }
        // Stripe posts it again; everything is keyed, so nothing happens twice.
        throw new InvalidOperationException("The deposit kept changing while Stripe's word was being saved.");
    }

    public async Task<string?> RecordProviderRefundAsync(Guid organizationId, string paymentIntentId, IReadOnlyList<ProviderRefund> refunds, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var deposit = await _db.ContractDeposits.FirstOrDefaultAsync(d => d.OrganizationId == organizationId
                && d.Provider == PaymentProviders.Stripe && d.ProviderPaymentId == paymentIntentId, ct);
            if (deposit == null) return null;
            // On an account already: the ledger's own refund path has it.
            if (deposit.LedgerPaymentId != null) return null;
            var refunded = refunds.Where(r => r.Status is "succeeded" or "pending").Sum(r => r.Amount);
            if (refunded == deposit.AmountRefunded) return "deposit refund already known";
            deposit.AmountRefunded = refunded;
            if (refunded >= (deposit.AmountPaid ?? deposit.Amount)) deposit.Status = ContractDepositStatuses.Refunded;
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            if (!await TrySaveAsync(deposit, ct)) continue;
            await LogEventAsync(deposit.ContractId, "deposit_refunded", $"{Money(refunded, deposit.Currency)} of the deposit was paid back in Stripe", ct);
            return "deposit refunded";
        }
        throw new InvalidOperationException("The deposit kept changing while Stripe's refund was being saved.");
    }

    public async Task<DepositTransferOutcome> RecordBankTransferAsync(Guid organizationId, Guid contractId, DepositTransfer transfer, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var deposit = await TrackedAsync(organizationId, contractId, ct);
            if (deposit == null) return new DepositTransferOutcome(false, null, null, null, "The reference is a contract's deposit that is no longer asked for: say whose this transfer is.");
            if (deposit.Status == ContractDepositStatuses.Paid)
            {
                if (deposit.ProviderPaymentId == transfer.BankReference)
                    return new DepositTransferOutcome(true, deposit.MemberId, null, deposit.LedgerPaymentId, deposit.LedgerPaymentId == null ? KeptNote : null);
                return new DepositTransferOutcome(false, null, null, null,
                    "The reference is a contract's deposit that was paid already: say whose this second transfer is, or pay it back.");
            }
            // A deposit that could still be paid when its contract was withdrawn (a payment was on
            // its way) is no longer one either: the transfer waits for staff, as one quoting no
            // reference would.
            if (deposit.Status is ContractDepositStatuses.Cancelled or ContractDepositStatuses.Refunded
                || await WithdrawnAsync(organizationId, contractId, ct))
                return new DepositTransferOutcome(false, null, null, null,
                    "The reference is the deposit of a contract that was declined or withdrawn: say whose this transfer is, or pay it back.");
            if (!string.Equals(transfer.Currency, deposit.Currency, StringComparison.OrdinalIgnoreCase))
                return new DepositTransferOutcome(false, null, null, null, $"The transfer is in {transfer.Currency}; the deposit is in {deposit.Currency}.");
            MarkPaid(deposit, PaymentProviders.Bank, transfer.BankReference, transfer.Amount, "bank", transfer.ReceivedAtUtc);
            deposit.UpdatedAtUtc = DateTime.UtcNow;
            if (!await TrySaveAsync(deposit, ct)) continue;
            await LogEventAsync(contractId, "deposit_paid",
                $"Deposit of {Money(transfer.Amount, deposit.Currency)} paid by bank transfer{(transfer.DebtorName != null ? $" from {transfer.DebtorName}" : "")} ({transfer.FileName})", ct);
            var settled = await SettleAsync(organizationId, contractId, null, ct);
            return settled == null
                ? new DepositTransferOutcome(true, null, null, null, KeptNote)
                : new DepositTransferOutcome(true, settled.MemberId, settled.MemberName, settled.LedgerPaymentId, null);
        }
        return new DepositTransferOutcome(false, null, null, null, "The deposit kept changing while the transfer was recorded: import the file again.");
    }

    private const string KeptNote = "A contract's deposit: kept until the club admits the member, then put on their account.";

    // ── putting it on the account ────────────────────────────────────────────

    public async Task<DepositSettlement?> SettleAsync(Guid organizationId, Guid contractId, Guid? userId, CancellationToken ct = default)
    {
        var deposit = await _db.ContractDeposits.AsNoTracking().FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.ContractId == contractId, ct);
        if (deposit is not { Status: ContractDepositStatuses.Paid, LedgerPaymentId: null }) return null;
        var amount = (deposit.AmountPaid ?? deposit.Amount) - deposit.AmountRefunded;
        if (amount <= 0) return null;
        var contract = await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId && c.OrganizationId == organizationId, ct);
        if (contract == null) return null;
        // Paid on a contract withdrawn or declined since: no agreement stands behind the money, so
        // it is the signer's to get back (the contract's trail says so), and whoever the club
        // admits later, by another contract or a reviewer's approval of the same lead, does not
        // get it as credit. Put there by itself, a deposit staff had already paid back by bank
        // would be paid out twice.
        if (Withdrawn(contract.Status)) return null;

        // The member the club's approval admitted for this contract, or for its lead. Nobody
        // admitted yet: the deposit waits, paid, until someone is.
        var leadId = contract.LeadId;
        var application = (await _db.ClubApplications.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && a.Status == ApplicationStatuses.Approved && a.MemberId != null
                    && (a.ContractId == contractId || (leadId != null && a.LeadId == leadId)))
                .ToListAsync(ct))
            .OrderByDescending(a => a.ContractId == contractId).ThenByDescending(a => a.ApprovedAtUtc)
            .FirstOrDefault();
        if (application == null) return null;
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == application.MemberId && m.OrganizationId == organizationId, ct);
        if (member == null || member.ErasedAtUtc != null)
        {
            await LogEventAsync(contractId, "deposit_not_recorded", "The deposit could not be put on an account: the member it was for is no longer on the club's list.", ct);
            return null;
        }

        // What the caller has pending is theirs: a failure here lets go only of what this started.
        var pendingBefore = _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        try
        {
            // The dues and the initiation fee the deposit pays for, when whoever approved could not
            // set them up: the applicant has paid for them, so the deposit's hook finishes them.
            var moneyFinished = false;
            if (application.MoneyPending)
            {
                var actor = userId ?? application.ApprovedByUserId ?? contract.CreatedByUserId;
                var finished = await _admissions.AdmitForDepositAsync(organizationId, application.Id, actor, deposit.Id, ct);
                if (finished.IsSuccess) moneyFinished = finished.Value.DuesStarted || finished.Value.FeeCharged;
                else _logger.LogWarning("Contract {ContractId}: the deposit could not finish the dues and fee of application {ApplicationId}: {Error}",
                    contractId, application.Id, finished.Error.Description);
                application = await _db.ClubApplications.AsNoTracking().FirstAsync(a => a.Id == application.Id, ct);
            }

            // The first dues, when the club bills automatically: the daily run would bill the
            // period that has started anyway, and billed now the deposit can pay it.
            var payFirst = new List<Guid>();
            if (application.InitiationChargeId is { } fee) payFirst.Add(fee);
            if (application.DuesSubscriptionId is { } subscription)
            {
                var rules = await _rules.ReadAsync(organizationId, ct);
                if (rules.Billing.AutomaticBilling)
                {
                    var billed = await _ledger.BillDueAsync(organizationId, subscription, DateTime.UtcNow, 1, ct);
                    if (!billed.IsSuccess)
                        _logger.LogInformation("Contract {ContractId}: the first dues were not billed with the deposit: {Error}", contractId, billed.Error.Description);
                }
                payFirst.AddRange(await _db.ClubCharges.AsNoTracking()
                    .Where(c => c.OrganizationId == organizationId && c.SubscriptionId == subscription && c.ReversesChargeId == null)
                    .OrderBy(c => c.BillingPeriodStartUtc).Select(c => c.Id).ToListAsync(ct));
            }

            var paid = await _ledger.RecordReceivedPaymentAsync(organizationId, new ReceivedPaymentRequest
            {
                MemberId = member.Id,
                Amount = amount,
                Currency = deposit.Currency,
                Method = MethodFor(deposit.MethodType),
                ReceivedAtUtc = deposit.PaidAtUtc ?? DateTime.UtcNow,
                Reference = deposit.ProviderPaymentId,
                Notes = Clip($"Deposit on the contract \"{contract.Title}\"", 1000),
                Provider = deposit.Provider ?? PaymentProviders.Bank,
                ProviderPaymentId = deposit.ProviderPaymentId ?? deposit.Id.ToString(),
                PayFirst = payFirst,
                IdempotencyKey = LedgerKey(deposit),
                RecordedBy = "Contract deposit",
            }, ct);
            if (!paid.IsSuccess)
            {
                await LogEventAsync(contractId, "deposit_not_recorded", $"The deposit could not be put on {Name(member)}'s account: {paid.Error.Description}", ct);
                return null;
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var row = await _db.ContractDeposits.FirstOrDefaultAsync(d => d.Id == deposit.Id, ct);
                if (row == null || row.LedgerPaymentId == paid.Value.Id) break;
                row.MemberId = member.Id;
                row.LedgerPaymentId = paid.Value.Id;
                row.RecordedAtUtc = DateTime.UtcNow;
                row.UpdatedAtUtc = DateTime.UtcNow;
                if (await TrySaveAsync(row, ct)) break;
            }
            if (deposit.Provider == PaymentProviders.Bank && deposit.ProviderPaymentId is { } bankReference)
            {
                // The bank file's line learns whose payment it became.
                foreach (var line in await _db.ClubBankTransactions.Where(t => t.OrganizationId == organizationId && t.BankReference == bankReference && t.LedgerPaymentId == null).ToListAsync(ct))
                {
                    line.MemberId = member.Id;
                    line.MemberName = Clip(Name(member), 200);
                    line.LedgerPaymentId = paid.Value.Id;
                    line.Note = null;
                }
                await _db.SaveChangesAsync(ct);
            }
            await LogEventAsync(contractId, "deposit_recorded", $"Deposit of {Money(amount, deposit.Currency)} put on {Name(member)}'s account", ct);
            return new DepositSettlement(member.Id, Name(member), paid.Value.Id, amount, deposit.Currency, moneyFinished);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never a reason to fail the countersignature, the webhook or the approval that got
            // here: the deposit stays paid, and is put on the account the next time something
            // settles it (Stripe's next word on the payment, or the application approved again).
            _logger.LogError(ex, "Contract {ContractId}: the paid deposit could not be put on the member's account", contractId);
            // What failed half-way is let go of, so the caller's next save does not write it.
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) && !pendingBefore.Contains(e.Entity)).ToList())
                entry.State = EntityState.Detached;
            return null;
        }
    }

    public async Task SettleForApplicationAsync(Guid organizationId, Guid applicationId, Guid? userId, CancellationToken ct = default)
    {
        var application = await _db.ClubApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId && a.Status == ApplicationStatuses.Approved, ct);
        if (application == null) return;
        var contractIds = await _db.Contracts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId
                && (c.Id == application.ContractId || (application.LeadId != null && c.LeadId == application.LeadId)))
            .Select(c => c.Id).ToListAsync(ct);
        var waiting = await _db.ContractDeposits.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && contractIds.Contains(d.ContractId)
                && d.Status == ContractDepositStatuses.Paid && d.LedgerPaymentId == null)
            .Select(d => d.ContractId).ToListAsync(ct);
        foreach (var contractId in waiting) await SettleAsync(organizationId, contractId, userId, ct);
    }

    // ── pieces ───────────────────────────────────────────────────────────────

    private Task<ContractDeposit?> TrackedAsync(Guid organizationId, Guid contractId, CancellationToken ct) =>
        _db.ContractDeposits.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.ContractId == contractId, ct);

    /// <summary>A contract the club voided or the signer declined: what was paid on it is paid back, never put on an account.</summary>
    private static bool Withdrawn(string status) => status is ContractStatuses.Voided or ContractStatuses.Declined;

    private async Task<bool> WithdrawnAsync(Guid organizationId, Guid contractId, CancellationToken ct) =>
        Withdrawn(await _db.Contracts.AsNoTracking().Where(c => c.Id == contractId && c.OrganizationId == organizationId)
            .Select(c => c.Status).FirstOrDefaultAsync(ct) ?? "");

    /// <summary>Where a paid deposit is paid back: in Stripe, or by a transfer from the club's bank.</summary>
    private static string RefundWhere(ContractDeposit deposit) =>
        deposit.Provider == PaymentProviders.Stripe ? " in Stripe" : " by bank transfer";

    /// <summary>Saves the deposit, or lets go of it when someone else saved it first (the caller reads it again).</summary>
    private async Task<bool> TrySaveAsync(ContractDeposit deposit, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(deposit).State = EntityState.Detached;
            return false;
        }
    }

    private static void MarkPaid(ContractDeposit deposit, string provider, string providerPaymentId, decimal amount, string methodType, DateTime paidAtUtc)
    {
        deposit.Status = ContractDepositStatuses.Paid;
        deposit.Provider = provider;
        deposit.ProviderPaymentId = Clip(providerPaymentId, 200);
        deposit.MethodType = Clip(methodType, 30);
        deposit.AmountPaid = amount;
        deposit.PaidAtUtc = DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc);
        deposit.FailureMessage = null;
        deposit.CheckoutUrl = null;
        deposit.CheckoutExpiresAtUtc = null;
    }

    /// <summary>
    /// The ledger's key for the deposit's money: the provider's id, as every other payment from
    /// outside is keyed, so it is recorded once whichever way it arrives.
    /// </summary>
    private static string LedgerKey(ContractDeposit deposit) => deposit.Provider switch
    {
        PaymentProviders.Stripe => $"stripe:{deposit.ProviderPaymentId}",
        PaymentProviders.Bank when deposit.ProviderPaymentId is { } reference => $"bank:{reference}"[..Math.Min(100, reference.Length + 5)],
        _ => $"deposit:{deposit.Id:N}",
    };

    /// <summary>The ledger's word for how the money came.</summary>
    private static string MethodFor(string? methodType) => methodType switch
    {
        "twint" => "Twint",
        "sepa_debit" => "DirectDebit",
        "bank" => "Wire",
        _ => "Card",
    };

    /// <summary>Why the signer cannot pay now, or null when they can.</summary>
    private static Error? Refusal(Contract contract, ContractDeposit deposit)
    {
        if (deposit.Status is ContractDepositStatuses.Paid or ContractDepositStatuses.Processing) return DomainErrors.ContractDeposit.AlreadyPaid;
        if (deposit.Status is ContractDepositStatuses.Cancelled or ContractDepositStatuses.Refunded
            || contract.Status is ContractStatuses.Declined or ContractStatuses.Voided) return DomainErrors.ContractDeposit.Closed;
        if (contract.Status is not (ContractStatuses.SignedByClient or ContractStatuses.Countersigned)) return DomainErrors.ContractDeposit.SignFirst;
        return null;
    }

    private static bool Payable(Contract contract, ContractDeposit deposit) => Refusal(contract, deposit) == null;

    private static bool QrBillsFor(PaymentConfig config, string currency) =>
        config.QrBillProblem == null && config.Creditor != null && currency is "CHF" or "EUR";

    private static string? Iban(PaymentConfig config) => (config.QrIban ?? config.Iban) is { } iban ? Grouped(iban) : null;

    /// <summary>"CH44 3199 9123 0008 8901 2", as people type it into e-banking.</summary>
    private static string Grouped(string iban)
    {
        var compact = iban.Replace(" ", "", StringComparison.Ordinal);
        return string.Join(' ', Enumerable.Range(0, (compact.Length + 3) / 4).Select(i => compact.Substring(i * 4, Math.Min(4, compact.Length - i * 4))));
    }

    private static Dictionary<string, string> Metadata(Contract contract, ContractDeposit deposit) => new()
    {
        ["cadence_org"] = contract.OrganizationId.ToString(),
        ["cadence_deposit"] = deposit.Id.ToString(),
        ["cadence_contract"] = contract.Id.ToString(),
    };

    private async Task LogEventAsync(Guid contractId, string type, string detail, CancellationToken ct)
    {
        _db.ContractEvents.Add(new ContractEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            Type = type,
            Detail = Clip(detail, 1000),
            ActorLabel = "Deposit",
            AtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string Money(decimal amount, string currency) =>
        $"{currency} {amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

    private static string Name(ClubMember member) => $"{member.FirstName} {member.LastName}".Trim();

    private static DateTime? Utc(DateTime? at) => at is null ? null : DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);

    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
