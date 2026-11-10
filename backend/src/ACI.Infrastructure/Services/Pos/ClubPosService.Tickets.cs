using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Pos;

/// <summary>
/// The tickets staff deal with: the ones that need a member and name none, and the ones that
/// could not be posted, with what to do about each (say whose it is, try again, set it aside).
/// </summary>
/// <remarks>
/// Each ticket carries a Version, so two people dealing with the same ticket at once get one
/// answer and a 409; and its sale is posted once whatever happens, as the ledger keeps one
/// sale per ticket.
/// </remarks>
public sealed partial class ClubPosService
{
    public async Task<IReadOnlyList<PosTicketDto>> TicketsAsync(Guid organizationId, string? status, int take = 100, CancellationToken ct = default)
    {
        var query = _db.ClubPosTickets.AsNoTracking().Where(t => t.OrganizationId == organizationId);
        var wanted = (status ?? "attention").Trim().ToLowerInvariant();
        query = wanted switch
        {
            "all" => query,
            "attention" => query.Where(t => t.Status == PosTicketStatuses.Unmatched || t.Status == PosTicketStatuses.Failed),
            _ => query.Where(t => t.Status == wanted),
        };
        var map = MapOf((await _db.ClubPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct))?.MethodMapJson);
        var rows = await query.OrderByDescending(t => t.ClosedAtUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return rows.Select(t => ToDto(t, map)).ToList();
    }

    public async Task<Result<PosTicketDto>> MatchAsync(Guid organizationId, Guid userId, Guid ticketId, PosMatchRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.ClubPosTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.OrganizationId == organizationId, ct);
        if (ticket == null) return DomainErrors.Pos.TicketNotFound;
        if (request.Version is { } version && version != ticket.Version) return DomainErrors.Pos.TicketChanged;
        if (ticket.SaleId != null) return DomainErrors.Pos.TicketPosted(ticket.MemberName ?? "a member");
        if (request.MemberId is not { } memberId || await MemberAsync(organizationId, memberId, ct) is not { } member) return DomainErrors.Club.MemberNotFound;

