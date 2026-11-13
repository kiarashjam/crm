using System.Globalization;

namespace ACI.Application.Common;

/// <summary>
/// Centralized domain error definitions for all entities.
/// </summary>
public static partial class DomainErrors
{
    /// <summary>
    /// A member's role does not let them do this (see Domain.Common.MemberPermissions).
    /// Answered 403, with the permission in the code ("Permission.records.delete").
    /// </summary>
    public static class Permission
    {
        /// <param name="permission">The permission that is missing.</param>
        /// <param name="what">Finishes "Your role here does not let you …"; the permission's own words by default.</param>
        public static Error Missing(string permission, string? what = null) => new(
            $"Permission.{permission}",
            Domain.Common.MemberPermissions.Refusal(what ?? Domain.Common.MemberPermissions.Phrase(permission)));

        /// <summary>
        /// Nobody hands out more than they can do themselves, or changes someone who can do
        /// more. The owner and managers hold everything, so this only ever stops a member
        /// the owner let manage the team with less.
        /// </summary>
        public static readonly Error MoreThanYourOwn = new(
            $"Permission.{Domain.Common.MemberPermissions.ManageMembers}",
            "You can only give a member what you can do yourself, and only change someone who can do no more than you.");
    }

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

        public static readonly Error VatRateInvalid = new(
            "DealLineItem.VatRateInvalid", "A VAT rate is a percentage between 0 and 50 with at most two decimals, such as 8.1");

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
    /// Club events and their guest lists. A code ending ".Conflict" answers 409: the event or
    /// the list is not in a state to do it, not the request.
    /// </summary>
    public static class ClubEvent
    {
        public static readonly Error NotFound = new(
            "ClubEvent.NotFound", "The event was not found.");

        public static readonly Error RegistrationNotFound = new(
            "EventRegistration.NotFound", "That person is not on this event's list.");

        public static readonly Error MemberNotFound = new(
            "EventMember.NotFound", "That member is not in this workspace.");

        public static readonly Error HostNotFound = new(
            "EventHost.NotFound", "The member bringing the guest is not in this workspace.");

        public static readonly Error NameRequired = new(
            "ClubEvent.NameRequired", "An event needs a name.");

        public static readonly Error GuestNameRequired = new(
            "EventRegistration.NameRequired", "A guest needs a name.");

        public static readonly Error MemberRequired = new(
            "EventRegistration.MemberRequired", "Pick the member this ticket is for, or make it a guest ticket.");

        public static readonly Error TicketInvalid = new(
            "EventRegistration.TicketInvalid", "A ticket is a Member or a Guest ticket.");

        public static readonly Error StatusInvalid = new(
            "EventRegistration.StatusInvalid", "Someone on the list can be checked in (CheckedIn), cancelled (Cancelled), taken off the waitlist (Reserved) or marked a no-show (NoShow).");

        public static readonly Error EventStatusInvalid = new(
            "ClubEvent.StatusInvalid", "An event is Draft, Published, Cancelled or Completed.");

        public static readonly Error VisibilityInvalid = new(
            "ClubEvent.VisibilityInvalid", "An event is for MembersOnly, MembersAndGuests or the Public.");

        public static readonly Error TimesInvalid = new(
            "ClubEvent.TimesInvalid", "An event needs a start and an end, the end after the start and at most 31 days later.");

        public static readonly Error CapacityInvalid = new(
            "ClubEvent.CapacityInvalid", "Capacity is a whole number of seats from 0 (no limit) to 100,000.");

        public static readonly Error PriceInvalid = new(
            "ClubEvent.PriceInvalid", "A price or a fee is an amount from 0 to 1,000,000, to the cent.");

        public static readonly Error UnnamedSeatsInvalid = new(
            "ClubEvent.UnnamedSeatsInvalid", "Unnamed seats can only be released: the number can go down to 0, never up.");

        public static Error TooLong(string what, int max) => new(
            "ClubEvent.TooLong", $"{what} can be at most {max} characters.");

        public static readonly Error Changed = new(
            "ClubEvent.Changed.Conflict", "Someone else changed this event at the same moment, so nothing was saved. Reload and try again.");

