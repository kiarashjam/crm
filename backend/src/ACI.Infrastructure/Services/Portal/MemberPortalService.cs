using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>
/// What a signed-in member sees and does in the portal (see <see cref="IMemberPortal"/>): their
/// own records, through the services the club's staff use, with the club's rules for what a
/// member may do themselves on top.
/// </summary>
/// <remarks>
/// <para>
/// Every read and write acts for the member's club and names the member: a record is found by
/// its id and the member's, so an id that belongs to another member, another household or
/// another club is answered as one that does not exist. A household's primary also sees the
/// household's people, balances and statement, and the others' event places and bookings, but
/// acts only for themselves.
/// </para>
/// <para>
/// The club's services report refusals worded for staff, and some name other people (a booking
/// in the way names its member); those are put into a member's words here, so nothing about
/// anyone else reaches the portal.
/// </para>
/// </remarks>
public sealed partial class MemberPortalService : IMemberPortal
{
    private const int MaxPhone = 40;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClubLedger _ledger;
    private readonly IClubBilling _billing;
    private readonly IClubHouseholds _households;
    private readonly IClubPayments _payments;
    private readonly IClubPaymentSettings _paymentSettings;
    private readonly IClubEvents _events;
    private readonly IClubReservations _reservations;
    private readonly IConsentService _consent;
    private readonly IAutomationEngine? _automations;
    private readonly ILogger<MemberPortalService> _logger;
    private readonly TimeProvider _clock;

