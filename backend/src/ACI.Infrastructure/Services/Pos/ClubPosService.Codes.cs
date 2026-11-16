using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Common.Payments;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Pos;

/// <summary>
/// Members' codes: what names a member at the POS. A member number the club already uses, the
/// code on their card, or one Cadence makes (nine digits, the last a check digit, so a mistyped
/// one is not someone else's), shown with its QR code for a card or a phone to carry.
/// </summary>
public sealed partial class ClubPosService
{
    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9._-]{2,39}$", RegexOptions.Compiled);

    public async Task<Result<IReadOnlyList<MemberCodeDto>>> MemberCodesAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        if (await MemberAsync(organizationId, memberId, ct) == null) return DomainErrors.Club.MemberNotFound;
        var codes = await _db.ClubMemberCodes.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<MemberCodeDto>>(codes.Select(ToDto).ToList());
    }

    public async Task<Result<MemberCodeDto>> AddMemberCodeAsync(Guid organizationId, Guid memberId, MemberCodeRequest request, CancellationToken ct = default)
    {
        if (await MemberAsync(organizationId, memberId, ct) == null) return DomainErrors.Club.MemberNotFound;
        var label = string.IsNullOrWhiteSpace(request.Label) ? null : Clip(request.Label.Trim(), 100);
        string code;
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            // A new number nobody has: nine digits, the last a Luhn check digit.
            code = "";
            for (var tries = 0; tries < 10 && code.Length == 0; tries++)
            {
                var candidate = Luhn(RandomNumberGenerator.GetInt32(10_000_000, 100_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (!await _db.ClubMemberCodes.AnyAsync(c => c.OrganizationId == organizationId && c.Kind == MemberCodeKinds.Card && c.Code == candidate, ct))
                    code = candidate;
            }
            if (code.Length == 0) return DomainErrors.Pos.CodeInvalid;
        }
        else
        {
            code = NormalizeCode(request.Code);
            if (!CodePattern.IsMatch(code)) return DomainErrors.Pos.CodeInvalid;
        }

        var existing = await _db.ClubMemberCodes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Kind == MemberCodeKinds.Card && c.Code == code, ct);
        if (existing != null)
        {
            // The same code given again to the same member is theirs already.
            if (existing.MemberId == memberId) return ToDto(existing);
            return DomainErrors.Pos.CodeTaken(await NameAsync(organizationId, existing.MemberId, ct));
        }
        var row = new ClubMemberCode
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = memberId,
            Kind = MemberCodeKinds.Card,
            Code = code,
            Label = label,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        _db.ClubMemberCodes.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Given to someone else at the same moment.
            _db.Entry(row).State = EntityState.Detached;
            return DomainErrors.Pos.CodeTaken("another member");
        }
        return ToDto(row);
    }

    public async Task<Result> RemoveMemberCodeAsync(Guid organizationId, Guid memberId, Guid codeId, CancellationToken ct = default)
    {
        var row = await _db.ClubMemberCodes.FirstOrDefaultAsync(c => c.Id == codeId && c.OrganizationId == organizationId && c.MemberId == memberId, ct);
        if (row == null) return DomainErrors.Pos.CodeNotFound;
        _db.ClubMemberCodes.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>A code as it is matched: upper case, with no spaces in it.</summary>
    internal static string NormalizeCode(string? code) =>
        new string((code ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>A POS customer's code: the POS and its id for them.</summary>
    internal static string CustomerCode(string provider, string customerId) => NormalizeCode($"{provider}:{customerId}");

    /// <summary>The digits with a Luhn check digit after them.</summary>
    public static string Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 0)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return digits + (char)('0' + (10 - sum % 10) % 10);
    }

    private async Task<string> NameAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubMembers.AsNoTracking().Where(m => m.Id == memberId && m.OrganizationId == organizationId)
            .Select(m => (m.FirstName + " " + m.LastName).Trim()).FirstOrDefaultAsync(ct) ?? "another member";

    private static MemberCodeDto ToDto(ClubMemberCode c) => new(
        c.Id, c.MemberId, c.Kind,
        // A POS customer's code is shown without the POS's name in front of it.
        c.Kind == MemberCodeKinds.PosCustomer && c.Code.IndexOf(':') is var colon and > 0 ? c.Code[(colon + 1)..] : c.Code,
        c.Label, c.CreatedAtUtc, c.CreatedByName,
        c.Kind == MemberCodeKinds.Card ? QrRows(c.Code) : null);

    /// <summary>The code's QR code, a row of 1s and 0s for each line of modules, for the page to draw.</summary>
    private static IReadOnlyList<string> QrRows(string code)
    {
        var qr = QrCode.EncodeBytes(Encoding.UTF8.GetBytes(code), 1, 10);
        var rows = new List<string>(qr.Size);
        for (var row = 0; row < qr.Size; row++)
        {
            var line = new StringBuilder(qr.Size);
            for (var column = 0; column < qr.Size; column++) line.Append(qr.IsDark(row, column) ? '1' : '0');
            rows.Add(line.ToString());
        }
        return rows;
    }
}
