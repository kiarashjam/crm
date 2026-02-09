namespace ACI.Application.Common;

/// <summary>
/// Centralized domain error definitions for all entities.
/// </summary>
public static class DomainErrors
{
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
    }

    /// <summary>
    /// Deal-related errors.
    /// </summary>
    public static class Deal
    {
        public static readonly Error NotFound = new(
            "Deal.NotFound", "The deal was not found");
        
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
            "Auth.AccountLocked", "The account has been locked due to too many failed attempts");
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
