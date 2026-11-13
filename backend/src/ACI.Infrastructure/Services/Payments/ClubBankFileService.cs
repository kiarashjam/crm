using ACI.Application.Common;
using ACI.Application.Common.Payments;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IClubBankFiles"/>
/// <remarks>
/// <para>
/// Each credit is kept by the bank's own reference for it, which the database holds once per
/// workspace, and is recorded on the ledger under that reference ("bank:…"). Importing the same
/// file again, a later file that repeats a credit, or two people importing at once, records
/// every transfer once.
/// </para>
/// <para>
/// A credit whose reference is one the club printed on a QR-bill is the member's payment: it
/// pays the statement's (or the charge's) open charges, then the member's others, and any
/// excess stays on their account as credit. Everything else waits, unmatched, for someone to
/// say whose it is or that it is not a member's at all. Debits, reversals and bookings not yet
/// final are not member payments and are left out.
/// </para>
/// </remarks>
public sealed class ClubBankFileService : IClubBankFiles
{
    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubBilling _billing;
    private readonly ClubQrBillService _qrBills;
    private readonly ICurrentUserService _user;
    private readonly ILedgerReadObserver _reads;
    private readonly ILogger<ClubBankFileService> _logger;
    private readonly IContractDeposits? _deposits;

    public ClubBankFileService(AppDbContext db, IClubLedger ledger, IClubBilling billing, ClubQrBillService qrBills, ICurrentUserService user,
        ILedgerReadObserver reads, ILogger<ClubBankFileService> logger, IContractDeposits? deposits = null)
    {
        _deposits = deposits;
        _db = db;
        _ledger = ledger;
        _billing = billing;
        _qrBills = qrBills;
        _user = user;
        _reads = reads;
        _logger = logger;
    }

    public async Task<Result<BankImportDto>> ImportAsync(Guid organizationId, Guid userId, string fileName, Stream content, CancellationToken ct = default)
    {
        CamtFile file;
        try
        {
            file = CamtReader.Read(content);
        }
        catch (CamtException ex)
        {
            return DomainErrors.Payments.BankFileInvalid(ex.Message);
        }

        var name = Clip(string.IsNullOrWhiteSpace(fileName) ? "bank file" : Path.GetFileName(fileName), 260)!;
        var import = new ClubBankImport
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FileName = name,
            Format = file.Format,
            MessageId = Clip(file.MessageId, 100),
            AccountIban = Clip(file.Accounts.FirstOrDefault(), 34),
            ImportedAtUtc = DateTime.UtcNow,
            ImportedByUserId = userId,
            ImportedByName = Clip(_user.UserName, 200),
        };
        _db.ClubBankImports.Add(import);
        await _db.SaveChangesAsync(ct);

        var reinstate = new HashSet<Guid>();
        foreach (var credit in file.Transactions)
        {
            if (!credit.IsCredit || credit.IsReversal || credit.Status != "BOOK" || credit.Amount <= 0)
            {
                import.Skipped++;
                continue;
            }
            import.Transactions++;
            if (await _db.ClubBankTransactions.AnyAsync(t => t.OrganizationId == organizationId && t.BankReference == credit.BankReference, ct))
            {
                import.AlreadyImported++;
                continue;
            }
            await _reads.ReadAsync("bank-files", ct);
            var row = new ClubBankTransaction
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ImportId = import.Id,
                BankReference = credit.BankReference,
                AccountIban = Clip(credit.AccountIban, 34),
                BookedAtUtc = credit.BookedAtUtc,
                ValueDateUtc = credit.ValueDateUtc,
                Amount = credit.Amount,
                Currency = credit.Currency.Length == 3 ? credit.Currency : "CHF",
                ReferenceType = credit.ReferenceType is "QRR" or "SCOR" ? credit.ReferenceType : null,
                Reference = Clip(credit.Reference, 40),
                DebtorName = Clip(credit.DebtorName, 140),
                DebtorIban = Clip(credit.DebtorIban, 34),
                Message = Clip(credit.Message, 500),
                Status = BankTransactionStatuses.Unmatched,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.ClubBankTransactions.Add(row);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Someone importing the same file at this moment took it first.
                _db.Entry(row).State = EntityState.Detached;
                import.AlreadyImported++;
                continue;
            }