    public MemberPortalService(
        AppDbContext db,
        ITenantContext tenant,
        IClubLedger ledger,
        IClubBilling billing,
        IClubHouseholds households,
        IClubPayments payments,
        IClubPaymentSettings paymentSettings,
        IClubEvents events,
        IClubReservations reservations,
        IConsentService consent,
        ILogger<MemberPortalService> logger,
        IAutomationEngine? automations = null,
        TimeProvider? clock = null)
    {
        _db = db;
        _tenant = tenant;
        _ledger = ledger;
        _billing = billing;
        _households = households;
        _payments = payments;
        _paymentSettings = paymentSettings;
        _events = events;
        _reservations = reservations;
        _consent = consent;
        _logger = logger;
        _automations = automations;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>The member, their club and its portal settings; erased or gone is not found.</summary>
    private sealed record Who(ClubMember Member, Organization Club, MemberPortalSettings? Settings)
    {
        public Guid OrganizationId => Club.Id;
        public Guid MemberId => Member.Id;
        public int CancelHours => Settings?.MemberCancelHours ?? 24;
        public TimeZoneInfo Zone => RegionalSettings.ZoneOf(Club.TimeZone);
    }

    private async Task<Result<Who>> WhoAsync(PortalMember who, CancellationToken ct, bool tracked = false)
    {
        var members = tracked ? _db.ClubMembers : _db.ClubMembers.AsNoTracking();
        var member = await members.FirstOrDefaultAsync(
            m => m.Id == who.MemberId && m.OrganizationId == who.OrganizationId && m.ErasedAtUtc == null, ct);
        var club = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == who.OrganizationId, ct);
        if (member == null || club == null) return DomainErrors.Portal.MemberNotFound;
        var settings = await _db.MemberPortalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == who.OrganizationId, ct);
        return new Who(member, club, settings);
    }

    // ── who they are ────────────────────────────────────────────────────────────

    public async Task<Result<PortalMeDto>> MeAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        return await MeDtoAsync(found.Value, ct);
    }

    public async Task<Result<PortalMeDto>> UpdateMeAsync(PortalMember who, PortalProfileRequest request, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct, tracked: true);
        if (found.IsFailure) return found.Error;
        var member = found.Value.Member;

        if (request.Clears(nameof(PortalProfileRequest.Phone))) member.Phone = null;
        else if (request.Phone != null)
        {
            var phone = request.Phone.Trim();
            if (phone.Length > MaxPhone || phone.Any(c => !(char.IsDigit(c) || c is ' ' or '+' or '(' or ')' or '-' or '/' or '.')))
                return DomainErrors.Portal.PhoneInvalid;
            member.Phone = phone.Length == 0 ? null : phone;
        }
        if (request.Clears(nameof(PortalProfileRequest.PreferredLanguage))) member.PreferredLanguage = null;
        else if (request.PreferredLanguage != null)
        {
            if (AppLanguages.Normalise(request.PreferredLanguage) is not { } language) return DomainErrors.Portal.LanguageInvalid;
            member.PreferredLanguage = language;
        }
        await _db.SaveChangesAsync(ct);
        return await MeDtoAsync(found.Value, ct);
    }

    private async Task<PortalMeDto> MeDtoAsync(Who who, CancellationToken ct)
    {
        var m = who.Member;
        var status = await _paymentSettings.StatusAsync(who.OrganizationId, ct);
        var household = await _households.ForMemberAsync(who.OrganizationId, who.MemberId, ct);
        return new PortalMeDto
        {
            MemberId = m.Id,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            Phone = m.Phone,
            PreferredLanguage = AppLanguages.Normalise(m.PreferredLanguage),
            Tier = m.Tier,
            Status = m.Status,
            Active = IsActive(m),
            JoinedAtUtc = m.JoinedAtUtc,
            RenewsAtUtc = m.RenewsAtUtc,
            StartsOnUtc = m.StartsOnUtc,
            StartUndetermined = m.StartUndetermined,
            DuesAmount = m.DuesAmount,
            DuesFrequency = m.DuesFrequency,
            ClubName = who.Club.Name,
            Currency = who.Club.Currency,
            Locale = who.Club.Locale,
            TimeZone = who.Club.TimeZone,
            MemberCancelHours = who.CancelHours,
            PaysOnline = status.OnlinePayments,
            Household = household == null ? null : HouseholdDto(household, who.MemberId),
        };
    }

    private static PortalHouseholdDto HouseholdDto(HouseholdDto household, Guid memberId)
    {
        var primary = household.PrimaryMemberId == memberId;
        return new PortalHouseholdDto(
            household.Name,
            primary,
            household.PrimaryName,
            household.People.Select(p => new PortalHouseholdPersonDto(
                p.Name, p.Role, p.IsPrimary, p.MemberId == memberId, p.RollUpBilling,
                // Their own accounts are the primary's to see, as the household statement shows them.
                primary ? p.HouseBalance : null)).ToList());
    }

    /// <summary>Booking and signing up are open to members whose membership is active.</summary>
    private static bool IsActive(ClubMember m) => string.Equals(m.Status, "Active", StringComparison.OrdinalIgnoreCase);

    // ── their account ───────────────────────────────────────────────────────────

    public async Task<Result<PortalAccountDto>> AccountAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;

        var balance = await _ledger.BalanceAsync(me.OrganizationId, me.MemberId, null, ct);
        if (balance.IsFailure) return balance.Error;
        var charges = await _ledger.ChargesAsync(me.OrganizationId, me.MemberId, ct);
        var open = charges
            .Where(c => c.Status is not ("Paid" or "Voided") && c.Amount - c.PaidAmount > 0)
            .OrderBy(c => c.DueAtUtc ?? c.PostedAtUtc)
            .Select(c => ToCharge(c, me.MemberId))
            .ToList();

        // The statements go back to the first month the account has anything in, or to joining.
        var payments = await _ledger.PaymentsAsync(me.OrganizationId, me.MemberId, ct);
        var first = new[] { (DateTime?)me.Member.JoinedAtUtc }
            .Concat(charges.Select(c => (DateTime?)c.PostedAtUtc))
            .Concat(payments.Select(p => (DateTime?)p.ReceivedAtUtc))
            .Where(d => d != null && d.Value.Year >= 2000)
            .Min() ?? Now;
        var now = Now;
        if (first > now) first = now;

        var household = await _households.ForMemberAsync(me.OrganizationId, me.MemberId, ct);
        var mine = household?.People.FirstOrDefault(p => p.MemberId == me.MemberId);
        var rollsUp = household != null && mine != null && !mine.IsPrimary && mine.RollUpBilling;
        return new PortalAccountDto(
            balance.Value.Currency, balance.Value.Balance, balance.Value.Owed, balance.Value.Credit, open,
            MonthOf(first, me.Zone), MonthOf(now, me.Zone), rollsUp, rollsUp ? household!.PrimaryName : null);
    }

    public async Task<Result<PortalStatementDto>> StatementAsync(PortalMember who, string month, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (Month(month, me.Zone) is not var (from, to)) return DomainErrors.Portal.MonthInvalid;

        var read = await _ledger.StatementAsync(me.OrganizationId, me.MemberId, from, to, ct);
        if (read.IsFailure) return read.Error;
        var s = read.Value;
        var lines = s.Lines.Select(l => new PortalStatementLineDto(
            l.AtUtc, l.EntryType, l.Description, l.Debit, l.Credit, -l.Balance,
            l.IncurredByMemberId is Guid by && by != me.MemberId ? l.IncurredByName : null)).ToList();
        return new PortalStatementDto(month, s.FromUtc, s.ToUtc, s.Currency, -s.OpeningBalance, -s.ClosingBalance,
            s.TotalDebits, s.TotalCredits, lines, HouseholdStatement: false);
    }

    public async Task<Result<StatementFile>> StatementPdfAsync(PortalMember who, string month, bool household, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (Month(month, me.Zone) is not var (from, to)) return DomainErrors.Portal.MonthInvalid;
        if (!household) return await _billing.StatementPdfAsync(me.OrganizationId, me.MemberId, from, to, ct);

        // The household's statement is the primary's account with the household's bills on it:
        // the primary's to read.
        var theirs = await _households.ForMemberAsync(me.OrganizationId, me.MemberId, ct);
        if (theirs == null || theirs.PrimaryMemberId != me.MemberId) return DomainErrors.Portal.NotPrimary;
        return await _households.StatementPdfAsync(me.OrganizationId, theirs.Id, from, to, ct);
    }

    // ── paying ──────────────────────────────────────────────────────────────────

    public async Task<Result<PortalCardDto>> CardAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var profile = await _payments.ProfileAsync(found.Value.OrganizationId, found.Value.MemberId, ct);
        if (profile.IsFailure) return profile.Error;
        return new PortalCardDto(profile.Value.OnlinePayments, profile.Value.Card, profile.Value.AutoPayDues);
    }

    public async Task<Result<PortalRedirectDto>> StartCardSetupAsync(PortalMember who, PortalCardSetupRequest request, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (!(await _paymentSettings.StatusAsync(me.OrganizationId, ct)).OnlinePayments) return DomainErrors.Portal.PaymentsOff;
        // The same page a pay link opens: the member saves the card on Stripe's, and agrees there
        // (with the club's words) to their dues being charged to it when they asked for that.
        var link = await _payments.CreatePayLinkAsync(me.OrganizationId, Guid.Empty, me.MemberId,
            new PayLinkRequest { Purpose = "setup", AutoPay = request.AutoPay }, ct);
        if (link.IsFailure) return link.Error;
        return new PortalRedirectDto(link.Value.Url);
    }

    public async Task<Result<PortalCardDto>> RemoveCardAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var profile = await _payments.RemoveCardAsync(found.Value.OrganizationId, Guid.Empty, found.Value.MemberId, ct);
        if (profile.IsFailure) return profile.Error;
        return new PortalCardDto(profile.Value.OnlinePayments, profile.Value.Card, profile.Value.AutoPayDues);
    }

    public async Task<Result<PortalRedirectDto>> StartPaymentAsync(PortalMember who, PortalPayRequest request, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (!(await _paymentSettings.StatusAsync(me.OrganizationId, ct)).OnlinePayments) return DomainErrors.Portal.PaymentsOff;

        var named = (request.ChargeIds ?? []).Distinct().ToList();
        if (named.Count > 0)
        {
            // Their own open charges only: a charge of anyone else's is not found here.
            var open = (await _ledger.ChargesAsync(me.OrganizationId, me.MemberId, ct))
                .Where(c => c.Status is not ("Paid" or "Voided") && c.Amount - c.PaidAmount > 0)
                .Select(c => c.Id)
                .ToHashSet();
            if (named.Any(id => !open.Contains(id))) return DomainErrors.Portal.RecordNotFound;
        }
        else
        {
            var balance = await _ledger.BalanceAsync(me.OrganizationId, me.MemberId, null, ct);
            if (balance.IsFailure) return balance.Error;
            if (balance.Value.Owed <= 0) return DomainErrors.Portal.NothingToPay;
        }
        var link = await _payments.CreatePayLinkAsync(me.OrganizationId, Guid.Empty, me.MemberId,
            new PayLinkRequest { Purpose = named.Count > 0 ? "charges" : "account", ChargeIds = named.Count > 0 ? named : null }, ct);
        if (link.IsFailure) return link.Error;
        return new PortalRedirectDto(link.Value.Url);
    }

    // ── pieces ──────────────────────────────────────────────────────────────────

    private static PortalChargeDto ToCharge(ChargeDto c, Guid memberId) => new()
    {
        Id = c.Id,
        Description = c.Description,
        Kind = c.Kind,
        Amount = c.Amount,
        PaidAmount = c.PaidAmount,
        Outstanding = c.Amount - c.PaidAmount,
        Currency = c.Currency,
        Status = c.Status,
        PostedAtUtc = c.PostedAtUtc,
        DueAtUtc = c.DueAtUtc,
        IncurredByName = c.IncurredByMemberId is Guid by && by != memberId ? c.IncurredByName : null,
    };

    private static string MonthOf(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>A calendar month "yyyy-MM" in the club's time zone, as UTC [from, to); null when it is not one.</summary>
    internal static (DateTime From, DateTime To)? Month(string? month, TimeZoneInfo zone)
    {
        if (!DateOnly.TryParseExact((month ?? "").Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
            || first.Year < 2000 || first.Year > 2100)
            return null;
        return (RegionalSettings.StartOfDayUtc(first, zone), RegionalSettings.StartOfDayUtc(first.AddMonths(1), zone));
    }

    /// <summary>
    /// The club's rules for something a member did themselves, once it is committed. The member is
    /// nobody on the team, so the rules act as the workspace's owner, as they do for a lead from
    /// the website or a payment Stripe reports. A rule that fails changes nothing about what was saved.
    /// </summary>
    private async Task AnnounceAsync(string trigger, Guid organizationId, string sourceKey, string recordName, string? email, Guid memberId, Guid recordId, CancellationToken ct)
    {
        if (_automations == null) return;
        try
        {
            var owner = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => (Guid?)o.OwnerUserId).FirstOrDefaultAsync(ct);
            if (owner is not Guid actor) return;
            await _automations.DispatchAsync(trigger, new AutomationEvent(
                organizationId, actor, sourceKey, recordName, Email: email, MemberId: memberId, RecordId: recordId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The automations for {Trigger} from the member portal could not run", trigger);
        }
    }
}
