namespace ACI.Application.Common;

/// <summary>
/// Centralized domain error definitions for all entities.
/// </summary>
public static class DomainErrors
{
    /// <summary>
    /// Deal line item errors.
    /// </summary>
    public static class DealLineItem
    {
        public static readonly Error NameRequired = new(
            "DealLineItem.NameRequired", "A line item needs a name");

        public static readonly Error QuantityInvalid = new(
            "DealLineItem.QuantityInvalid", "Quantity must be more than 0");

        public static readonly Error PriceInvalid = new(
            "DealLineItem.PriceInvalid", "Unit price can't be negative");

        public static readonly Error TooLarge = new(
            "DealLineItem.TooLarge", "That quantity or price is too large");

        // Codes ending ".NotFound" map to 404.
        public static readonly Error DealNotFound = new(
            "DealLineItemDeal.NotFound", "The deal was not found");

        public static readonly Error NotFound = new(
            "DealLineItem.NotFound", "The line item was not found");
    }

    /// <summary>
    /// Attachment errors.
    /// </summary>
    public static class Attachment
    {
        public static readonly Error InvalidEntityType = new(
            "Attachment.InvalidEntityType", "Files can be attached to leads, contacts, companies and deals");

        public static readonly Error Empty = new(
            "Attachment.Empty", "The file is empty");

        public static readonly Error TooLarge = new(
            "Attachment.TooLarge", "Files can be up to 10 MB");

        public static readonly Error RecordNotFound = new(
            "AttachmentRecord.NotFound", "The record to attach this to was not found");

        public static readonly Error NotFound = new(
            "Attachment.NotFound", "The attachment was not found");
    }

    /// <summary>
    /// Custom field errors.
    /// </summary>
    public static class CustomField
    {
        public static readonly Error InvalidEntityType = new(
            "CustomField.InvalidEntityType", "Custom fields can be added to leads, contacts, companies and deals");

        public static readonly Error InvalidType = new(
            "CustomField.InvalidType", "That is not a kind of custom field");

        public static readonly Error LabelRequired = new(
            "CustomField.LabelRequired", "A custom field needs a label");

        public static readonly Error OptionsRequired = new(
            "CustomField.OptionsRequired", "A dropdown field needs at least one option");

        public static readonly Error CannotMove = new(
            "CustomField.CannotMove", "A field can't move to another kind of record; add a new one instead");

        public static readonly Error NotFound = new(
            "CustomField.NotFound", "The custom field was not found");

        public static readonly Error RecordNotFound = new(
            "CustomFieldRecord.NotFound", "The record was not found");

        // "NotOwner" in the code is what maps this to 403 Forbidden.
        public static readonly Error NotOwnerOrManager = new(
            "CustomField.NotOwnerOrManager", "Only the owner or a manager can change custom fields");

        public static Error InvalidValue(string label, string expected) => new(
            "CustomField.InvalidValue", $"{label} {expected}");
    }

    /// <summary>
    /// Audit-trail errors.
    /// </summary>
    public static class Audit
    {
        public static readonly Error InvalidEntityType = new(
            "Audit.InvalidEntityType", "The audit trail does not record that kind of record");

        public static readonly Error InvalidAction = new(
            "Audit.InvalidAction", "The audit trail does not record that action");

        public static readonly Error SummaryRequired = new(
            "Audit.SummaryRequired", "An audit line needs a summary");
    }

    /// <summary>
    /// Contract-related errors.
    /// </summary>
    public static class Contract
    {
        public static readonly Error NotFound = new(
            "Contract.NotFound", "The contract was not found");

        public static readonly Error LeadNotFound = new(
            "Contract.LeadNotFound", "The lead this contract is for was not found");

        public static readonly Error BodyRequired = new(
            "Contract.BodyRequired", "The contract needs a body before it can be sent");

        public static readonly Error CounterpartyEmailRequired = new(
            "Contract.CounterpartyEmailRequired", "The contract needs an email address to send to");

        public static readonly Error CounterpartyEmailInvalid = new(
            "Contract.CounterpartyEmailInvalid", "That email address does not look valid");