            await MatchAsync(organizationId, row, name, ct);
            if (row.Status == BankTransactionStatuses.Matched)
            {
                import.Matched++;
                import.AmountMatched += row.Amount;
                if (row.MemberId is { } member) reinstate.Add(member);
            }
            else
            {
                import.Unmatched++;
            }
            await _db.SaveChangesAsync(ct);
        }
        await _db.SaveChangesAsync(ct);

        // A member the billing lapsed is put back once what was overdue is paid.
        foreach (var memberId in reinstate) await ReinstateAsync(organizationId, memberId, ct);
        return ToDto(import);
    }

    public async Task<IReadOnlyList<BankImportDto>> ImportsAsync(Guid organizationId, int take = 20, CancellationToken ct = default) =>
        (await _db.ClubBankImports.AsNoTracking().Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.ImportedAtUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<IReadOnlyList<BankTransactionDto>> TransactionsAsync(Guid organizationId, string? status, Guid? importId, CancellationToken ct = default)
    {
        var query = _db.ClubBankTransactions.AsNoTracking().Where(t => t.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status.Trim().ToLowerInvariant());
        if (importId is { } id) query = query.Where(t => t.ImportId == id);
        return (await query.OrderByDescending(t => t.BookedAtUtc).ThenByDescending(t => t.CreatedAtUtc).Take(500).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    public async Task<Result<BankTransactionDto>> AllocateAsync(Guid organizationId, Guid userId, Guid transactionId, Guid memberId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var row = await _db.ClubBankTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.OrganizationId == organizationId, ct);
        if (row == null) return DomainErrors.Payments.BankTransactionNotFound;
        if (row.Status is BankTransactionStatuses.Matched or BankTransactionStatuses.Allocated) return DomainErrors.Payments.BankTransactionResolved;
        if (expectedVersion is { } version && version != row.Version) return DomainErrors.Payments.BankTransactionChanged;
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;

        var paid = await RecordAsync(organizationId, row, memberId, [], $"Bank transfer allocated by {Clip(_user.UserName, 150) ?? "staff"}", ct);
        if (!paid.IsSuccess) return paid.Error;
        // Two people allocating the same transfer at once: the money went once, to whom the
        // first said, and the second is told rather than having the record say otherwise.
        if (paid.Value.MemberId != memberId) return DomainErrors.Payments.BankTransactionChanged;
        row.Status = BankTransactionStatuses.Allocated;
        row.MemberId = member.Id;
        row.MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200);
        row.LedgerPaymentId = paid.Value.Id;
        row.Note = null;
        Resolved(row, userId);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Payments.BankTransactionChanged;
        }
        await ReinstateAsync(organizationId, memberId, ct);
        return ToDto(row);
    }

    public async Task<Result<BankTransactionDto>> IgnoreAsync(Guid organizationId, Guid userId, Guid transactionId, string? reason, Guid? expectedVersion, CancellationToken ct = default)
    {
        var why = (reason ?? "").Trim();
        if (why.Length == 0) return DomainErrors.Payments.IgnoreReasonRequired;
        if (why.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        var row = await _db.ClubBankTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.OrganizationId == organizationId, ct);
        if (row == null) return DomainErrors.Payments.BankTransactionNotFound;
        if (row.Status is BankTransactionStatuses.Matched or BankTransactionStatuses.Allocated) return DomainErrors.Payments.BankTransactionResolved;
        if (expectedVersion is { } version && version != row.Version) return DomainErrors.Payments.BankTransactionChanged;
        row.Status = BankTransactionStatuses.Ignored;
        row.Note = why;
        Resolved(row, userId);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Payments.BankTransactionChanged;
        }
        return ToDto(row);
    }

    /// <summary>Records the credit as the member its reference names, or says why it waits for someone.</summary>
    private async Task MatchAsync(Guid organizationId, ClubBankTransaction row, string fileName, CancellationToken ct)
    {
        if (row.Reference == null)
        {
            row.Note = "No reference: the payer quoted none of the club's QR-bill references.";
            return;
        }
        var reference = await _qrBills.FindRowAsync(organizationId, row.Reference, ct);
        if (reference == null)
        {
            row.Note = "The reference is not one this club printed on a QR-bill.";
            return;
        }
        if (reference.Purpose == ClubQrBillService.DepositPurpose && reference.ContractId is { } contractId)
        {
            // A contract's deposit, printed before the signer was anyone's member: the deposit
            // takes it, and puts it on the member's account once the club has admitted them.
            if (_deposits == null)
            {
                row.Note = "The reference is a contract's deposit.";
                return;
            }
            var taken = await _deposits.RecordBankTransferAsync(organizationId, contractId,
                new DepositTransfer(row.BankReference, row.Amount, row.Currency, row.ValueDateUtc ?? row.BookedAtUtc, row.DebtorName, fileName), ct);
            row.Note = Clip(taken.Note, 500);
            if (!taken.Taken) return;
            row.Status = BankTransactionStatuses.Matched;
            row.PaymentReferenceId = reference.Id;
            row.MemberId = taken.MemberId;
            row.MemberName = Clip(taken.MemberName, 200);
            row.LedgerPaymentId = taken.LedgerPaymentId;
            return;
        }
        if (reference.MemberId is not { } memberId)
        {
            row.Note = "The reference names no member.";
            return;
        }
        var payFirst = reference.ChargeId is { } charge ? new[] { charge } : Array.Empty<Guid>();
        var paid = await RecordAsync(organizationId, row, memberId, payFirst, $"Bank file {fileName}", ct);
        if (!paid.IsSuccess)
        {
            row.MemberId = memberId;
            row.Note = Clip(paid.Error.Code == DomainErrors.Club.MemberNotFound.Code
                ? "The reference names a member who is no longer on the club's list."
                : paid.Error.Description, 500);
            return;
        }
        row.Status = BankTransactionStatuses.Matched;
        row.PaymentReferenceId = reference.Id;
        row.MemberId = paid.Value.MemberId;
        row.MemberName = Clip(paid.Value.MemberName, 200);
        row.LedgerPaymentId = paid.Value.Id;
        row.Note = null;
    }

    private Task<Result<PaymentDto>> RecordAsync(Guid organizationId, ClubBankTransaction row, Guid memberId, IReadOnlyList<Guid> payFirst, string recordedBy, CancellationToken ct) =>
        _ledger.RecordReceivedPaymentAsync(organizationId, new ReceivedPaymentRequest
        {
            MemberId = memberId,
            Amount = row.Amount,
            Currency = row.Currency,
            Method = "Wire",
            ReceivedAtUtc = row.ValueDateUtc ?? row.BookedAtUtc,
            Reference = row.Reference != null ? PaymentReferences.Format(row.Reference) : Clip(row.BankReference, 200),
            Notes = row.DebtorName != null ? Clip($"From {row.DebtorName}", 1000) : null,
            Provider = PaymentProviders.Bank,
            ProviderPaymentId = row.BankReference,
            PayFirst = payFirst,
            IdempotencyKey = $"bank:{row.BankReference}"[..Math.Min(100, row.BankReference.Length + 5)],
            RecordedBy = recordedBy,
        }, ct);

    private async Task ReinstateAsync(Guid organizationId, Guid memberId, CancellationToken ct)
    {
        try
        {
            await _billing.ReinstateIfSettledAsync(organizationId, memberId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The payment is recorded; the next daily run puts the member back.
            _logger.LogError(ex, "Member {MemberId} could not be put back after a bank transfer settled their account", memberId);
        }
    }

    private void Resolved(ClubBankTransaction row, Guid userId)
    {
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ResolvedByUserId = userId;
        row.ResolvedByName = Clip(_user.UserName, 200);
    }

    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private static BankImportDto ToDto(ClubBankImport i) => new(
        i.Id, i.FileName, i.Format, i.MessageId, i.AccountIban, i.Transactions, i.Matched, i.Unmatched, i.AlreadyImported, i.Skipped,
        i.AmountMatched, i.ImportedAtUtc, i.ImportedByName);

    private static BankTransactionDto ToDto(ClubBankTransaction t) => new()
    {
        Id = t.Id,
        ImportId = t.ImportId,
        BankReference = t.BankReference,
        AccountIban = t.AccountIban,
        BookedAtUtc = t.BookedAtUtc,
        ValueDateUtc = t.ValueDateUtc,
        Amount = t.Amount,
        Currency = t.Currency,
        ReferenceType = t.ReferenceType,
        Reference = t.Reference,
        DebtorName = t.DebtorName,
        DebtorIban = t.DebtorIban,
        Message = t.Message,
        Status = t.Status,
        Note = t.Note,
        MemberId = t.MemberId,
        MemberName = t.MemberName,
        LedgerPaymentId = t.LedgerPaymentId,
        ResolvedAtUtc = t.ResolvedAtUtc,
        ResolvedByName = t.ResolvedByName,
        Version = t.Version,
    };
}
