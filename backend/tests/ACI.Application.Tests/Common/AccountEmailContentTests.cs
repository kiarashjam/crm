using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// CadenceE's own account emails (password reset, address confirmation, workspace
/// invitation, task reminder): the link in both parts and written out under the button, how
/// long it works said truthfully, the "not you?" reassurance kept, and nothing a person typed
/// delivered as markup.
/// </summary>
public class AccountEmailContentTests
{
    private const string ResetUrl = "https://crm.example.ch/reset-password?token=r3s3t";
    private const string VerifyUrl = "https://crm.example.ch/verify-email?token=v3r1fy";
    private const string AcceptUrl = "https://crm.example.ch/invites/accept?token=1nv1t3";
    private const string TaskUrl = "https://crm.example.ch/tasks/7d1c";

    /// <summary>Says <paramref name="words"/> in the text part, and (escaped, as the design writes it) in the HTML part.</summary>
    private static void SaysInBoth(EmailContent e, params string[] words)
    {
        foreach (var w in words)
        {
            e.Text.Should().Contain(w);
            e.Html.Should().Contain(EmailLayout.E(w));
        }
    }

    private static IEnumerable<EmailContent> All() =>
    [
        AccountEmailContent.PasswordReset("anna.muller@pavillon46.ch", "Anna Müller", ResetUrl),
        AccountEmailContent.EmailVerification("anna.muller@pavillon46.ch", "Anna Müller", VerifyUrl),
        AccountEmailContent.WorkspaceInvitation("luc.favre@pavillon46.ch", "Pavillon 46", "Miguel van Ackere", AcceptUrl, "read_only"),
        AccountEmailContent.TaskReminder("Anna Müller", "Call Ada Rossi about the renewal", new DateTime(2026, 11, 27, 14, 0, 0, DateTimeKind.Utc), TaskUrl),
    ];

    [Fact]
    public void EachIsInCadenceEsDesign_WithBothParts()
    {
        foreach (var e in All())
        {
            e.Html.Should().StartWith("<!DOCTYPE html>").And.Contain("<html lang=\"en\"").And.Contain("&#10022;").And.Contain("cadencee.ai")
                .And.Contain($"{EmailLayout.E(e.Subject)}</title>");
            e.Text.Should().NotContain("<").And.NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ThePasswordReset_HasItsLinkInBothParts_ItsHourAndThe_NotYou_Reassurance()
    {
        var e = AccountEmailContent.PasswordReset("anna.muller@pavillon46.ch", "Anna Müller", ResetUrl);

        e.Subject.Should().Be("Reset your CadenceE password");
        e.Html.Should().Contain($"href=\"{ResetUrl}\"").And.Contain(">Choose a new password<").And.Contain("copy this address into your browser");
        SaysInBoth(e, "Hi Anna Müller,", ResetUrl, "anna.muller@pavillon46.ch",
            "This link expires in one hour and works once",
            "you are signed out on every device",
            "If you did not ask for this, you can ignore this email: your password stays as it is.");
        // What the email says is what AuthService issues.
        AccountEmailContent.ResetLinkLifetime.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void TheConfirmation_HasItsLinkIts24HoursAndWhatWaitsOnIt()
    {
        var e = AccountEmailContent.EmailVerification("anna.muller@pavillon46.ch", "Anna Müller", VerifyUrl);

        e.Subject.Should().Be("Confirm your email address for CadenceE");
        e.Html.Should().Contain($"href=\"{VerifyUrl}\"");
        SaysInBoth(e, VerifyUrl, "This link expires in 24 hours", "you can use your own workspace but cannot join anyone else's.",
            "If you did not create a CadenceE account, you can ignore this email.");
        AccountEmailContent.VerificationLinkLifetime.Should().Be(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData("read_only", "Read-only", "Reads everything, every deal included. Changes nothing and exports nothing.")]
    [InlineData("sales_rep", "Sales rep", "Works leads, deals, contacts and tasks.")]
    [InlineData(null, "Member", "Everything but the workspace's settings, the team, voids and other people's private deals.")]
    [InlineData("owner", "Member", "Everything but the workspace's settings")]
    [InlineData("no_such_preset", "Member", "Everything but the workspace's settings")]
    public void TheInvitation_SaysWhoInvitedThem_AndWhatTheyWillBeAbleToDo(string? preset, string role, string what)
    {
        var e = AccountEmailContent.WorkspaceInvitation("luc.favre@pavillon46.ch", "Pavillon 46", "Miguel van Ackere", AcceptUrl, preset);

        e.Subject.Should().Be("You have been invited to Pavillon 46 on CadenceE");
        e.Text.Should().Contain("Miguel van Ackere invited you to join Pavillon 46 on CadenceE.")
            .And.Contain($"You'll join as: {role}").And.Contain($"As {role}: ").And.Contain(what)
            .And.Contain(AcceptUrl)
            .And.Contain("(luc.favre@pavillon46.ch)")
            .And.Contain("This invitation expires in 7 days. The link works only for you, so do not forward this email.")
            .And.Contain("If you were not expecting it, you can ignore this email.");
        e.Html.Should().Contain($"href=\"{AcceptUrl}\"").And.Contain(">Accept the invitation<");
        SaysInBoth(e, "Miguel van Ackere", "Pavillon 46", role, what, "This invitation expires in 7 days.", "luc.favre@pavillon46.ch");
        AccountEmailContent.InvitationLifetime.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void AnInvitationFromNobodyInParticular_ReadsSomeone()
    {
        var e = AccountEmailContent.WorkspaceInvitation("luc.favre@pavillon46.ch", "Pavillon 46", "  ", AcceptUrl);

        e.Text.Should().Contain("Someone invited you to join Pavillon 46").And.NotContain("Invited by");
    }

    [Fact]
    public void TheTaskReminder_HasTheTaskItsDueTimeAndItsLink()
    {
        var e = AccountEmailContent.TaskReminder("Anna Müller", "Call Ada Rossi about the renewal", new DateTime(2026, 11, 27, 14, 0, 0, DateTimeKind.Utc), TaskUrl);

        e.Subject.Should().Be("Reminder: Call Ada Rossi about the renewal");
        e.Text.Should().StartWith("Call Ada Rossi about the renewal\n")
            .And.Contain("Hi Anna Müller, this is a reminder about your task.")
            .And.Contain("Due: Friday 27 November 2026 at 14:00 UTC")
            .And.Contain($"Open the task:\n{TaskUrl}")
            .And.Contain("task reminders are on in your notification settings");
        e.Html.Should().Contain($"href=\"{TaskUrl}\"");
        SaysInBoth(e, "Call Ada Rossi about the renewal", "Friday 27 November 2026 at 14:00 UTC", TaskUrl);

        AccountEmailContent.TaskReminder("", "Send the contract", null, TaskUrl).Text.Should().NotContain("Due").And.Contain("Hi, this is a reminder");
    }

    [Fact]
    public void WhatAPersonTypedIsEscaped_NeverMarkup()
    {
        const string nasty = "<script>alert('x')</script> & \"Co\"";
        var emails = new[]
        {
            AccountEmailContent.PasswordReset("a@b.ch", nasty, ResetUrl),
            AccountEmailContent.EmailVerification("a@b.ch", nasty, VerifyUrl),
            AccountEmailContent.WorkspaceInvitation("a@b.ch", nasty, nasty, AcceptUrl),
            AccountEmailContent.TaskReminder(nasty, nasty, null, TaskUrl),
        };
        foreach (var e in emails)
        {
            e.Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;").And.Contain("&amp;");
        }
    }
}
