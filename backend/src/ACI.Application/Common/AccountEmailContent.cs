using System.Globalization;
using ACI.Domain.Common;

namespace ACI.Application.Common;

/// <summary>
/// The wording of CadenceE's own account emails: the password reset, the address
/// confirmation, the invitation to a workspace and the task reminder, each as both parts in
/// CadenceE's design (<see cref="EmailLook.Product"/>).
/// </summary>
/// <remarks>
/// Pure functions of their arguments, as <see cref="ContractEmailContent"/> is, so what a
/// person reads is testable without a mail server; <c>SmtpEmailSender</c> keeps only the
/// decision to send. How long each link works is said here and must match the code that
/// issues it: an hour for a reset (AuthService), a day for a confirmation, seven days for an
/// invitation (InviteService). The tests hold those numbers.
/// </remarks>
public static class AccountEmailContent
{
    /// <summary>How long a password reset link works (AuthService.RequestPasswordResetAsync).</summary>
    public static readonly TimeSpan ResetLinkLifetime = TimeSpan.FromHours(1);

    /// <summary>How long an address confirmation link works (AuthService.IssueVerificationToken).</summary>
    public static readonly TimeSpan VerificationLinkLifetime = TimeSpan.FromHours(24);

    /// <summary>How long an invitation works, from when it is sent or sent again (InviteService).</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private static string Hi(string? name) => string.IsNullOrWhiteSpace(name) ? "Hi," : $"Hi {name.Trim()},";

    /// <summary>The "forgot password" email.</summary>
    public static EmailContent PasswordReset(string toEmail, string? recipientName, string resetUrl)
    {
        const string subject = "Reset your CadenceE password";
        var design = new EmailDesign(EmailLook.Product)
            {
                Language = "en",
                Eyebrow = "Password reset",
                Title = "Choose a new password",
                Preheader = "Someone asked to reset your CadenceE password. The link works for one hour.",
                FooterNote = $"Sent to {toEmail.Trim()} because a password reset was asked for this address.",
            }
            .Paragraph(Hi(recipientName))
            .Paragraph($"We received a request to reset the password of your CadenceE account, {toEmail.Trim()}. " +
                       "You can choose a new one below.")
            .Button("Choose a new password", resetUrl)
            .Note("This link expires in one hour and works once. If you ask again, only the newest link works. " +
                  "Choosing a new password signs you out on every device.")
            .Paragraph("If you did not ask for this, you can ignore this email: your password stays as it is.", muted: true);
        return EmailLayout.Compose(design, subject);
    }

    /// <summary>The email that asks a new account to confirm its address.</summary>
    public static EmailContent EmailVerification(string toEmail, string? recipientName, string verifyUrl)
    {
        const string subject = "Confirm your email address for CadenceE";
        var design = new EmailDesign(EmailLook.Product)
            {
                Language = "en",
                Eyebrow = "Confirm your email",
                Title = "Is this your email address?",
                Preheader = "Confirm your address to finish setting up CadenceE. The link works for 24 hours.",
                FooterNote = $"Sent to {toEmail.Trim()} because a CadenceE account was created with this address.",
            }
            .Paragraph(Hi(recipientName))
            .Paragraph($"Please confirm that {toEmail.Trim()} is your email address. It takes one click.")
            .Button("Confirm my email address", verifyUrl)
            .Note("This link expires in 24 hours. Until you confirm your address, you can use your own workspace " +
                  "but cannot join anyone else's.")
            .Paragraph("If you did not create a CadenceE account, you can ignore this email.", muted: true);
        return EmailLayout.Compose(design, subject);
    }

    /// <summary>The invitation to join a workspace.</summary>
    /// <param name="toEmail">The address it was issued to: the one they must sign in with.</param>
    /// <param name="invitedByName">Who sent it; empty reads "Someone".</param>
    /// <param name="permissionPreset">
    /// The preset they join with (<see cref="MemberPermissions.Presets"/>); none (or one not
    /// known) joins them as a Member, as before presets.
    /// </param>
    public static EmailContent WorkspaceInvitation(
        string toEmail, string organizationName, string? invitedByName, string acceptUrl, string? permissionPreset = null)
    {
        var workspace = organizationName.Trim();
        var from = string.IsNullOrWhiteSpace(invitedByName) ? null : invitedByName.Trim();
        var (role, what) = Role(permissionPreset);
        var subject = $"You have been invited to {workspace} on CadenceE";
        var design = new EmailDesign(EmailLook.Product)
            {
                Language = "en",
                Eyebrow = "Invitation",
                Title = $"Join {workspace} on CadenceE",
                Preheader = $"{from ?? "Someone"} invited you to join {workspace}. You'll join as {role}.",
                FooterNote = $"Sent to {toEmail.Trim()} because {from ?? "someone"} invited this address to {workspace}.",
            }
            .Paragraph("Hi,")
            .Paragraph($"{from ?? "Someone"} invited you to join {workspace} on CadenceE.")
            .Facts([("Workspace", workspace), ("Invited by", from), ("You'll join as", role)])
            .Paragraph($"{role} \u2014 {char.ToLowerInvariant(what[0])}{what[1..]}", muted: true)
            .Button("Accept the invitation", acceptUrl)
            .Paragraph($"Sign in, or create your account, with this email address ({toEmail.Trim()}), " +
                       "and confirm the address if CadenceE asks you to.")
            .Note("This invitation expires in 7 days. The link works only for you, so do not forward this email.")
            .Paragraph("If you were not expecting it, you can ignore this email.", muted: true);
        return EmailLayout.Compose(design, subject);
    }

    /// <summary>What someone joining with a preset will be able to do, in its own words.</summary>
    private static (string Role, string What) Role(string? presetKey) =>
        MemberPermissions.FindPreset(presetKey) is { } preset && preset != MemberPermissions.Owner
            ? (preset.Label, preset.Description)
            // What a Member without a preset could always do (MemberPermissions.LegacyMember).
            : ("Member", "Everything but the workspace's settings, the team, voids and other people's private deals.");

    /// <summary>A reminder about a task whose reminder time has come.</summary>
    public static EmailContent TaskReminder(string? recipientName, string taskTitle, DateTime? dueDateUtc, string taskUrl)
    {
        var title = string.IsNullOrWhiteSpace(taskTitle) ? "Your task" : taskTitle.Trim();
        var due = dueDateUtc?.ToString("dddd d MMMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var design = new EmailDesign(EmailLook.Product)
            {
                Language = "en",
                Eyebrow = "Task reminder",
                Title = title,
                Preheader = due == null ? "A reminder about your task." : $"A reminder about your task, due {due}.",
                FooterNote = "You are receiving this because task reminders are on in your notification settings.",
            }
            .Paragraph($"{Hi(recipientName)} this is a reminder about your task.")
            .Facts([("Due", due)])
            .Button("Open the task", taskUrl);
        return EmailLayout.Compose(design, $"Reminder: {taskTitle}");
    }
}
