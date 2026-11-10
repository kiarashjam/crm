using System.Globalization;

namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>A sale at the house-charge screen or from the POS (IClubLedger.PostSaleAsync).</summary>
    public static class Sale
    {
        public static readonly Error LinesRequired = new(
            "Sale.LinesRequired", "A sale needs at least one line, and at most 200.");

        public static readonly Error CatalogOnly = new(
            "Sale.CatalogOnly", "At the house-charge screen every line comes from the price catalogue. Add the item to the catalogue, or post a charge by hand on the house account page.");

        public static readonly Error LineNameRequired = new(
            "Sale.LineNameRequired", "Each line of a sale needs a name of at most 200 characters.");

        public static readonly Error LinePriceInvalid = new(
            "Sale.LinePriceInvalid", "A line's price is a number from 0 to 100,000, to the cent.");

        public static readonly Error LineKindInvalid = new(
            "Sale.LineKindInvalid", "A line is posted as Dues, Dining, Bar, Event, Spa, Booking, Retail or Other.");

        public static readonly Error DiscountInvalid = new(
            "Sale.DiscountInvalid", "A discount is from 0 up to what it is taken off, to the cent; a percentage is from 0 to 100.");

        public static readonly Error DiscountReasonRequired = new(
            "Sale.DiscountReasonRequired", "Say why the discount was given: it is kept with the sale and on the member's statement lines.");

        public static readonly Error TenderInvalid = new(
            "Sale.TenderInvalid", "A sale is put on the house account, or paid by card, cash or TWINT.");

        public static Error TendersDoNotAddUp(decimal total, decimal tendered) => new(
            "Sale.TendersDoNotAddUp",
            string.Create(CultureInfo.InvariantCulture, $"The sale comes to {total:0.00}, and the ways it was settled add up to {tendered:0.00}."));

        public static readonly Error NegativeTotal = new(
            "Sale.NegativeTotal", "A sale can't come to less than nothing. Take a refund back as a void or a credit on the house account page.");

        public static readonly Error Changed = new(
            "Sale.Changed.Conflict", "Something else was posted to this member's account at the same moment, so nothing was saved. Try again.");
    }

    /// <summary>The POS connection, its tickets and members' codes (IClubPos).</summary>
    public static class Pos
    {
        public static readonly Error NotConfigured = new(
            "Pos.NotConfigured", "Lightspeed is not set up on this server yet: the owner adds Cadence's Lightspeed client id and secret to the server's settings.");

        public static readonly Error NotConnected = new(
            "Pos.NotConnected", "No POS is connected. Someone who takes payments connects Lightspeed on the POS tickets page.");

        public static readonly Error ConnectExpired = new(
            "Pos.ConnectExpired", "The connection to Lightspeed took too long or was started elsewhere. Start it again from the POS tickets page.");

        public static Error ProviderRefused(string message) => new("Pos.ProviderRefused", message);

        public static Error ProviderUnreachable(string message) => new("Pos.SendFailed", message);

        public static Error SettingsInvalid(string sentence) => new("PosSettings.Invalid", sentence);

        public static readonly Error SettingsChanged = new(
            "PosSettings.Changed.Conflict", "Someone else changed the POS settings at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error ImportRunning = new(
            "Pos.ImportRunning.Conflict", "An import from the POS is already running. Its tickets show here as soon as it is done.");

        public static readonly Error TicketNotFound = new(
            "PosTicket.NotFound", "That POS ticket was not found.");

        public static readonly Error TicketChanged = new(
            "PosTicket.Changed.Conflict", "Someone else dealt with this ticket at the same moment. Reload to see what they did.");

        public static Error TicketPosted(string memberName) => new(
            "PosTicket.Posted.Conflict", $"This ticket is already on {memberName}'s account. Void its charges on the house account page if it was put on the wrong one.");

        public static readonly Error IgnoreReasonRequired = new(
            "PosTicket.ReasonRequired", "Say why this ticket is not a member's (at most 500 characters).");

        public static readonly Error CodeInvalid = new(
            "MemberCode.Invalid", "A member code is 3 to 40 letters, digits, dashes, dots or underscores, as the card or the POS shows it.");

        public static Error CodeTaken(string memberName) => new(
            "MemberCode.Duplicate", $"That code already names {memberName}. A code names one member.");

        public static readonly Error CodeNotFound = new(
            "MemberCode.NotFound", "That member code was not found.");
    }
}
