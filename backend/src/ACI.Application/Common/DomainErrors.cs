using System.Globalization;

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
    /// Club event attendee errors.
    /// </summary>
    public static class EventAttendee
    {
        // Codes ending ".NotFound" map to 404.
        public static readonly Error EventNotFound = new(
            "ClubEvent.NotFound", "The event was not found");

        public static readonly Error NotFound = new(
            "EventAttendee.NotFound", "That person is not on this event's list");

        public static readonly Error MemberNotFound = new(
            "EventAttendeeMember.NotFound", "That member is not in this workspace");

        public static readonly Error NameRequired = new(
            "EventAttendee.NameRequired", "A guest needs a name");

        public static readonly Error TooLong = new(
            "EventAttendee.TooLong", "A name can be up to 200 characters and an email up to 320");

        public static readonly Error TicketInvalid = new(
            "EventAttendee.TicketInvalid", "A ticket is a Member or a Guest ticket");

        public static readonly Error StatusInvalid = new(
            "EventAttendee.StatusInvalid", "An attendee is Reserved, CheckedIn, Waitlisted or Cancelled");

        // Mapped to 409 in ResultExtensions: the event's state, not the request, is the problem.
        public static readonly Error EventFull = new(
            "EventAttendee.EventFull", "The event is full: cancel a reservation first");

        // Also 409: someone else already checked this person in, and the screen asking
        // has not caught up yet.
        public static readonly Error AlreadyCheckedIn = new(
            "EventAttendee.AlreadyCheckedIn", "This person is already checked in");
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

        /// <summary>A 409: the code holds "Duplicate".</summary>
        public static Error UnarchiveDuplicateEmail(string email) => new(
            "Contact.DuplicateEmailOnRestore",
            $"An active contact already uses '{email}', so this one cannot be restored beside it. Merge the two, or change one of the emails first");

        /// <summary>A 409: the code ends ".Conflict".</summary>
        public static readonly Error Changed = new(
            "Contact.Changed.Conflict",
            "Someone else changed this contact after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.");
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

        /// <summary>A 409: the code ends ".Conflict".</summary>
        public static readonly Error Changed = new(
            "Company.Changed.Conflict",
            "Someone else changed this company after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.");
    }

    /// <summary>
    /// Errors for importing a file of leads, contacts or companies, and for undoing one.
    /// </summary>
    public static class Import
    {
        public static readonly Error InvalidKind = new(
            "Import.InvalidKind", "You can import leads, contacts or companies");

        public static readonly Error Unreadable = new(
            "Import.Unreadable", "The file could not be read. Upload it again");

        public static Error TooLarge(int megabytes) => new(
            "Import.TooLarge", $"The file is larger than {megabytes} MB. Split it into smaller files and import them one after another");

        public static readonly Error Empty = new(
            "Import.Empty", "The file is empty");

        public static readonly Error NoRows = new(
            "Import.NoRows", "The file has a header row but no rows under it");

        public static Error TooManyRows(int max) => new(
            "Import.TooManyRows", string.Create(CultureInfo.InvariantCulture,
                $"The file has more than {max:N0} rows. Import up to {max:N0} at a time"));

        public static Error UnclosedQuote(int row) => new(
            "Import.UnclosedQuote", $"Row {row} opens a quoted cell that never closes, so the rest of the file would be read as one cell. Check the quotes in that row");

        public static readonly Error ColumnsMismatch = new(
            "Import.ColumnsMismatch", "The column choices don't match the file's columns. Choose the columns again");

        public static Error UnknownField(string field) => new(
            "Import.UnknownField", $"\"{field}\" is not a field you can import into");

        public static Error FieldMappedTwice(string label) => new(
            "Import.FieldMappedTwice", $"Two columns are going into {label}. Choose one");

        public static Error FieldRequired(string label) => new(
            "Import.FieldRequired", $"Choose the column that holds {label}");

        public static readonly Error InvalidDuplicates = new(
            "Import.InvalidDuplicates", "Rows that match a record already here are either skipped or used to update it");

        public static readonly Error NotFound = new(
            "Import.NotFound", "The import was not found");

        // "NotOwner" in the code is what maps this to 403 Forbidden.
        public static readonly Error NotOwnerOrManager = new(
            "Import.NotOwnerOrManager", "Only whoever ran the import, or the workspace's owner or a manager, can undo it");

        // Codes ending ".Conflict" map to 409.
        public static readonly Error StillRunning = new(
            "Import.Running.Conflict", "This import is still running. Undo it once it has finished");
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

        /// <summary>A 409: the code ends ".Conflict".</summary>
        public static readonly Error Changed = new(
            "Lead.Changed.Conflict",
            "Someone else changed this lead after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.");
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

        public static readonly Error LeadContactDeleted = new(
            "ContactRelationship.LeadContactDeleted",
            "This lead's contact was deleted, so relationships are not recorded on the lead");
    }

    /// <summary>
    /// Errors for the team's "not the same" decisions on duplicates. Not "Duplicate.": a code
    /// with that word in it is answered as a conflict (409), and these are bad requests.
    /// </summary>
    public static class Dedupe
    {
        public static readonly Error InvalidKind = new(
            "Dedupe.InvalidKind", "Unknown kind of record. Use one of: " + string.Join(", ", ACI.Domain.Entities.DuplicateKinds.All));

        public static Error RecordCount(int max) => new(
            "Dedupe.RecordCount", $"Name between two and {max} different records");

        public static readonly Error RecordsNotInWorkspace = new(
            "Dedupe.RecordsNotInWorkspace", "Some of these records were not found in this workspace");
    }

    /// <summary>
    /// Errors for the records a write links to and the person it assigns. None ends in
    /// ".NotFound": the request is at fault, so they are 400s, and an id from another
    /// workspace reads exactly like one that does not exist.
    /// </summary>
    public static class Link
    {
        public static readonly Error LeadNotFound = new(
            "Link.LeadNotFound", "The lead was not found in this workspace");

        public static readonly Error DealNotFound = new(
            "Link.DealNotFound", "The deal was not found in this workspace");

        public static readonly Error ContactNotFound = new(
            "Link.ContactNotFound", "The contact was not found in this workspace");

        public static readonly Error CompanyNotFound = new(
            "Link.CompanyNotFound", "The company was not found in this workspace");

        // "NotMember" in the code is what maps this to 403 Forbidden, as Lead.OwnerNotMember.
        public static readonly Error AssigneeNotMember = new(
            "Link.AssigneeNotMember", "The person you picked is not a member of this workspace");

        public static readonly Error AssigneeCannotSeeDeal = new(
            "Link.AssigneeCannotSeeDeal",
            "The person you picked cannot see this private deal, so they could not open the task. Pick someone who can, or share the deal first.");
    }

    /// <summary>
    /// Deal-related errors.
    /// </summary>
    public static class Calendar
    {
        public static readonly Error InvalidRange = new(
            "Calendar.InvalidRange", "A calendar reads up to 45 days at a time, from a start before its end.");
    }

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

        public static readonly Error AmountOutOfRange = new(
            "Deal.AmountOutOfRange", "The amount is too large: a deal holds at most 16 digits before the decimal point.");

        public static readonly Error InvalidCurrency = new(
            "Deal.InvalidCurrency", "A currency is a three-letter ISO code, such as CHF or EUR.");

        public static readonly Error InvalidStage = new(
            "Deal.InvalidStage", "That stage is not one of this workspace's pipeline stages, or not in the pipeline chosen");
        
        public static readonly Error PipelineNotFound = new(
            "Deal.PipelineNotFound", "The pipeline was not found in this workspace");

        /// <summary>A 409: the code ends ".Conflict".</summary>
        public static readonly Error Changed = new(
            "Deal.Changed.Conflict",
            "Someone else changed this deal after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.");
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

        public static readonly Error OccurredAhead = new(
            "Activity.OccurredAhead",
            "An activity is something that has happened, so it cannot be dated in the future. Plan what is ahead as a task.");

        public static readonly Error OccurredTooEarly = new(
            "Activity.OccurredTooEarly", "That date is too long ago to be when this happened. Check the year.");

        public static Error NoOutcome(string type) => new(
            "Activity.NoOutcome", $"A {Kind(type)} has no outcome. Outcomes are for calls and meetings.");

        public static Error InvalidOutcome(string type, IReadOnlyList<string> allowed) => new(
            "Activity.InvalidOutcome", $"A {Kind(type)} outcome is one of: {string.Join(", ", allowed)}");

        public static Error NoDuration(string type) => new(
            "Activity.NoDuration", $"A {Kind(type)} has no duration. Durations are for calls and meetings.");

        public static readonly Error InvalidDuration = new(
            "Activity.InvalidDuration", $"The duration is in minutes, from 0 to {ActivityFacts.MaxDurationMinutes}.");

        public static Error NoDirection(string type) => new(
            "Activity.NoDirection", $"A {Kind(type)} has no direction. Inbound and outbound are for calls and emails.");

        public static readonly Error InvalidDirection = new(
            "Activity.InvalidDirection", "The direction is inbound or outbound.");

        private static string Kind(string type) => type.Replace('_', ' ');
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

        // Turning 2FA on or off in Settings, where the person is already signed in: a
        // wrong code or password there is a typo, so 400. The 401 that a failed sign-in
        // gets makes the browser treat the session as over and sign the person out.
        public static readonly Error WrongTwoFactorCode = new(
            "Auth.WrongTwoFactorCode", "The two-factor authentication code is invalid");

        public static readonly Error WrongPassword = new(
            "Auth.WrongPassword", "The password is incorrect");

        public static readonly Error TwoFactorRequired = new(
            "Auth.TwoFactorRequired", "Two-factor authentication is required");
        
        public static readonly Error PasswordTooWeak = new(
            "Auth.PasswordTooWeak", "The password does not meet security requirements");
        
        public static readonly Error AccountLocked = new(
            "Auth.AccountLocked", "Too many failed attempts. Wait 15 minutes, then try again.");

        public static readonly Error InvalidResetToken = new(
            "Auth.InvalidResetToken", "This password reset link is invalid or has expired. Request a new one.");

        public static readonly Error InvalidVerificationToken = new(
            "Auth.InvalidVerificationToken", "This confirmation link is invalid or has expired. Sign in and send a new one.");

        // Mapped to 403 in ResultExtensions: the account is real, it just has not shown it owns the address.
        public static readonly Error EmailNotVerified = new(
            "Auth.EmailNotVerified", "Confirm your email address first. Open the link we sent you, or send a new one from the banner at the top of the page.");

        public static readonly Error TooManyVerificationEmails = new(
            "Auth.TooManyVerificationEmails", "We have sent several confirmation emails already. Wait 15 minutes, then try again.");

        public static readonly Error VerificationEmailNotSent = new(
            "Auth.VerificationEmailNotSent", "The confirmation email could not be sent. Try again later.");
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

        public static readonly Error InvalidCurrency = new(
            "Organization.InvalidCurrency", "A currency is a three-letter ISO code, such as CHF or EUR.");

        public static readonly Error InvalidLocale = new(
            "Organization.InvalidLocale", "A locale is a language with an optional region, such as de-CH or fr-CH.");

        public static readonly Error InvalidTimeZone = new(
            "Organization.InvalidTimeZone", "A time zone is an IANA name, such as Europe/Zurich.");

        public static readonly Error InvalidStaleDealDays = new(
            "Organization.InvalidStaleDealDays", "A deal is stale after a whole number of days from 1 to 365, or 0 to never flag deals.");
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

        public static readonly Error InvalidStaleDealDays = new(
            "Pipeline.InvalidStaleDealDays", "A deal is stale after a whole number of days from 1 to 365, or 0 to never flag deals in this pipeline.");
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
    /// <summary>
    /// The deals board.
    /// </summary>
    public static class DealBoard
    {
        public static readonly Error ColumnNotFound = new(
            "DealBoardColumn.NotFound", "That column is not one of this board's stages.");

        public static readonly Error InvalidSort = new(
            "DealBoard.InvalidSort", "Cards are sorted by close, value, created or name.");
    }

    public static class DealStage
    {
        public static readonly Error NotFound = new(
            "DealStage.NotFound", "The deal stage was not found");

        public static readonly Error NameRequired = new(
            "DealStage.NameRequired", "Deal stage name is required");

        public static readonly Error HasDeals = new(
            "DealStage.HasDeals", "Cannot delete deal stage with existing deals");

        public static readonly Error InvalidProbability = new(
            "DealStage.InvalidProbability", "A stage's probability is a whole number from 0 to 100.");
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

        public static Error SubscriptionNotBillable(string status) => new(
            "Subscription.NotBillable.Conflict", status == "Paused"
                ? "This subscription is paused, so nothing was billed. Resume it to bill it."
                : "This subscription was cancelled, so nothing was billed.");

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

        public static readonly Error PaymentNotFound = new(
            "Payment.NotFound", "That payment was not found.");

        public static readonly Error ReasonRequired = new(
            "Club.ReasonRequired", "Say why this is being voided. The reason stays on the record.");

        public static readonly Error ChargeCannotDelete = new(
            "Charge.CannotDelete", "A charge can't be deleted. An owner or manager can void it, with a reason, and the record stays.");

        public static readonly Error ChargeHasPayments = new(
            "Charge.HasPayments.Conflict", "This charge has payments against it. Void those payments first, then the charge.");

        public static readonly Error PaymentVoided = new(
            "Payment.Voided.Conflict", "This payment is already voided.");

        public static readonly Error GiftCardVoided = new(
            "GiftCard.Voided.Conflict", "This gift card is already voided.");

        public static readonly Error PaymentChargeRequired = new(
            "Payment.ChargeRequired", "Choose the charge this payment settles. A payment against no charge would not change what the member owes.");

        public static readonly Error ChargeAlreadyPaid = new(
            "Charge.Paid.Conflict", "This charge is already paid in full.");

        public static readonly Error ChargeVoided = new(
            "Charge.Voided.Conflict", "This charge was voided, so there is nothing to pay or void on it.");

        public static readonly Error ChargeChanged = new(
            "Charge.Changed.Conflict", "This charge was paid or changed somewhere else at the same moment, so nothing was saved. Reload and try again.");

        /// <remarks>
        /// The payment record kept the whole amount while the charge took only what it was
        /// owed, so the rest vanished: no credit, and a statement that disagreed with the
        /// balance. There is no house-credit ledger yet, so the overpayment is refused.
        /// </remarks>
        public static Error Overpaid(decimal owed, decimal amount) => new(
            "Payment.Overpaid", string.Create(CultureInfo.InvariantCulture,
                $"That charge has {owed:0.00} left to pay, so a payment of {amount:0.00} would be more than is owed. Record {owed:0.00} or less."));

        public static Error DateInvalid(string what) => new(
            "Club.DateInvalid", $"{what} must be a date.");

        public static Error TooLong(string what, int max) => new(
            "Club.TooLong", $"{what} can be at most {max} characters.");
    }

    /// <summary>
    /// Reports and forecasts.
    /// </summary>
    public static class Reporting
    {
        public static readonly Error InvalidPeriod = new(
            "Reporting.InvalidPeriod", "A report's period has to start before it ends and cover at most five years.");

        public static readonly Error InvalidScope = new(
            "Reporting.InvalidScope", "A report's scope is me, team or org.");

        public static readonly Error TeamNotFound = new(
            "Team.NotFound", "That team is not one of this workspace's.");

        public static readonly Error NoTeam = new(
            "Reporting.NoTeam", "You are in no sales team here. Pick a team, or ask a manager to add you to one.");

        public static readonly Error InvalidMonth = new(
            "Reporting.InvalidMonth", "A forecast month is a year and a month from 1 to 12, for at most 24 months.");

        public static readonly Error MonthOver = new(
            "Reporting.MonthOver", "That month is over. A forecast is saved before or during the month it is for, not after.");

        public static readonly Error MonthTooFar = new(
            "Reporting.MonthTooFar", "A forecast can be saved for at most twelve months ahead.");

        public static readonly Error InvalidCurrency = new(
            "Reporting.InvalidCurrency", "A currency is a code of at most eight letters, such as CHF.");

        public static readonly Error SnapshotNotFound = new(
            "ForecastSnapshot.NotFound", "That saved forecast is not one of this month's.");

        public static readonly Error NothingToSave = new(
            "Reporting.NothingToSave", "There are no open deals, so there is no forecast to save.");
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