        /// <summary>
        /// Refused because the state machine does not permit it — countersigning
        /// before the client signed, editing after sending, signing twice.
        /// </summary>
        public static readonly Error NotAllowedInThisState = new(
            "Contract.NotAllowedInThisState", "That is not possible at this stage of the contract");

        /// <summary>
        /// The document could not be rendered. Only reachable through a defect, since
        /// rendering is pure and in-memory — but it must be a reported outcome rather
        /// than an empty file, which a browser shows as a broken document with no
        /// explanation at all.
        /// </summary>
        /// <summary>
        /// The stored text no longer hashes to what was frozen when the contract was
        /// sent. Unreachable through the application — editing after send is refused
        /// — so it means the row was changed from outside it.
        /// </summary>
        /// <summary>
        /// Somebody else changed the contract between our read and our write.
        /// </summary>
        /// <remarks>
        /// A normal outcome, not a fault: two people can hold the same signing link,
        /// and a CRM user can void a contract in the instant it is signed. Reported
        /// so the caller reloads and sees what actually happened, rather than having
        /// their stale snapshot written over the top.
        /// </remarks>
        public static readonly Error ChangedByAnother = new(
            "Contract.ChangedByAnother",
            "Somebody else just changed this contract — reload it and try again");

        public static readonly Error BodyChangedSinceSend = new(
            "Contract.BodyChangedSinceSend",
            "The contract text has changed since it was sent for signature, so it cannot be executed");

        public static readonly Error DocumentUnavailable = new(
            "Contract.DocumentUnavailable", "The contract document could not be produced");

        /// <summary>
        /// Sending is blocked while any template placeholder is still unfilled,
        /// rather than posting a contract that reads "Dear {{lead.name}},".
        /// </summary>
        public static readonly Error HasUnresolvedFields = new(
            "Contract.HasUnresolvedFields", "Fill in the highlighted placeholders before sending");

        public static readonly Error SignatureNameRequired = new(
            "Contract.SignatureNameRequired", "Type your full name as your signature");

        public static readonly Error ConsentRequired = new(
            "Contract.ConsentRequired", "Confirm you agree to be bound by this contract");

        public static readonly Error LinkInvalid = new(
            "Contract.LinkInvalid", "This signing link is not valid");

        public static readonly Error LinkExpired = new(
            "Contract.LinkExpired", "This signing link has expired. Ask for a new one.");
    }

    /// <summary>
    /// Contact-related errors.
    /// </summary>
    public static class Contact
    {
        public static readonly Error NotFound = new(
            "Contact.NotFound", "The contact was not found");
        
        public static readonly Error NameRequired = new(
            "Contact.NameRequired", "Contact name is required");
        
        public static readonly Error EmailRequired = new(
            "Contact.EmailRequired", "Contact email is required");
        
        public static readonly Error EmailInvalid = new(
            "Contact.EmailInvalid", "The email format is invalid");
        
        public static readonly Error DuplicateEmail = new(
            "Contact.DuplicateEmail", "A contact with this email already exists");
        
        public static readonly Error AlreadyArchived = new(
            "Contact.AlreadyArchived", "The contact is already archived");
        
        public static readonly Error NotArchived = new(
            "Contact.NotArchived", "The contact is not archived");
        
        public static readonly Error DoNotContact = new(
            "Contact.DoNotContact", "This contact is marked as Do Not Contact. Remove the flag before performing this action.");
        
        public static Error DuplicateEmailWithValue(string email) => new(
            "Contact.DuplicateEmail", $"A contact with email '{email}' already exists");
    }

    /// <summary>
    /// Company-related errors.
    /// </summary>
    public static class Company
    {
        public static readonly Error NotFound = new(
            "Company.NotFound", "The company was not found");
        
        public static readonly Error NameRequired = new(
            "Company.NameRequired", "Company name is required");
        
        public static readonly Error DuplicateName = new(
            "Company.DuplicateName", "A company with this name already exists");
        
        public static readonly Error DomainInvalid = new(
            "Company.DomainInvalid", "The domain format is invalid");
    }

