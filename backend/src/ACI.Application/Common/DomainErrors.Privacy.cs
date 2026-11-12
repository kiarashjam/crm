namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>Exporting and erasing a person, and the workspace's privacy settings.</summary>
    public static class Privacy
    {
        public static readonly Error NothingAsked = new(
            "Privacy.NothingAsked", "Give an email address, or start from a lead, a contact or a member.");

        public static readonly Error NothingChosen = new(
            "Privacy.NothingChosen", "Choose at least one record or address that belongs to this person.");

        public static readonly Error EmailInvalid = new("Privacy.EmailInvalid", "That email address does not look right.");

        /// <summary>A record named in the request is not in this workspace (or no longer exists).</summary>
        public static readonly Error RecordNotFound = new(
            "Privacy.Record.NotFound", "One of the records chosen was not found in this workspace. Look the person up again.");

        public static readonly Error ErasureConflict = new(
            "Privacy.Erasure.Conflict",
            "One of the person's records changed while they were being erased, so nothing was erased. Look them up again and try once more.");

        public static readonly Error ConfirmationMissing = new(
            "Privacy.ConfirmationMissing", "Type ERASE to confirm. An erasure cannot be undone.");

        public static readonly Error ReferenceHasPersonalData = new(
            "Privacy.ReferenceHasPersonalData",
            "The reference is kept in the log of erasures, which names nobody: use a ticket number or a date, not the person's name or address.");

        public static readonly Error TooManyRecords = new(
            "Privacy.TooManyRecords", "That is more records than one person has. Choose only the ones that are this person's.");

        public static Error TooLong(string what, int max) => new("Privacy.TooLong", $"{what} can be at most {max} characters.");

        public static readonly Error ConsentModeInvalid = new(
            "Privacy.ConsentModeInvalid", "The marketing checkbox is off, optional or required.");

        public static readonly Error RetentionOutOfRange = new(
            "Privacy.RetentionOutOfRange",
            "A retention period is a whole number: leads 1 to 120 months, members 1 to 30 years, emails and details 1 to 120 months. Leave it empty to keep the data.");

        public static readonly Error ControllerEmailInvalid = new(
            "Privacy.ControllerEmailInvalid", "The privacy contact's email address does not look right.");

        public static readonly Error Changed = new(
            "Privacy.Changed.Conflict", "Someone else changed the privacy settings since you opened them. Reload them and make your change again.");
    }
}