        public static Error Full(int capacity) => new(
            "ClubEvent.Full.Conflict", $"The event is full: all {capacity} seats are taken. Cancel a reservation or mark a no-show first, or add them to the waitlist.");

        public static Error CapacityBelowSeats(int taken) => new(
            "ClubEvent.CapacityBelowSeats.Conflict", $"{taken} seats are taken, so the capacity can't go below {taken}. Cancel reservations first.");

        public static readonly Error AlreadyRegistered = new(
            "EventRegistration.AlreadyRegistered.Conflict", "This member is already on this event's list.");

        public static readonly Error AlreadyCheckedIn = new(
            "EventRegistration.AlreadyCheckedIn.Conflict", "This person is already checked in.");

        public static Error NotOpen(string status) => new(
            "ClubEvent.NotOpen.Conflict", status == "Cancelled"
                ? "This event was cancelled, so its list can't change."
                : "This event is completed, so nobody new can be added to its list.");

        public static Error CannotMove(string from, string to) => new(
            "EventRegistration.CannotMove.Conflict", (from, to) switch
            {
                ("Cancelled", _) => "This registration was cancelled. Register them again instead.",
                ("CheckedIn", _) => "This person has already been checked in.",
                ("NoShow", "Cancelled") => "This person was marked a no-show; check them in if they came after all.",
                (_, "NoShow") => "Only someone with a seat who did not come can be marked a no-show.",
                (_, "Reserved") => "Only someone on the waitlist can be given a seat.",
                _ => $"Someone who is {from} can't be set to {to}.",
            });

        public static readonly Error NotStarted = new(
            "ClubEvent.NotStarted.Conflict", "The event hasn't started yet, so nobody can be marked a no-show.");

        public static Error StatusStays(string from, string to) => new(
            "ClubEvent.StatusStays.Conflict", from switch
            {
                "Cancelled" => "This event was cancelled and stays cancelled. Make a new event instead.",
                "Completed" => "This event is completed and stays completed.",
                _ when to == "Completed" => "The event hasn't started yet, so it can't be marked completed.",
                _ => $"An event can't go from {from} to {to}.",
            });

        public static readonly Error HasMoney = new(
            "ClubEvent.HasMoney.Conflict", "Tickets or fees for this event are on members' accounts, so it can't be deleted. Cancel it instead: everyone's tickets are taken back and the record stays.");

        public static readonly Error RegistrationHasMoney = new(
            "EventRegistration.HasMoney.Conflict", "This person's ticket or fee is on a house account, or they came, so they can't be taken off the list. Cancel the registration instead.");

        public static readonly Error RsvpNeedsName = new(
            "ClubEvent.RsvpNeedsName", "An RSVP needs a name now: add the member or guest to the event's list, so they can be checked in at the door and charged their ticket.");
    }

    /// <summary>
    /// The club's bookable resources and their bookings. A code ending ".Conflict" answers 409.
    /// </summary>
    public static class Reservation
    {
        public static readonly Error NotFound = new(
            "Reservation.NotFound", "That booking was not found.");

        public static readonly Error ResourceNotFound = new(
            "Resource.NotFound", "That resource was not found.");

        public static readonly Error MemberNotFound = new(
            "ReservationMember.NotFound", "That member is not in this workspace.");

        public static readonly Error NameRequired = new(
            "Reservation.NameRequired", "Pick the member this booking is for, or give the guest's name.");

        public static readonly Error ResourceNameRequired = new(
            "Resource.NameRequired", "A resource needs a name.");

        public static readonly Error KindInvalid = new(
            "Resource.KindInvalid", "A resource is Dining, Meeting, Spa, Court, Studio or Lounge.");

        public static readonly Error ModeInvalid = new(
            "Resource.BookingModeInvalid", "A resource is booked Exclusive (one booking at a time) or Shared (up to its capacity).");

        public static readonly Error CapacityInvalid = new(
            "Resource.CapacityInvalid", "A resource holds from 1 to 10,000 people.");

