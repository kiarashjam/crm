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
            $"{(seats == 1 ? "Its one seat is" : $"All {seats} seats are")} taken. Swap a nominee, remove one, or give the membership more seats.");

        public static Error AlreadySeated(string membershipName) => new(
            "CorporateMembership.AlreadySeated.Conflict",
            $"That member already holds a seat on {membershipName}. A member holds one corporate seat at a time.");

        public static readonly Error ReasonRequired = new("CorporateMembership.ReasonRequired", "Say why, in a few words (up to 500 characters).");

        public static Error TooLong(string what, int max) => new("CorporateMembership.TooLong", $"{what} can be at most {max} characters.");
    }

    public static class PrivateEvent
    {
        public static readonly Error NotFound = new("PrivateEvent.NotFound", "That private event was not found.");

        public static readonly Error Changed = new(
            "PrivateEvent.Changed.Conflict", "Someone else changed this private event at the same moment. Reload it and try again.");

        public static readonly Error HoldIsTheEvents = new(
            "PrivateEvent.HoldIsTheEvents.Conflict",
            "This booking holds the space for a private event. Change the event's time or spaces on the private event instead.");

        public static readonly Error TitleRequired = new("PrivateEvent.TitleRequired", "Give the private event a title.");

        public static readonly Error TimesInvalid = new(
            "PrivateEvent.TimesInvalid", "A private event needs a start and an end after it, at most 14 days apart.");

        public static readonly Error GuestCountInvalid = new("PrivateEvent.GuestCountInvalid", "The number of guests is 0 to 10,000.");

        public static readonly Error ClientRequired = new(
            "PrivateEvent.ClientRequired", "Say who the event is for: link a company or a contact.");

        public static readonly Error CompanyNotFound = new("PrivateEvent.Company.NotFound", "That company was not found.");
        public static readonly Error ContactNotFound = new("PrivateEvent.Contact.NotFound", "That contact was not found.");
        public static readonly Error DealNotFound = new("PrivateEvent.Deal.NotFound", "That deal was not found.");
        public static readonly Error LineNotFound = new("PrivateEvent.Line.NotFound", "That line was not found on this event.");
        public static readonly Error GuestNotFound = new("PrivateEvent.Guest.NotFound", "That guest was not found on this event's list.");
        public static readonly Error MemberNotFound = new("PrivateEvent.Member.NotFound", "That member was not found in this club.");

        public static Error NotWhileStatus(string status) => new(
            "PrivateEvent.NotWhile.Conflict", $"This private event is {status.ToLowerInvariant()}: that cannot be done now.");

        public static readonly Error Invoiced = new(
            "PrivateEvent.Invoiced.Conflict", "The final invoice has been posted: its menu, services and client can no longer change. Void the invoice's charges first to change them.");

        public static readonly Error ClientHasMoney = new(
            "PrivateEvent.ClientHasMoney.Conflict",
            "Money for this event is already on the client's house account, so who the client is can no longer change.");

        public static readonly Error DepositAsked = new(
            "PrivateEvent.DepositAsked.Conflict", "A deposit has already been asked for this event.");

        public static readonly Error AmountInvalid = new("PrivateEvent.AmountInvalid", "An amount must be more than 0 and less than 1,000,000.");

        public static readonly Error QuantityInvalid = new("PrivateEvent.QuantityInvalid", "A quantity is more than 0 and at most 10,000, to the hundredth.");

        public static readonly Error VatInvalid = new("PrivateEvent.VatInvalid", "A VAT rate is a percentage from 0 to 100.");

        public static readonly Error NothingToInvoice = new(
            "PrivateEvent.NothingToInvoice", "The event has no menu or services to invoice. Add them first.");

        public static readonly Error DealRequiredForContract = new(
            "PrivateEvent.DealRequiredForContract", "Link the event to its deal first: the contract is drafted on the deal, with its contact as the signer.");

        public static readonly Error ContractExists = new(
            "PrivateEvent.ContractExists.Conflict", "A contract has already been drafted for this event. Void it first to draft another.");

        public static readonly Error DepositChoiceRequired = new(
            "PrivateEvent.DepositChoiceRequired", "Say what happens to the deposit: keep it, or give it back as house credit.");

        public static readonly Error ReasonRequired = new("PrivateEvent.ReasonRequired", "Say why, in a few words (up to 500 characters).");

        public static Error TooLong(string what, int max) => new("PrivateEvent.TooLong", $"{what} can be at most {max} characters.");
    }
}
