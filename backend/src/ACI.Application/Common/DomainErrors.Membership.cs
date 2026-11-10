namespace ACI.Application.Common;

// The refusals of membership applications and households, kept apart from the rest so the
// sentences for one area read together. Codes ending ".NotFound" answer 404 and ".Conflict"
// 409 (ResultExtensions); avoid words that map elsewhere, such as "NotMember" (403).
public static partial class DomainErrors
{
    public static class Application
    {
        public static readonly Error NotFound = new("Application.NotFound", "That application was not found.");

        public static readonly Error NameRequired = new(
            "Application.NameRequired", "An application needs a first name, a last name and an email.");

        public static readonly Error EmailInvalid = new("Application.EmailInvalid", "That email address does not look right.");

        public static Error TooLong(string what, int max) => new("Application.TooLong", $"{what} can be at most {max} characters.");

        public static readonly Error Changed = new(
            "Application.Changed.Conflict",
            "Someone else changed this application at the same moment. Reload it and try again.");

        public static Error StatusChange(string from, string to) => new(
            "Application.StatusChange.Conflict",
            $"An application that is {Words(from)} cannot be moved to {Words(to)}.");

        public static Error NotOpen(string status) => new(
            "Application.NotOpen.Conflict",
            $"Only an application still waiting for a decision can be approved; this one is {Words(status)}.");

        public static readonly Error ApprovedLocked = new(
            "Application.Approved.Conflict",
            "This application was approved and its member made, so it can no longer be edited. Change the member instead.");

        public static readonly Error Approving = new(
            "Application.Approving.Conflict",
            "Someone else is approving this application right now. Reload in a moment to see the member.");

        public static readonly Error MemberGone = new(
            "Application.MemberGone",
            "The member this application was approved as has since been deleted, so there is nothing to finish setting up.");

        public static readonly Error HouseholdRoleInvalid = new(
            "Application.HouseholdRoleInvalid", "Say what the applicant is to the household's primary: spouse, partner, child, dependent, parent, sibling or other.");

        public static readonly Error DepositInvalid = new(
            "Application.DepositInvalid", "A deposit is an amount more than 0, received by card, cash, cheque or wire.");

        public static readonly Error FormNotFound = new("ApplicationForm.NotFound", "This application form is not open.");

        public static readonly Error FormTextTooLong = new(
            "ApplicationForm.TooLong", "The welcome can be at most 1,000 characters, and the thank-you and consent wording 500 each.");

        public static readonly Error SubmitFailed = new(
            "Application.SubmitFailed", "That could not be sent. Please check the details and try again.");

        private static string Words(string status) => status switch
        {
            ACI.Domain.Entities.ApplicationStatuses.UnderReview => "under review",
            _ => status.ToLowerInvariant(),
        };
    }

    public static class Household
    {
        public static readonly Error NotFound = new("Household.NotFound", "That household was not found.");

        public static readonly Error PersonNotFound = new("HouseholdMember.NotFound", "That person is not in this household.");

        public static readonly Error NameRequired = new("Household.NameRequired", "A household needs a name.");

        public static Error TooLong(string what, int max) => new("Household.TooLong", $"{what} can be at most {max} characters.");

        public static readonly Error PersonRequired = new(
            "Household.PersonRequired", "Pick the member or the contact to add.");

        public static readonly Error RoleInvalid = new(
            "Household.RoleInvalid", "A household role is one of spouse, partner, child, dependent, parent, sibling or other.");

        public static Error AlreadyInHousehold(string person, string household) => new(
            "Household.AlreadyIn.Conflict",
            $"{person} is already in the {household} household. Take them out of it first.");

        public static Error CoupleTaken(string household, string partner) => new(
            "Household.Couple.Conflict",
            $"The {household} household already has a spouse or partner ({partner}). Change or remove that one first.");

        public static readonly Error PrimaryCannotLeave = new(
            "Household.PrimaryCannotLeave",
            "The primary can't be taken out of their household. Make someone else the primary first, or delete the household.");

        public static Error PrimaryOfHousehold(string member, string household) => new(
            "Household.Primary.Conflict",
            $"{member} is the primary of the {household} household. Make someone else its primary first.");

        public static readonly Error RollUpForPrimary = new(
            "Household.RollUpForPrimary", "The primary's bills are their own: roll-up is for the others in the household.");

        public static Error RollUpNeedsMember(string person) => new(
            "Household.RollUpNeedsMember", $"Only a club member's bills roll up, and {person} is not a member.");

        public static readonly Error RollUpNeedsMemberPrimary = new(
            "Household.RollUpNeedsMemberPrimary",
            "Roll-up bills the household's primary, and this household's primary is not a club member.");

        public static readonly Error Changed = new(
            "Household.Changed.Conflict", "Someone else changed this household at the same moment. Reload it and try again.");

        public static Error PrimaryDecides(string person, string household, string primary) => new(
            "Household.PrimaryDecides.Conflict",
            $"{person} is in the {household} household, where {primary} is the primary. Record it from {primary}'s page or on the Households page.");

        public static Error OtherHousehold(string person, string household) => new(
            "Household.Other.Conflict",
            $"{person} belongs to the {household} household. A spouse or partner shares a household: take one of them out of theirs first.");

        public static Error SameHousehold(string person) => new(
            "Household.Same.Conflict",
            $"{person} is in the same household. Change their role in the household instead.");

        public static readonly Error PeriodInvalid = new(
            "Household.PeriodInvalid", "Pick a period that ends after it starts and spans at most five years.");
    }
}
