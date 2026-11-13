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
}