    /// <summary>
    /// Lead-related errors.
    /// </summary>
    public static class Lead
    {
        public static readonly Error NotFound = new(
            "Lead.NotFound", "The lead was not found");
        
        public static readonly Error NameRequired = new(
            "Lead.NameRequired", "Lead name is required");
        
        public static readonly Error EmailRequired = new(
            "Lead.EmailRequired", "Lead email is required");
        
        public static readonly Error EmailInvalid = new(
            "Lead.EmailInvalid", "The email format is invalid");
        
        public static readonly Error AlreadyConverted = new(
            "Lead.AlreadyConverted", "This lead has already been converted");
        
        public static readonly Error InvalidStatus = new(
            "Lead.InvalidStatus", "The lead status is invalid");
        
        public static readonly Error InvalidSource = new(
            "Lead.InvalidSource", "The lead source is invalid");

        public static readonly Error Duplicate = new(
            "Lead.Duplicate", "An open lead with this email already exists");

        public static readonly Error ReferrerNotFound = new(
            "Lead.ReferrerNotFound", "The referring contact was not found in this workspace");

        public static readonly Error ReferrerArchived = new(
            "Lead.ReferrerArchived", "The referring contact is archived. Restore it first, or pick another contact");

        public static readonly Error ReferrerIsSelf = new(
            "Lead.ReferrerIsSelf", "A lead cannot be referred by the contact made from that same lead");
    }

    /// <summary>
    /// Errors for how two contacts are related (spouse, parent, ...).
    /// </summary>
    public static class ContactRelationship
    {
        public static readonly Error NotFound = new(
            "ContactRelationship.NotFound", "The relationship was not found on this contact");

        public static readonly Error InvalidType = new(
            "ContactRelationship.InvalidType",
            "Unknown relationship. Use one of: " + string.Join(", ", ACI.Domain.Entities.ContactRelationshipTypes.All));

        public static readonly Error RelatedContactRequired = new(
            "ContactRelationship.RelatedContactRequired", "Pick the contact this person is related to");

        public static readonly Error RelatedContactNotInWorkspace = new(
            "ContactRelationship.RelatedContactNotInWorkspace", "The related contact was not found in this workspace");

        public static readonly Error RelatedContactArchived = new(
            "ContactRelationship.RelatedContactArchived", "The related contact is archived. Restore it first, or pick another contact");

        public static readonly Error IsSelf = new(
            "ContactRelationship.IsSelf", "A contact cannot be related to themselves");

        public static Error Duplicate(string name) => new(
            "ContactRelationship.Duplicate", $"A relationship with {name} is already recorded. Change that one instead");

        public static Error CoupleDuplicate(string name) => new(
            "ContactRelationship.CoupleDuplicate", $"{name} already has a spouse or partner recorded. Remove that one first");

        public static readonly Error LeadHasNoContact = new(
            "ContactRelationship.LeadHasNoContact", "This lead needs a name and an email before relationships can be recorded");
    }

    /// <summary>
    /// Deal-related errors.
    /// </summary>
    public static class Deal
    {
        public static readonly Error NotFound = new(
            "Deal.NotFound", "The deal was not found");

        public static readonly Error PrivacyNotOwner = new(
            "Deal.PrivacyNotOwner", "Only the deal's creator, its owner, or a manager can make it private or shared");

        public static readonly Error MergeWouldHideNotOwner = new(
            "Deal.MergeWouldHideNotOwner",
            "The duplicate is private, so merging it would make the kept deal private too. Only the kept deal's creator, its owner, or a manager can do that. Keep the private deal instead, or ask one of them.");
        
        public static readonly Error NameRequired = new(
            "Deal.NameRequired", "Deal name is required");
        
        public static readonly Error ValueRequired = new(
            "Deal.ValueRequired", "Deal value is required");
        
        public static readonly Error ValueInvalid = new(
            "Deal.ValueInvalid", "The deal value must be a valid number");
        
        public static readonly Error InvalidStage = new(
            "Deal.InvalidStage", "The deal stage is invalid");
        
        public static readonly Error PipelineNotFound = new(
            "Deal.PipelineNotFound", "The specified pipeline was not found");
    }

