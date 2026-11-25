namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>The workspace's recap emails.</summary>
    public static class Recaps
    {
        /// <summary>The person named is not in the workspace (404).</summary>
        public static readonly Error PersonNotFound = new(
            "Recap.NotFound", "That person is not on this workspace's team.");

        /// <summary>Switching a recap on for someone who does not see every deal (400).</summary>
        public static Error NotEligible(string? name) => new(
            "Recap.NotEligible",
            $"Recap emails are the whole team's figures, so they go only to people who see all deals. "
            + $"Give {(string.IsNullOrWhiteSpace(name) ? "them" : name.Trim())} a preset with \"See all deals\" on the Team page (Read-only, say), then switch it on.");

        /// <summary>Switching their own recap on for someone who does not see every deal (400).</summary>
        public static readonly Error NotEligibleYourself = new(
            "Recap.NotEligible",
            "Recap emails are the whole team's figures, so they go only to people who see all deals, which your role here does not. Ask whoever manages the team.");
    }
}
