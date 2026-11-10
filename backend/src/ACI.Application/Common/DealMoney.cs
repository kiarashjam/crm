using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// What a write puts in a deal's money columns: <c>Amount</c>, the number, and
/// <c>Value</c>, the same number as text for clients from before amounts were typed.
/// </summary>
/// <remarks>
/// Deal.Value was free text that the board, the Reports page, the dashboard and the
/// company totals each read with a parser of their own, and they disagreed: "1.234,56"
/// was 1234.56 on one page and 1.23 on another. The amount is now a decimal(18,2) set
/// once, here, and the text is written from it, so there is nothing left to read
/// differently.
/// </remarks>
public static class DealMoney
{
    /// <summary>
    /// The amount a request gives a deal: the number it sends, or else the number in its
    /// text. Null, without an error, when it sends neither.
    /// </summary>
    public static Result<decimal?> AmountOf(decimal? amount, string? text)
    {
        if (amount is decimal typed)
            return MoneyText.StoredAmount(typed) is decimal stored ? stored : DomainErrors.Deal.AmountOutOfRange;
        if (string.IsNullOrWhiteSpace(text)) return Result.Success<decimal?>(null);
        if (!MoneyText.TryParseAmount(text, out var read)) return DomainErrors.Deal.ValueInvalid;
        return MoneyText.StoredAmount(read) is decimal fromText ? fromText : DomainErrors.Deal.AmountOutOfRange;
    }

    /// <summary>Puts an amount in both columns, the number and its text.</summary>
    public static void SetAmount(Deal deal, decimal amount)
    {
        deal.Amount = amount;
        deal.Value = MoneyText.ValueText(amount);
    }

    /// <summary>
    /// The currency a request names, as an ISO code, or <paramref name="workspaceCurrency"/>
    /// when it names none. A deal saved without one used to be counted and shown as USD.
    /// </summary>
    public static Result<string> CurrencyOf(string? requested, string workspaceCurrency)
    {
        if (string.IsNullOrWhiteSpace(requested)) return workspaceCurrency;
        return RegionalSettings.TryCurrency(requested, out var code) ? code : DomainErrors.Deal.InvalidCurrency;
    }

    /// <summary>
    /// The currency new deals in this workspace are priced in: the organisation's, or the
    /// default in a personal workspace, which has no organisation to hold one.
    /// </summary>
    public static async Task<string> WorkspaceCurrencyAsync(this IOrganizationRepository? organizations, Guid? organizationId, CancellationToken ct = default)
    {
        if (organizationId is not Guid id || organizations == null) return RegionalSettings.DefaultCurrency;
        var org = await organizations.GetByIdAsync(id, ct);
        return org != null && RegionalSettings.TryCurrency(org.Currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }
}