    /// <summary>
    /// Task-related errors.
    /// </summary>
    public static class Task
    {
        public static readonly Error NotFound = new(
            "Task.NotFound", "The task was not found");
        
        public static readonly Error TitleRequired = new(
            "Task.TitleRequired", "Task title is required");
        
        public static readonly Error InvalidStatus = new(
            "Task.InvalidStatus", "The task status is invalid");
        
        public static readonly Error InvalidPriority = new(
            "Task.InvalidPriority", "The task priority is invalid");
        
        public static readonly Error DueDateInPast = new(
            "Task.DueDateInPast", "The due date cannot be in the past");
        
        public static readonly Error AlreadyCompleted = new(
            "Task.AlreadyCompleted", "The task is already completed");
    }

    /// <summary>
    /// Activity-related errors.
    /// </summary>
    public static class Activity
    {
        public static readonly Error NotFound = new(
            "Activity.NotFound", "The activity was not found");
        
        public static readonly Error TypeRequired = new(
            "Activity.TypeRequired", "Activity type is required");
        
        public static readonly Error InvalidType = new(
            "Activity.InvalidType", "The activity type is invalid. Valid types: call, meeting, email, note, task, follow_up, deadline, video, demo");
        
        public static readonly Error NoRelatedEntity = new(
            "Activity.NoRelatedEntity", "Activity must be linked to at least one entity (contact, deal, or lead)");
        
        public static readonly Error RelatedEntityNotFound = new(
            "Activity.RelatedEntityNotFound", "The related entity was not found");
    }

    /// <summary>
    /// Template-related errors.
    /// </summary>
    public static class Template
    {
        public static readonly Error NotFound = new(
            "Template.NotFound", "The template was not found");
        
        public static readonly Error TitleRequired = new(
            "Template.TitleRequired", "Template title is required");
        
        public static readonly Error ContentRequired = new(
            "Template.ContentRequired", "Template content is required");
        
        public static readonly Error NotOwned = new(
            "Template.NotOwned", "You do not own this template");
        
        public static readonly Error CannotModifySystem = new(
            "Template.CannotModifySystem", "System templates cannot be modified");
    }

    /// <summary>
    /// Authentication-related errors.
    /// </summary>
    public static class Auth
    {
        public static readonly Error InvalidCredentials = new(
            "Auth.InvalidCredentials", "Invalid email or password");
        
        public static readonly Error EmailNotFound = new(
            "Auth.EmailNotFound", "No account found with this email");
        
        public static readonly Error EmailAlreadyExists = new(
            "Auth.EmailAlreadyExists", "An account with this email already exists");
        
        public static readonly Error InvalidTwoFactorCode = new(
            "Auth.InvalidTwoFactorCode", "The two-factor authentication code is invalid");
        
        public static readonly Error TwoFactorRequired = new(
            "Auth.TwoFactorRequired", "Two-factor authentication is required");
        
        public static readonly Error PasswordTooWeak = new(
            "Auth.PasswordTooWeak", "The password does not meet security requirements");
        
        public static readonly Error AccountLocked = new(
            "Auth.AccountLocked", "Too many failed attempts. Wait 15 minutes, then try again.");

        public static readonly Error InvalidResetToken = new(
            "Auth.InvalidResetToken", "This password reset link is invalid or has expired. Request a new one.");
    }

    /// <summary>
    /// Organization-related errors.
    /// </summary>
    public static class Organization
    {
        public static readonly Error NotFound = new(
            "Organization.NotFound", "The organization was not found");
        
        public static readonly Error NameRequired = new(
            "Organization.NameRequired", "Organization name is required");
        
        public static readonly Error NotMember = new(
            "Organization.NotMember", "You are not a member of this organization");
        
        public static readonly Error NotOwner = new(
            "Organization.NotOwner", "Only the organization owner can perform this action");
        
        public static readonly Error NotOwnerOrManager = new(
            "Organization.NotOwnerOrManager", "Only the owner or a manager can perform this action");
        
        public static readonly Error AlreadyMember = new(
            "Organization.AlreadyMember", "This user is already a member of the organization");
        
        public static readonly Error CannotRemoveOwner = new(
            "Organization.CannotRemoveOwner", "The organization owner cannot be removed");