        // Their next tickets find them by the POS's customer, so nobody matches them by hand again.
        if (request.Remember && ticket.CustomerId is { } customerId)
        {
            var code = CustomerCode(ticket.Provider, customerId);
            var existing = await _db.ClubMemberCodes.FirstOrDefaultAsync(c => c.OrganizationId == organizationId
                && c.Kind == MemberCodeKinds.PosCustomer && c.Code == code, ct);
            if (existing != null && existing.MemberId != member.Id)
            {
                var owner = await MemberAsync(organizationId, existing.MemberId, ct);
                return DomainErrors.Pos.CodeTaken(owner != null ? $"{owner.FirstName} {owner.LastName}".Trim() : "another member");
            }
            if (existing == null)
                _db.ClubMemberCodes.Add(new ClubMemberCode
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    MemberId = member.Id,
                    Kind = MemberCodeKinds.PosCustomer,
                    Code = code,
                    Label = Clip(ticket.CustomerName is { } name ? $"Lightspeed customer {name}" : "Lightspeed customer", 100),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId,
                    CreatedByName = Who(),
                });
        }
        ticket.ResolvedAtUtc = DateTime.UtcNow;
        ticket.ResolvedByUserId = userId;
        ticket.ResolvedByName = Who();
        var settings = await SettingsRowAsync(organizationId, ct);
        if (await ProcessAsync(organizationId, ticket, settings, member, "staff", countAttempt: true, ct) == Outcome.Changed)
            return DomainErrors.Pos.TicketChanged;
        return await TicketDtoAsync(organizationId, ticketId, ct);
    }

    public async Task<Result<PosTicketDto>> RetryAsync(Guid organizationId, Guid ticketId, Guid? expectedVersion, CancellationToken ct = default)
    {
        var ticket = await _db.ClubPosTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.OrganizationId == organizationId, ct);
        if (ticket == null) return DomainErrors.Pos.TicketNotFound;
        if (expectedVersion is { } version && version != ticket.Version) return DomainErrors.Pos.TicketChanged;
        if (ticket.SaleId != null) return DomainErrors.Pos.TicketPosted(ticket.MemberName ?? "a member");
        var settings = await SettingsRowAsync(organizationId, ct);
        if (await ProcessAsync(organizationId, ticket, settings, null, null, countAttempt: true, ct) == Outcome.Changed)
            return DomainErrors.Pos.TicketChanged;
        return await TicketDtoAsync(organizationId, ticketId, ct);
    }

    public async Task<Result<PosTicketDto>> IgnoreAsync(Guid organizationId, Guid userId, Guid ticketId, string? reason, Guid? expectedVersion, CancellationToken ct = default)
    {
        var why = (reason ?? "").Trim();
        if (why.Length is 0 or > 500) return DomainErrors.Pos.IgnoreReasonRequired;
        var ticket = await _db.ClubPosTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.OrganizationId == organizationId, ct);
        if (ticket == null) return DomainErrors.Pos.TicketNotFound;
        if (expectedVersion is { } version && version != ticket.Version) return DomainErrors.Pos.TicketChanged;
        if (ticket.SaleId != null) return DomainErrors.Pos.TicketPosted(ticket.MemberName ?? "a member");
        ticket.Status = PosTicketStatuses.Ignored;
        ticket.Note = why;
        ticket.ResolvedAtUtc = DateTime.UtcNow;
        ticket.ResolvedByUserId = userId;
        ticket.ResolvedByName = Who();
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Pos.TicketChanged;
        }
        return await TicketDtoAsync(organizationId, ticketId, ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>The settings a ticket is posted by; the defaults when the workspace has none (a ticket kept after disconnecting).</summary>
    private async Task<ClubPosSettings> SettingsRowAsync(Guid organizationId, CancellationToken ct) =>
        await _db.ClubPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct)
        ?? new ClubPosSettings { OrganizationId = organizationId };

    private async Task<Result<PosTicketDto>> TicketDtoAsync(Guid organizationId, Guid ticketId, CancellationToken ct)
    {
        var ticket = await _db.ClubPosTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId && t.OrganizationId == organizationId, ct);
        if (ticket == null) return DomainErrors.Pos.TicketNotFound;
        var map = MapOf((await _db.ClubPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct))?.MethodMapJson);
        return ToDto(ticket, map);
    }

    private static PosTicketDto ToDto(ClubPosTicket t, IReadOnlyDictionary<string, string> map)
    {
        var data = ReadData(t);
        return new PosTicketDto
        {
            Id = t.Id,
            Provider = t.Provider,
            ExternalId = t.ExternalId,
            ReceiptNumber = t.ReceiptNumber,
            ClosedAtUtc = t.ClosedAtUtc,
            Currency = t.Currency,
            Total = t.Total,
            OnAccountAmount = t.OnAccountAmount,
            PaidAmount = t.PaidAmount,
            TipAmount = t.TipAmount,
            Tenders = t.TendersText,
            StaffName = t.StaffName,
            TableName = t.TableName,
            CustomerName = t.CustomerName,
            CustomerReference = t.CustomerReference,
            HasCustomer = t.CustomerId != null,
            Status = t.Status,
            Note = t.Note,
            Attempts = t.Attempts,
            MemberId = t.MemberId,
            MemberName = t.MemberName,
            MatchedBy = t.MatchedBy,
            SaleId = t.SaleId,
            PointsAwarded = t.PointsAwarded,
            ImportedAtUtc = t.ImportedAtUtc,
            PostedAtUtc = t.PostedAtUtc,
            ResolvedByName = t.ResolvedByName,
            Lines = data?.Lines.Select(l => new PosTicketLineDto(l.Name, l.Quantity, l.Total, l.VatRate, l.Discount, l.DiscountName)).ToList()
                    ?? (IReadOnlyList<PosTicketLineDto>)Array.Empty<PosTicketLineDto>(),
            Payments = data?.Payments.Select(p => new PosTicketPaymentDto(p.Name ?? p.Code ?? "Payment", MeansOf(p, map) ?? "Unknown", p.Amount, p.Tip)).ToList()
                       ?? (IReadOnlyList<PosTicketPaymentDto>)Array.Empty<PosTicketPaymentDto>(),
            Version = t.Version,
        };
    }
}
