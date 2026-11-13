using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// House accounts for clients who are not members: a company (its corporate membership's
/// dues, its private events, what it pays for its nominees) or a private client (their
/// private events).
/// </summary>
/// <remarks>
/// <para>
/// The ledger only knew members, so a corporate membership's dues or a private event's
/// invoice could only go on some member's account, and the company's statement did not
/// exist. A client's account is the same account a member has, keyed by the company's or the
/// contact's id: every entry on it names that id where a member's entries name the member,
/// so charges, payments, voids, refunds, statements, QR-bills and bank matching need nothing
/// of their own.
/// </para>
/// <para>
/// It is opened on purpose, never by a charge naming a stray id: the account row says whose it
/// is (<see cref="ClubHouseAccount.CompanyId"/>, <see cref="ClubHouseAccount.ContactId"/>), and
/// only an id with such a row is billed (HolderAsync). A contact who is a member is billed on
/// their member's account, so one person never has two.
/// </para>
/// </remarks>
public sealed partial class ClubLedgerService
{
    public async Task<Result<ClientAccountDto>> ClientAccountAsync(Guid? organizationId, string holderKind, Guid holderId, CancellationToken ct = default)
    {
        var kind = ClientKind(holderKind);
        if (kind == null) return DomainErrors.ClientAccount.KindInvalid;
        var found = await ClientRecordAsync(organizationId, kind, holderId, ct);
        if (!found.IsSuccess) return found.Error;
        return await ClientDtoAsync(organizationId, kind, holderId, found.Value, ct);
    }

    public async Task<Result<ClientAccountDto>> OpenClientAccountAsync(Guid? organizationId, string holderKind, Guid holderId, CancellationToken ct = default)
    {
        var kind = ClientKind(holderKind);
        if (kind == null) return DomainErrors.ClientAccount.KindInvalid;
        var found = await ClientRecordAsync(organizationId, kind, holderId, ct);
        if (!found.IsSuccess) return found.Error;
        if (kind == ClubAccountHolders.Contact
            && await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && m.ContactId == holderId && m.ErasedAtUtc == null)
                .Select(m => m.FirstName + " " + m.LastName).FirstOrDefaultAsync(ct) is { } memberName)
            return DomainErrors.ClientAccount.ContactIsMember(memberName.Trim());

        if (!await _db.ClubHouseAccounts.AnyAsync(a => a.MemberId == holderId && a.OrganizationId == organizationId, ct))
        {
            _db.ClubHouseAccounts.Add(new ClubHouseAccount
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = holderId,
                CompanyId = kind == ClubAccountHolders.Company ? holderId : null,
                ContactId = kind == ClubAccountHolders.Contact ? holderId : null,
                Currency = await WorkspaceCurrencyAsync(organizationId, ct),
                OpenedAtUtc = DateTime.UtcNow,
            });
            // Opened at the same moment by someone else (a second click, the corporate membership
            // and a private event both opening it): theirs stands, and is the one answered.
            await SaveAsync(DomainErrors.Club.AccountChanged, ct);
        }
        return await ClientDtoAsync(organizationId, kind, holderId, found.Value, ct);
    }

    /// <summary>"company" or "contact", as the routes and requests spell them; null for anything else.</summary>
    private static string? ClientKind(string? holderKind) =>
        (holderKind ?? "").Trim().ToLowerInvariant() switch
        {
            "company" or "companies" => ClubAccountHolders.Company,
            "contact" or "contacts" => ClubAccountHolders.Contact,
            _ => null,
        };

    /// <summary>The company or the contact in this workspace, by name.</summary>
    private async Task<Result<string>> ClientRecordAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct)
    {
        if (kind == ClubAccountHolders.Company)
        {
            var company = await _db.Companies.AsNoTracking().Where(c => c.Id == id && c.OrganizationId == organizationId)
                .Select(c => c.Name).FirstOrDefaultAsync(ct);
            return company == null ? DomainErrors.ClientAccount.CompanyNotFound : Truncate(company.Trim(), 200);
        }
        var contact = await _db.Contacts.AsNoTracking().Where(c => c.Id == id && c.OrganizationId == organizationId)
            .Select(c => c.Name).FirstOrDefaultAsync(ct);
        return contact == null ? DomainErrors.ClientAccount.ContactNotFound : Truncate(contact.Trim(), 200);
    }

    private async Task<ClientAccountDto> ClientDtoAsync(Guid? organizationId, string kind, Guid id, string name, CancellationToken ct)
    {
        var account = await _db.ClubHouseAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.MemberId == id && a.OrganizationId == organizationId, ct);
        if (account == null)
            return new ClientAccountDto(kind, id, name, false, await WorkspaceCurrencyAsync(organizationId, ct), 0, 0, 0, 0, null);
        var balance = await BalanceOfAsync(organizationId, id, null, ct);
        var open = await OpenChargesAsync(organizationId, id, ct);
        var credit = await UnallocatedCreditAsync(organizationId, id, ct);
        return new ClientAccountDto(kind, id, name, true, account.Currency, balance, open.Sum(c => c.Owed), credit.Sum(c => c.Left),
            open.Count, account.OpenedAtUtc);
    }
}