        public static readonly Error WebhookPasswordTooLong = new(
            "Organization.WebhookPasswordTooLong", "Webhook password cannot exceed 256 characters");
    }

    /// <summary>
    /// Invite-related errors.
    /// </summary>
    public static class Invite
    {
        public static readonly Error NotFound = new(
            "Invite.NotFound", "The invite was not found");
        
        public static readonly Error Expired = new(
            "Invite.Expired", "The invite has expired");
        
        public static readonly Error AlreadyAccepted = new(
            "Invite.AlreadyAccepted", "The invite has already been accepted");
        
        public static readonly Error EmailMismatch = new(
            "Invite.EmailMismatch", "The invite was sent to a different email address");
        
        public static readonly Error InvalidToken = new(
            "Invite.InvalidToken", "The invite token is invalid");
    }

    /// <summary>
    /// Pipeline-related errors.
    /// </summary>
    public static class Pipeline
    {
        public static readonly Error NotFound = new(
            "Pipeline.NotFound", "The pipeline was not found");
        
        public static readonly Error NameRequired = new(
            "Pipeline.NameRequired", "Pipeline name is required");
        
        public static readonly Error HasDeals = new(
            "Pipeline.HasDeals", "Cannot delete pipeline with existing deals");
    }

    /// <summary>
    /// JoinRequest-related errors.
    /// </summary>
    public static class JoinRequest
    {
        public static readonly Error NotFound = new(
            "JoinRequest.NotFound", "The join request was not found");
        
        public static readonly Error AlreadyProcessed = new(
            "JoinRequest.AlreadyProcessed", "The join request has already been processed");
        
        public static readonly Error AlreadyPending = new(
            "JoinRequest.AlreadyPending", "A pending join request already exists for this user");
    }

    /// <summary>
    /// LeadSource-related errors.
    /// </summary>
    public static class LeadSource
    {
        public static readonly Error NotFound = new(
            "LeadSource.NotFound", "The lead source was not found");
        
        public static readonly Error NameRequired = new(
            "LeadSource.NameRequired", "Lead source name is required");
    }

    /// <summary>
    /// LeadStatus-related errors.
    /// </summary>
    public static class LeadStatus
    {
        public static readonly Error NotFound = new(
            "LeadStatus.NotFound", "The lead status was not found");
        
        public static readonly Error NameRequired = new(
            "LeadStatus.NameRequired", "Lead status name is required");
    }

    /// <summary>
    /// DealStage-related errors.
    /// </summary>
    public static class DealStage
    {
        public static readonly Error NotFound = new(
            "DealStage.NotFound", "The deal stage was not found");

        public static readonly Error NameRequired = new(
            "DealStage.NameRequired", "Deal stage name is required");

        public static readonly Error HasDeals = new(
            "DealStage.HasDeals", "Cannot delete deal stage with existing deals");
    }

    /// <summary>
    /// Club money records: charges, payments, gift cards, tabs, subscriptions, donations.
    /// </summary>
    /// <remarks>
    /// Codes ending ".NotFound" map to 404 and codes ending ".Conflict" to 409: a record
    /// that moved on (closed, voided, changed by someone else) rather than a bad request.
    /// </remarks>
    public static class Club
    {
        public static readonly Error MemberNotFound = new(
            "ClubMember.NotFound", "That member was not found.");

        public static readonly Error RecordNotFound = new(
            "ClubRecord.NotFound", "That record was not found.");

        public static readonly Error ChargeNotFound = new(
            "Charge.NotFound", "That charge was not found.");

        public static readonly Error GiftCardNotFound = new(
            "GiftCard.NotFound", "That gift card was not found.");

        public static readonly Error TabNotFound = new(
            "Tab.NotFound", "That tab was not found.");

        public static readonly Error SubscriptionNotFound = new(
            "Subscription.NotFound", "That subscription was not found.");

        public static readonly Error AmountInvalid = new(
            "Club.AmountInvalid", "The amount must be a number more than 0 and under 1,000,000,000.");

