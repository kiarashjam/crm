namespace ACI.Application.Common;

// The refusals of the club's clients who are not members: house accounts for companies and
// private clients, corporate memberships and their nominees, and private events. Codes ending
// ".NotFound" answer 404 and ".Conflict" 409 (ResultExtensions); the rest 400. Words that map
// elsewhere ("NotMember" is 403) are avoided on purpose.
public static partial class DomainErrors
{
    public static class ClientAccount
    {
        public static readonly Error KindInvalid = new(
            "ClientAccount.KindInvalid", "A client's house account is a company's or a contact's.");

        public static readonly Error CompanyNotFound = new("ClientAccount.Company.NotFound", "That company was not found.");

        public static readonly Error ContactNotFound = new("ClientAccount.Contact.NotFound", "That contact was not found.");

        public static readonly Error NotOpen = new(
            "ClientAccount.NotOpen", "This client has no house account yet. Open one first.");

        public static Error ContactIsMember(string memberName) => new(
            "ClientAccount.ContactIsMember",
            $"This contact is the member {memberName}: what they are charged goes on their member's house account, not on one of their own.");
    }

    public static class Corporate
    {
        public static readonly Error NotFound = new("CorporateMembership.NotFound", "That corporate membership was not found.");

        public static readonly Error Changed = new(
            "CorporateMembership.Changed.Conflict", "Someone else changed this corporate membership at the same moment. Reload it and try again.");

        public static readonly Error Ended = new(
            "CorporateMembership.Ended.Conflict", "This corporate membership has ended: its seats and dues can no longer change.");

        public static readonly Error NameRequired = new("CorporateMembership.NameRequired", "Give the corporate membership a name.");

        public static readonly Error SeatsInvalid = new("CorporateMembership.SeatsInvalid", "A corporate membership holds 1 to 500 nominees.");

        public static Error SeatsBelowNominees(int nominees) => new(
            "CorporateMembership.SeatsBelowNominees.Conflict",
            $"{nominees} nominees hold a seat now, so the membership cannot have fewer seats. Remove nominees first.");

        public static readonly Error RuleInvalid = new(
            "CorporateMembership.RuleInvalid", "Say who pays what the nominees run up: the company or each nominee.");

        public static readonly Error DatesInvalid = new(
            "CorporateMembership.DatesInvalid", "The renewal date must be a date after the start.");

        public static readonly Error TermInvalid = new("CorporateMembership.TermInvalid", "A term runs 1 to 60 months.");

        public static readonly Error EmailInvalid = new("CorporateMembership.EmailInvalid", "The billing email is not an email address.");

        public static readonly Error LanguageInvalid = new("CorporateMembership.LanguageInvalid", "Statements are written in English, German or French (en, de or fr).");

        public static readonly Error CompanyNotFound = new("CorporateMembership.Company.NotFound", "That company was not found.");

        public static readonly Error ContactNotFound = new("CorporateMembership.Contact.NotFound", "That billing contact was not found.");

        public static readonly Error MemberNotFound = new("CorporateMembership.Member.NotFound", "That member was not found in this club.");

        public static readonly Error NomineeNotFound = new("CorporateMembership.Nominee.NotFound", "That nominee was not found on this membership.");

        public static readonly Error MemberNotEligible = new(
            "CorporateMembership.MemberNotEligible", "Only an active or pending member can hold a corporate seat.");

        public static Error SeatsFull(int seats) => new(
            "CorporateMembership.SeatsFull.Conflict",
            $"All {seats} seats are taken. Swap a nominee, remove one, or give the membership more seats.");

        public static Error AlreadySeated(string membershipName) => new(
            "CorporateMembership.AlreadySeated.Conflict",
            $"That member already holds a seat on {membershipName}. A member holds one corporate seat at a time.");

        public static readonly Error ReasonRequired = new("CorporateMembership.ReasonRequired", "Say why, in a few words (up to 500 characters).");

        public static Error TooLong(string what, int max) => new("CorporateMembership.TooLong", $"{what} can be at most {max} characters.");
    }
}
