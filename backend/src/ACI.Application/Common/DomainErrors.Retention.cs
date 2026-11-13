namespace ACI.Application.Common;

// The refusals of the retention and referral work: the club's rules, the at-risk score, the
// renewals board, concerns, referrers and sponsors. Codes ending ".NotFound" answer 404 and
// ".Conflict" 409 (ResultExtensions); the rest 400. Words that map elsewhere ("NotMember" is
// 403) are avoided on purpose.
public static partial class DomainErrors
{
    public static class Retention
    {
        public static readonly Error Changed = new(
            "RetentionSettings.Changed.Conflict",
            "Someone else changed these settings at the same moment. Reload them and try again.");

        public static Error Invalid(string sentence) => new("RetentionSettings.Invalid", sentence);

        public static readonly Error ConcernNotFound = new("Concern.NotFound", "That complaint or concern was not found.");

        public static readonly Error ConcernKindInvalid = new("Concern.KindInvalid", "A concern is either a complaint or a concern.");

        public static readonly Error ConcernNoteRequired = new("Concern.NoteRequired", "Say in a few words what the complaint or concern is (up to 1,000 characters).");

        public static readonly Error ConcernResolved = new("Concern.Resolved.Conflict", "That complaint or concern was resolved already.");

        public static readonly Error ScoringRunning = new(
            "Scoring.Running.Conflict", "The scores are being worked out right now. Look again in a minute.");

        public static readonly Error NotActive = new(
            "Retention.MemberNotActive", "Only an active member has an at-risk score: a member who has lapsed, left or not started yet is not scored.");
    }

    public static class Renewal
    {
        public static readonly Error Changed = new(
            "Renewal.Changed.Conflict", "Someone else changed this renewal at the same moment. Reload the board and try again.");

        public static Error StageInvalid(string stage) => new(
            "Renewal.StageInvalid", $"\"{stage}\" is not a stage a renewal can be put in: contacted, renewed, at risk, lapsed, or automatic to let the board decide.");

        public static readonly Error SourceInvalid = new("Renewal.SourceInvalid", "A renewal is of a membership or of a subscription.");

        public static readonly Error SubscriptionNotFound = new("RenewalSubscription.NotFound", "That member has no such subscription.");

        public static readonly Error NoteTooLong = new("Renewal.NoteTooLong", "A note on a renewal can be at most 1,000 characters.");

        public static readonly Error RenewUntilInvalid = new(
            "Renewal.RenewUntilInvalid", "A membership is renewed to a date after the one it ends on, within ten years.");

        public static readonly Error WindowInvalid = new(
            "Renewal.WindowInvalid", "The board looks from 1 to 365 days ahead and up to 365 days back.");

        public static readonly Error NotFound = new(
            "Renewal.NotFound", "That renewal is not on the board any more: the date it ends on has changed. Reload the board to see where it stands.");
    }

    public static class Referral
    {
        public static readonly Error ReferrerNotFound = new("Referrer.NotFound", "The member named as referrer was not found in this club.");

        public static readonly Error ProposerNotFound = new("Proposer.NotFound", "The member named as proposer was not found in this club.");

        public static readonly Error SeconderNotFound = new("Seconder.NotFound", "The member named as seconder was not found in this club.");

        public static readonly Error SameSponsor = new(
            "Sponsor.SamePerson", "The proposer and the seconder have to be two different members.");

        public static readonly Error SelfSponsor = new(
            "Sponsor.Applicant", "An applicant cannot propose, second or refer themselves.");

        public static Error NotInGoodStanding(string role, string name, string why) => new(
            "Sponsor.NotInGoodStanding", $"{name} cannot {role} by the club's rule: {why}.");

        public static readonly Error SponsorsRequired = new(
            "Sponsor.Required", "The club asks for a proposer and a seconder, both members in good standing, before an application is approved.");

        public static readonly Error SelfReferral = new("Referrer.Self", "A member cannot be their own referrer.");

        public static readonly Error NoReferrer = new("Referrer.None", "Nobody is recorded as having referred this member, so there is no bonus to give.");

        public static readonly Error NoBonus = new(
            "ReferralBonus.Off", "The club gives no referral bonus. Set one under Club settings, Retention and referrals.");

        public static readonly Error BonusGiven = new(
            "ReferralBonus.Given.Conflict", "The referral bonus for this member was given already: a member earns their referrer one bonus.");

        public static readonly Error CreditNeedsMoney = new(
            "Permission.Required", "A referral bonus paid as a credit puts money on an account, which needs the permission to take payments and charge accounts.");
    }
}
