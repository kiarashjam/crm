namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>The member portal: signing in, and what a member may do there.</summary>
    public static class Portal
    {
        /// <summary>The club has its member portal turned off (403, ResultExtensions).</summary>
        public const string OffCode = "Portal.Off";

        /// <summary>A sign-in link that is unknown, used, run out or for another club: one answer for all, so it tells nothing.</summary>
        public const string LinkInvalidCode = "Portal.LinkInvalid";

        /// <summary>Too many sign-in links asked for one address (429, ResultExtensions).</summary>
        public const string TooManyLinksCode = "Portal.TooManyLinks";

        /// <summary>Something a member may not do themselves, for a reason the sentence gives (403, ResultExtensions).</summary>
        public const string NotAllowedCode = "Portal.NotAllowed";

        public static readonly Error NotFound = new("Portal.NotFound", "This member portal address is not one we know. Check the link the club gave you.");

        public static readonly Error Off = new(OffCode, "The club's member portal is closed at the moment. Ask the club if you need anything.");

        public static readonly Error LinkInvalid = new(LinkInvalidCode,
            "This sign-in link has expired or has already been used. Ask for a new one below.");

        public static readonly Error TooManyLinks = new(TooManyLinksCode,
            "Several sign-in links were sent to this address in the last few minutes. Use the latest one, or try again in a quarter of an hour.");

        public static readonly Error EmailInvalid = new("Portal.EmailInvalid", "Enter the email address the club has for you.");

        public static readonly Error MemberNotFound = new("Portal.Member.NotFound", "That member was not found.");

        /// <summary>Staff inviting members while the portal is still off.</summary>
        public static readonly Error NotOn = new("Portal.NotOn", "Turn the member portal on first, in Settings › Welcome & portal.");

        public static readonly Error MemberNoEmail = new("Portal.NoEmail", "This member has no email address to send the invitation to.");

        public static readonly Error MemberErased = new("Portal.Erased", "This member's personal data was erased: they cannot be invited.");

        public static readonly Error SettingsChanged = new("Portal.Settings.Changed.Conflict",
            "Someone else changed the member portal's settings meanwhile. Reload them and try again.");

        public static readonly Error CancelHoursInvalid = new("Portal.CancelHours", "Members may cancel up to between 0 and 720 hours (30 days) before.");

        /// <summary>A record the member named is not theirs, or not this club's: the same answer as one that does not exist.</summary>
        public static readonly Error RecordNotFound = new("Portal.Record.NotFound", "That was not found in your account.");

        public static readonly Error MonthInvalid = new("Portal.Month", "Say which month, as yyyy-MM.");

        public static readonly Error NotPrimary = new(NotAllowedCode, "Only the household's primary member sees the household's statement.");

        public static readonly Error NotActive = new(NotAllowedCode,
            "Your membership is not active at the moment, so booking and signing up for events are closed. Ask the club if you think this is a mistake.");

        public static readonly Error EventNotOpen = new(NotAllowedCode, "This event is not open for signing up.");

        public static readonly Error EventStarted = new(NotAllowedCode, "This event has started, so the list can no longer be changed here.");

        public static Error TooLateToCancel(int hours) => new(NotAllowedCode,
            $"Places and bookings can be cancelled here up to {hours} hour{(hours == 1 ? "" : "s")} before they start. Ask the club to cancel this one.");

        public static readonly Error NotYours = new(NotAllowedCode, "Only the person it is for can cancel it here.");

        public static readonly Error AlreadyCancelled = new("Portal.AlreadyCancelled.Conflict", "This is cancelled already.");

        public static readonly Error ResourceNotBookable = new("Portal.Resource.NotFound", "That cannot be booked in the portal. Ask the club.");

        public static readonly Error BookingTimesInvalid = new("Portal.BookingTimes",
            "Choose a start in the future and an end after it, on the same or the next day, at most 180 days ahead.");

        public static readonly Error PaymentsOff = new("Portal.PaymentsOff", "The club does not take payments online yet. Pay as the statement says.");

        public static readonly Error NothingToPay = new("Portal.NothingToPay", "Nothing is owed on your account at the moment.");

        public static readonly Error ConsentChannelInvalid = new("Portal.Consent", "Choose email, SMS or post, for news and offers or for your membership's messages.");

        public static readonly Error PhoneInvalid = new("Portal.Phone", "A phone number is at most 40 characters: digits, spaces, + ( ) - and /.");

        public static readonly Error LanguageInvalid = new("Portal.Language", "The language is English (en), German (de) or French (fr).");

        /// <summary>The club's own portal's request for a hand-over sign-in is not proven by the hand-over's secret (401).</summary>
        public static readonly Error HandoffRefused = new("General.Unauthorized", "That hand-over and signature do not match.");

        public static readonly Error HandoffNoMember = new("Portal.Handoff.NoMember",
            "This hand-over added nobody to the club's members, so there is no club account to open.");
    }
}