        public static readonly Error PartyInvalid = new(
            "Reservation.PartyInvalid", "A party is from 1 to 10,000 people.");

        public static readonly Error TimesInvalid = new(
            "Reservation.TimesInvalid", "A booking needs a start and an end, the end after the start and at most 14 days later.");

        public static readonly Error AmountInvalid = new(
            "Resource.AmountInvalid", "A deposit or a fee is an amount from 0 to 1,000,000, to the cent.");

        public static readonly Error StatusInvalid = new(
            "Reservation.StatusInvalid", "A booking is Pending, Confirmed, CheckedIn, Completed, Cancelled or NoShow.");

        public static readonly Error DepositOptionInvalid = new(
            "Reservation.DepositInvalid", "A deposit is charged to the member's house account (charge), paid now (paid, with how), or waived (waive, with a reason).");

        public static readonly Error WaiveReasonRequired = new(
            "Reservation.WaiveReasonRequired", "Say why the deposit is waived. The reason stays on the booking.");

        public static Error TooLong(string what, int max) => new(
            "Reservation.TooLong", $"{what} can be at most {max} characters.");

        public static readonly Error Changed = new(
            "Reservation.Changed.Conflict", "Someone else changed this booking at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error ResourceChanged = new(
            "Resource.Changed.Conflict", "Someone else changed this resource at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error ResourceInactive = new(
            "Resource.Inactive.Conflict", "This resource is not taking bookings. Make it bookable again first, or pick another.");

        public static readonly Error MemberBookingInvalid = new(
            "Reservation.MemberBooking", "Members book it in the portal: off, request (staff confirm it) or instant.");

        public static Error TooBig(string resource, int capacity, int party) => new(
            "Reservation.Capacity.Conflict", $"{resource} holds {capacity}, so a party of {party} can't be booked into it.");

        /// <summary>The booking it would overlap, named, for a resource booked one at a time.</summary>
        public static Error Overlaps(string resource, string when, string who, string status) => new(
            "Reservation.Overlap.Conflict", $"{resource} is already booked {when} by {who} ({status}), so it can't take this booking.");

        /// <summary>For a shared resource: how full it already is at its busiest moment in the time asked.</summary>
        public static Error Full(string resource, int capacity, int booked, string at, int bookings, int party) => new(
            "Reservation.Capacity.Conflict", $"{resource} holds {capacity}, and {booked} {(booked == 1 ? "is" : "are")} already booked at {at} ({bookings} booking{(bookings == 1 ? "" : "s")}), so a party of {party} doesn't fit. {Math.Max(0, capacity - booked)} {(capacity - booked == 1 ? "place is" : "places are")} free then.");

        public static readonly Error DepositNeedsMember = new(
            "Reservation.DepositNeedsMember.Conflict", "This resource asks a deposit, which goes on a member's house account. Book it under the member hosting the guest, or have someone who handles money waive it.");

        public static Error CannotMove(string from, string to) => new(
            "Reservation.CannotMove.Conflict", from switch
            {
                "Cancelled" => "This booking was cancelled. Book again instead.",
                "Completed" => "This booking is completed and stays completed.",
                _ => $"A booking that is {from} can't be set to {to}.",
            });

        public static readonly Error HasMoney = new(
            "Reservation.HasMoney.Conflict", "This booking's deposit or no-show fee is on the member's account, so it can't be deleted. Cancel it instead: the deposit is given back and the record stays.");

        public static readonly Error NotStarted = new(
            "Reservation.NotStarted.Conflict", "This booking hasn't started yet, so it can't be marked a no-show.");

        public static readonly Error ResourceHasBookings = new(
            "Resource.HasBookings.Conflict", "This resource has bookings, so it can't be deleted. Make it not bookable instead: its bookings keep its name.");
    }

    /// <summary>
    /// Custom field errors.
    /// </summary>
    public static class CustomField
    {
        public static readonly Error InvalidEntityType = new(
            "CustomField.InvalidEntityType", "Custom fields can be added to leads, contacts, companies, deals and, in an organization, members");

        public static readonly Error InvalidCurrency = new(
            "CustomField.InvalidCurrency", "A money field's currency is a three-letter code such as CHF");