        public static readonly Error ChargeKindInvalid = new(
            "Charge.KindInvalid", "A charge is one of Dues, Dining, Bar, Event, Spa, Booking, Retail or Other.");

        public static readonly Error DescriptionRequired = new(
            "Charge.DescriptionRequired", "A charge needs a description.");

        public static readonly Error MethodInvalid = new(
            "Payment.MethodInvalid", "A payment is made by CardOnFile, Cash, Check, Wire or AppliedCredit.");

        public static readonly Error ExpiryInvalid = new(
            "GiftCard.ExpiryInvalid", "A gift card's expiry must be a date in the future.");

        public static readonly Error VenueInvalid = new(
            "Tab.VenueInvalid", "A tab is opened at the Bar, Dining, Spa, Rooftop or PoolDeck.");

        public static readonly Error CadenceInvalid = new(
            "Subscription.CadenceInvalid", "A subscription is billed Monthly, Quarterly, SemiAnnual or Annual.");

        public static readonly Error SubscriptionStatusInvalid = new(
            "Subscription.StatusInvalid", "A subscription can be set Active, Paused or Cancelled.");

        public static readonly Error NotEditable = new(
            "ClubRecord.NotEditable", "This record can't be edited. Void it and post a new one instead.");

        public static readonly Error Changed = new(
            "ClubRecord.Changed.Conflict", "Someone else changed this at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error GiftCardChanged = new(
            "GiftCard.Changed.Conflict", "This gift card was used somewhere else at the same moment, so nothing was redeemed. Check its balance and try again.");

        public static Error GiftCardExpired(DateTime expiredAtUtc) => new(
            "GiftCard.Expired.Conflict", $"This gift card expired on {expiredAtUtc:d MMM yyyy}, so it can't be redeemed.");

        public static Error GiftCardNotActive(string status) => new(
            "GiftCard.NotActive.Conflict", status switch
            {
                "Redeemed" => "This gift card has nothing left on it.",
                "Voided" => "This gift card was voided, so it can't be redeemed.",
                _ => "This gift card can't be redeemed.",
            });

        public static readonly Error TabChanged = new(
            "Tab.Changed.Conflict", "Someone else changed this tab at the same moment, so nothing was saved. Reload the tab and try again.");

        public static readonly Error TabClosedCannotVoid = new(
            "Tab.Closed.Conflict", "This tab is already closed and posted to the member's account, so it can't be voided. A manager can void the charge instead.");

        public static readonly Error TabItemNotFound = new(
            "TabItem.NotFound", "That item is not on this tab.");

        public static readonly Error TabItemNameRequired = new(
            "Tab.ItemNameRequired", "An item needs a name.");

        public static readonly Error TabQuantityInvalid = new(
            "Tab.QuantityInvalid", "A quantity must be more than 0 and at most 1,000, to two decimal places.");

        public static readonly Error TabPriceInvalid = new(
            "Tab.PriceInvalid", "A price must be a number from 0 to 100,000, to the cent.");

        public static Error TabNotOpen(string status) => new(
            "Tab.NotOpen.Conflict", status == "Closed"
                ? "This tab is already closed and posted to the member's account, so it can't be changed."
                : "This tab was voided, so it can't be changed.");

        public static Error DateInvalid(string what) => new(
            "Club.DateInvalid", $"{what} must be a date.");

        public static Error TooLong(string what, int max) => new(
            "Club.TooLong", $"{what} can be at most {max} characters.");
    }

    /// <summary>
    /// General/shared errors.
    /// </summary>
    public static class General
    {
        public static readonly Error Unauthorized = new(
            "General.Unauthorized", "You are not authorized to perform this action");
        
        public static readonly Error Forbidden = new(
            "General.Forbidden", "Access to this resource is forbidden");
        
        public static readonly Error ServerError = new(
            "General.ServerError", "An unexpected error occurred");
        
        public static readonly Error InvalidRequest = new(
            "General.InvalidRequest", "The request is invalid");
        
        public static readonly Error ValidationError = new(
            "General.ValidationError", "One or more validation errors occurred");
        
        public static readonly Error OrganizationRequired = new(
            "General.OrganizationRequired", "X-Organization-Id header is required");
    }
}
