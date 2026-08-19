namespace ACI.Application.DTOs;

/// <summary>
/// One currency's share of a total. Listed alongside the others, never converted
/// into them — there is no exchange rate anywhere in this system to convert with,
/// so adding CHF to EUR would be inventing a number.
/// </summary>
public record CurrencyTotalDto(string Currency, decimal Value, int DealCount);