        public static readonly Error LinkTypeRequired = new(
            "CustomField.LinkTypeRequired", "A link field needs the kind of record it points at: a lead, contact, company, deal or member");

        // ".Conflict" is what maps this to 409: the field is there, but its values stop it changing kind.
        public static readonly Error TypeChangeConflict = new(
            "CustomField.TypeChange.Conflict", "This field already has values, so its kind can't change; add a new field instead");

        public static readonly Error ValuesConflict = new(
            "CustomField.Values.Conflict", "Someone else saved these fields at the same moment; reload the record and try again");

        public static readonly Error ReorderInvalid = new(
            "CustomField.InvalidOrder", "Send every field of that kind of record, each once");

        public static Error IsRequired(string label) => new(
            "CustomField.Required", $"{label} is required");

        public static Error LinkNotFound(string label) => new(
            "CustomField.InvalidValue", $"{label} must point at a record of this workspace");

        public static Error InvalidDefault(string label, string expected) => new(
            "CustomField.InvalidDefault", $"The default for {label} {expected}");

        public static readonly Error InvalidType = new(
            "CustomField.InvalidType", "That is not a kind of custom field");

        public static readonly Error LabelRequired = new(
            "CustomField.LabelRequired", "A custom field needs a label");

        public static readonly Error OptionsRequired = new(
            "CustomField.OptionsRequired", "A dropdown or multi select field needs at least one option");

        public static readonly Error CannotMove = new(
            "CustomField.CannotMove", "A field can't move to another kind of record; add a new one instead");

        public static readonly Error NotFound = new(
            "CustomField.NotFound", "The custom field was not found");

        public static readonly Error RecordNotFound = new(
            "CustomFieldRecord.NotFound", "The record was not found");

        // "NotOwner" in the code is what maps this to 403 Forbidden. It takes
        // MemberPermissions.ManageSettings (the owner and managers by default).
        public static readonly Error NotOwnerOrManager = new(
            "CustomField.NotOwnerOrManager", "Your role here does not let you change custom fields");

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

        public static readonly Error DealNotFound = new(
            "ContractDeal.NotFound", "The deal this contract is for was not found");

        /// <summary>A membership contract names a tier of the club, which needs the club turned on.</summary>
        public static readonly Error ClubOff = new(
            "Contract.ClubOff", "The club is turned off in this workspace, so a contract cannot admit a member or take a deposit");

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

    /// <summary>A contract's deposit (see IContractDeposits).</summary>
    public static class ContractDeposit
    {
        public static readonly Error NotAsked = new(
            "ContractDeposit.NotFound", "This contract asks for no deposit.");

        public static readonly Error NeedsTier = new(
            "ContractDeposit.NeedsTier",
            "Only a membership contract asks for a deposit, because it goes on the member's account: choose the tier it admits them at first.");

        public static readonly Error AmountInvalid = new(
            "ContractDeposit.AmountInvalid", "A deposit is an amount above zero.");

        public static readonly Error NoWayToPay = new(
            "ContractDeposit.NoWayToPay",
            "The club cannot be paid yet: set up Stripe, or the club's bank details for QR-bills, under Club settings, Payments, before asking for a deposit.");

        public static readonly Error SignFirst = new(
            "ContractDeposit.SignFirst", "The deposit is paid once the contract is signed: sign it first.");

        /// <summary>Paid already: a second payment would be the signer paying twice.</summary>
        public static readonly Error AlreadyPaid = new(
            "ContractDeposit.Paid.Conflict", "The deposit is paid.");

        public static readonly Error Closed = new(
            "ContractDeposit.Closed", "The deposit can no longer be paid: the contract was declined or withdrawn.");

        public static readonly Error NoQrBill = new(
            "ContractDeposit.NoQrBill", "The club takes no bank transfers by QR-bill. Pay online, or ask the club how to pay.");

        /// <summary>
        /// The text the signer agrees to must say what they are asked to pay: a deposit the text
        /// never mentions would be money asked for outside the agreement.
        /// </summary>
        public static Error NotInText(string money) => new(
            "ContractDeposit.NotInText",
            $"The contract text does not say the deposit of {money}. Add it, for example \"A deposit of {money} is payable on signing.\", before sending.");
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
            "Import.NotOwnerOrManager", "Only whoever ran the import, or someone who manages the workspace's settings, can undo it");

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

        // An account single sign-on made, or one whose unconfirmed password went when the
        // address's owner signed on, has no password to confirm a change with. Also 400:
        // the person is signed in.
        public static readonly Error NoPassword = new(
            "Auth.NoPassword", "Your account has no password yet: you sign in with Google, Microsoft or a passkey. Set a password first (Forgot password on the sign-in page sends you a link), then turn on two-factor sign-in.");

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
        
public static readonly Error AlreadyMember = new(
            "Organization.AlreadyMember", "This user is already a member of the organization");
        
        public static readonly Error CannotRemoveOwner = new(
            "Organization.CannotRemoveOwner", "The organization owner cannot be removed");

        public static readonly Error HandOverToLeaver = new(
            "Organization.HandOverToLeaver", "Choose someone else to take over their work: they are the one leaving.");

        public static readonly Error HandOverOutsider = new(
            "Organization.HandOverOutsider", "The person taking over their work must be a member of this organization.");

        public static readonly Error HandOverReadOnly = new(
            "Organization.HandOverReadOnly", "The person taking over their work can only read records here. Choose someone who can work them.");

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
            "Payment.MethodInvalid", "A payment is made by CardOnFile, Card, Twint, DirectDebit, Cash, Check, Wire or AppliedCredit.");

        public static Error ReceivedCurrencyMismatch(string received, string account) => new(
            "Payment.CurrencyMismatch",
            $"The money came in {received}, and the member's account is kept in {account}: record it by hand at the rate the bank used.");

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
            "Charge.CannotDelete", "A charge can't be deleted. Someone allowed to void money can void it, with a reason, and the record stays.");

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

        // ── the typed ledger ─────────────────────────────────────────────────────

        public static readonly Error AllocationsInvalid = new(
            "Payment.AllocationsInvalid", "What a payment pays each charge must be more than 0 and add up to the payment.");

        public static readonly Error ChargeOfAnotherMember = new(
            "Payment.ChargeOfAnotherMember", "A payment pays one member's charges, and one of those charges is another member's.");

        public static Error CreditTooLow(decimal available, decimal amount) => new(
            "Payment.CreditTooLow", string.Create(CultureInfo.InvariantCulture,
                $"The member has {available:0.00} of house credit, so {amount:0.00} can't be paid from it."));

        public static Error RefundTooMuch(decimal left, decimal amount) => new(
            "Payment.RefundTooMuch", string.Create(CultureInfo.InvariantCulture,
                $"{left:0.00} of this payment is left to refund, so a refund of {amount:0.00} would be more than was paid."));

        public static readonly Error NothingToRefund = new(
            "Payment.NothingToRefund.Conflict", "This payment has been refunded or voided in full already.");

        public static readonly Error ReasonRequiredForRefund = new(
            "Club.ReasonRequired", "Say why this is being refunded. The reason stays on the record.");

        public static readonly Error PeriodBilled = new(
            "Subscription.PeriodBilled.Conflict", "This period was billed already, so nothing more was charged.");

        public static readonly Error SubscriptionHasCharges = new(
            "Subscription.HasCharges.Conflict", "This subscription has billed charges, so it can't be deleted. Cancel it instead; its charges stay on the account.");

        public static readonly Error SubscriptionChanged = new(
            "Subscription.Changed.Conflict", "Someone else changed this subscription at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error NameRequired = new(
            "Subscription.NameRequired", "A subscription needs a name.");

        public static readonly Error DonationNotFound = new(
            "Donation.NotFound", "That donation was not found.");

        public static readonly Error DonationVoided = new(
            "Donation.Voided.Conflict", "This donation is already voided.");

        public static readonly Error DonationChanged = new(
            "Donation.Changed.Conflict", "Someone else changed this donation at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error DonationCannotDelete = new(
            "Donation.CannotDelete", "A donation can't be deleted. Someone allowed to void money can void it, with a reason, and the record stays.");

        public static readonly Error CauseInvalid = new(
            "Donation.CauseInvalid", "A donation needs a cause of at most 60 characters.");

        public static readonly Error GiftCardCodeRequired = new(
            "GiftCard.CodeRequired", "Type the gift card's code.");

        public static readonly Error AccountChanged = new(
            "HouseAccount.Changed.Conflict", "Something else was posted to this member's account at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error MemberHasOpenAccount = new(
            "ClubMember.OpenAccount.Conflict", "This member still has a balance, an open tab or a running subscription. Settle or close them first, or set the member Resigned or Lapsed instead, so the club's books keep saying whose they were.");

        public static readonly Error PeriodInvalid = new(
            "Club.PeriodInvalid", "A statement's period has to start before it ends and cover at most five years.");

        // ── billing ─────────────────────────────────────────────────────────────

        public static Error PeriodNotStarted(DateTime startsAtUtc) => new(
            "Subscription.NotDue.Conflict",
            string.Create(CultureInfo.InvariantCulture,
                $"This subscription's next period starts on {startsAtUtc:d MMM yyyy}, so nothing was billed. Bill it early to charge that period now."));

        public static readonly Error BilledAhead = new(
            "Subscription.BilledAhead.Conflict",
            "The next period has already been billed early, so nothing more was charged. A subscription is billed at most one period ahead.");

        public static Error TierUnknown(string tier) => new(
            "Member.TierUnknown",
            $"'{tier}' is not one of the club's membership tiers. Pick one from the price catalogue, or add it there first.");

        public static Error TierNotOnSale(string tier) => new(
            "Member.TierNotOnSale",
            $"The {tier} tier is no longer offered, so it can't be given to a member. Members who have it keep it.");

        public static readonly Error MemberNoEmail = new(
            "ClubMember.NoEmail", "This member has no valid email address on their record, so nothing was sent.");

        public static Error CurrencyMismatch(string itemCurrency, string accountCurrency) => new(
            "CatalogItem.CurrencyMismatch",
            $"This item is priced in {itemCurrency} and the member's account is kept in {accountCurrency}, so it can't be charged to it.");
    }

    /// <summary>
    /// The club's price catalogue.
    /// </summary>
    public static class Catalog
    {
        public static readonly Error NotFound = new(
            "CatalogItem.NotFound", "That catalogue item was not found.");

        public static readonly Error KindInvalid = new(
            "CatalogItem.KindInvalid", "A catalogue item is a Membership tier, Dues, a Fee, a Product, a Ticket or a MenuItem.");

        public static readonly Error KindFixed = new(
            "CatalogItem.KindFixed", "An item's kind can't be changed once it exists. Take it off sale and add one of the other kind.");

        public static readonly Error NameRequired = new(
            "CatalogItem.NameRequired", "A catalogue item needs a name.");

        public static readonly Error PriceInvalid = new(
            "CatalogItem.PriceInvalid", "A price must be a number from 0 to under 1,000,000,000, to the cent.");

        public static readonly Error VatInvalid = new(
            "CatalogItem.VatInvalid", "A VAT rate is a percent from 0 to 100, to two decimal places, such as 8.1.");

        public static readonly Error CurrencyInvalid = new(
            "CatalogItem.CurrencyInvalid", "A currency is a three-letter code, such as CHF.");

        public static readonly Error CadenceInvalid = new(
            "CatalogItem.CadenceInvalid", "A tier or dues is billed Monthly, Quarterly, SemiAnnual or Annual.");

        public static readonly Error MultiplierInvalid = new(
            "CatalogItem.MultiplierInvalid", "A loyalty multiplier is a number more than 0 and at most 100, such as 1.5.");

        public static readonly Error ChargeKindInvalid = new(
            "CatalogItem.ChargeKindInvalid", "An item is charged as Dues, Dining, Bar, Event, Spa, Booking, Retail or Other.");

        public static readonly Error BenefitsInvalid = new(
            "CatalogItem.BenefitsInvalid", "A tier has at most 20 benefits, each at most 200 characters.");

        public static readonly Error SortOrderInvalid = new(
            "CatalogItem.SortOrderInvalid", "An item's place in the list is a whole number from 0 to 10,000.");

        public static Error TierNameTaken(string name) => new(
            "CatalogItem.TierNameTaken.Conflict", $"There is already a tier named {name}. Each tier needs its own name, as members carry it.");

        public static Error NotOnSale(string name) => new(
            "CatalogItem.NotOnSale.Conflict", $"{name} is no longer on sale, so it can't be charged.");

        public static Error NotForThis(string kind, string use) => new(
            "CatalogItem.WrongKind", $"A {kind} item can't be used for {use}.");

        public static readonly Error Changed = new(
            "CatalogItem.Changed.Conflict", "Someone else changed this item at the same moment, so nothing was saved. Reload and try again.");
    }

    /// <summary>
    /// Money from outside the club: payment settings, Stripe, QR-bills, bank files and the journal.
    /// </summary>
    public static class Payments
    {
        public static Error SettingsInvalid(string sentence) => new("PaymentSettings.Invalid", sentence);

        public static readonly Error SettingsChanged = new(
            "PaymentSettings.Changed.Conflict", "Someone else changed the payment settings at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error NotSetUp = new(
            "Payments.NotSetUp", "Online payments are not set up in this workspace. Someone who manages settings adds the Stripe key under Club settings, Payments.");

        public static readonly Error KeyUnreadable = new(
            "Payments.KeyUnreadable", "The saved Stripe key can no longer be read on this server. Enter it again under Club settings, Payments.");

        public static Error ProviderRefused(string message) => new("Payments.ProviderRefused", message);

        public static Error ProviderUnreachable(string message) => new("Payments.SendFailed", message);

        public static readonly Error LinkInvalid = new(
            "PayLink.NotFound", "This payment link is not valid any more. Ask the club for a new one.");

        public static readonly Error PurposeInvalid = new(
            "PayLink.PurposeInvalid", "A payment link is for what the member owes (account), some of their charges (charges), or saving a card (setup).");

        public static readonly Error NothingToPay = new(
            "Payments.NothingToPay", "There is nothing to pay: the charges are paid, or the account is in credit.");

        public static readonly Error TooManyCharges = new(
            "Payments.TooManyCharges", "Choose at most 50 charges at a time, or none for everything the member owes.");

        public static readonly Error CurrencyNotOffered = new(
            "Payments.CurrencyNotOffered", "The member's account is in a currency online payments do not take.");

        public static readonly Error NoSavedCard = new(
            "Payments.NoSavedCard", "This member has no saved card. Send them a link to save one first.");

        public static readonly Error AgreementRequired = new(
            "Payments.AgreementRequired", "Charging dues to a saved card needs the member's agreement. Confirm that they agreed.");

        public static readonly Error ProfileChanged = new(
            "PaymentProfile.Changed.Conflict", "Someone else changed this member's saved card at the same moment, so nothing was saved. Reload and try again.");

        public static readonly Error ChargeInProgress = new(
            "Payments.Charge.Conflict", "A charge to this card is already under way for these charges. Wait for it to finish.");

        public static readonly Error NotAProviderPayment = new(
            "Payments.NotAProviderPayment", "This payment was not taken online, so it is refunded by hand.");

        public static readonly Error ReferenceNotFound = new(
            "PaymentReference.NotFound", "No QR-bill of this workspace carries that reference.");

        public static Error BankFileInvalid(string sentence) => new("BankFile.Invalid", sentence);

        public static readonly Error BankTransactionNotFound = new(
            "BankTransaction.NotFound", "That bank transfer was not found.");

        public static readonly Error BankTransactionResolved = new(
            "BankTransaction.Resolved.Conflict", "This transfer is already recorded as a member's payment.");

        public static readonly Error BankTransactionChanged = new(
            "BankTransaction.Changed.Conflict", "Someone else dealt with this transfer at the same moment. Reload to see what they did.");

        public static readonly Error IgnoreReasonRequired = new(
            "BankTransaction.ReasonRequired", "Say why this transfer is not a member's payment.");

        public static readonly Error JournalPeriodInvalid = new(
            "Journal.PeriodInvalid", "The journal's period must start before it ends and cover at most a year.");

        public static readonly Error JournalLayoutInvalid = new(
            "Journal.LayoutInvalid", "The journal's layout is gross (one booking per entry, with its VAT code) or net (VAT booked on its own line).");
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
    /// Single sign-on with Google and Microsoft, and a workspace's requirement of it.
    /// </summary>
    public static class Sso
    {
        public const string RequiredCode = "Sso.Required";

        public static readonly Error NotOffered = new(
            "Sso.NotOffered", "Signing in with that provider is not set up on this server.");

        public static readonly Error BadChallenge = new(
            "Sso.BadChallenge", "The sign-in could not start: the app sent no valid challenge. Reload the page and try again.");

        public static readonly Error ProviderUnavailable = new(
            "Sso.ProviderUnavailable", "The sign-in provider did not answer. Try again in a moment.");

        public static readonly Error HandoffExpired = new(
            "Sso.HandoffExpired", "This sign-in has expired or was already used. Sign in again.");

        public static readonly Error HandoffNotYours = new(
            "Sso.HandoffNotYours", "This sign-in was started in another browser or tab. Start it again here.");

        public static readonly Error LinkNotFound = new(
            "Sso.LinkNotFound", "That sign-in is not linked to your account.");

        public static readonly Error LastWayIn = new(
            "Sso.LastWayIn", "This is the only way you sign in. Add a passkey or set a password (Forgot password sends a link) before you unlink it.");

        public static readonly Error NoProvider = new(
            "Sso.NoProvider", "No single sign-on provider is set up on this server, so nobody could meet the requirement. Ask whoever runs Cadence to set up Google or Microsoft sign-in first.");

        public static Error InvalidDomain(string domain) => new(
            "Sso.InvalidDomain", $"\"{domain}\" is not an email domain. Write domains like pavillon46.ch, without the @.");

        public static readonly Error TooManyDomains = new(
            "Sso.TooManyDomains", "List at most 20 email domains.");

        public static Error Required(string workspace) => new(
            RequiredCode, $"{workspace} asks its team to sign in with Google or Microsoft. Sign in that way to open it.");
    }

    /// <summary>
    /// Passkeys (WebAuthn): adding them and signing in with them.
    /// </summary>
    public static class Passkeys
    {
        public static readonly Error NotConfigured = new(
            "Passkeys.NotConfigured", "Passkeys are not set up on this server.");

        public static readonly Error CeremonyExpired = new(
            "Passkeys.CeremonyExpired", "That passkey prompt has expired. Try again.");

        public static readonly Error NotRecognised = new(
            "Passkeys.NotRecognised", "That passkey was not recognised. Use another, or sign in with your password.");

        public static readonly Error CounterWentBack = new(
            "Passkeys.CounterWentBack", "That passkey was refused: its counter went backwards, which happens when a security key has been copied. Sign in another way and remove it in Settings.");

        public static readonly Error RegistrationFailed = new(
            "Passkeys.RegistrationFailed", "The passkey could not be added. Try again, or use another device.");

        public static readonly Error AlreadyAdded = new(
            "Passkeys.AlreadyAdded", "That passkey is already added to an account.");

        public static readonly Error RecentSignInRequired = new(
            "Passkeys.RecentSignInRequired", "For your security, sign in again before you add a passkey: your account has no password to confirm it with.");

        public static readonly Error PasswordRequired = new(
            "Passkeys.PasswordRequired", "Enter your password to add a passkey.");

        public static readonly Error NotFound = new(
            "Passkeys.NotFound", "That passkey is not one of yours.");

        public static readonly Error TooMany = new(
            "Passkeys.TooMany", "You have 20 passkeys already. Remove one you no longer use first.");

        public static readonly Error NoneForAccount = new(
            "Passkeys.NoneForAccount", "You have not added a passkey yet. Enter the code from your authenticator app.");
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
