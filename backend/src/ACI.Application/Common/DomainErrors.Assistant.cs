using System.Globalization;

namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>The assistant and what calls to the language model may cost.</summary>
    public static class Ai
    {
        /// <summary>The month's allowance of calls to the model is used up (429, ResultExtensions).</summary>
        public const string MonthlyLimitCode = "Ai.MonthlyLimit";

        /// <summary>The workspace made too many calls in the last minutes (429, ResultExtensions).</summary>
        public const string BusyCode = "Ai.Busy";

        /// <param name="limit">The month's allowance.</param>
        /// <param name="resetsOn">The first day of next month, when it starts again.</param>
        /// <param name="personal">Outside any organization: the allowance is the person's own.</param>
        public static Error MonthlyLimit(int limit, DateOnly resetsOn, bool personal) => new(
            MonthlyLimitCode,
            limit <= 0
                ? (personal
                    ? "The AI is not available outside an organization on this server."
                    : "The AI is turned off in this workspace (its monthly limit is 0). A manager can change that in Settings › Assistant.")
                : (personal
                    ? $"You have used your {limit} AI calls for this month. They start again on {Day(resetsOn)}."
                    : $"This workspace has used its {limit} AI calls for this month. They start again on {Day(resetsOn)}. A manager can raise the limit in Settings › Assistant."));

        public static readonly Error Busy = new(
            BusyCode, "Your team is asking the AI a lot right now. Wait a minute, then try again.");

        private static string Day(DateOnly day) => day.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>The assistant's chat and settings.</summary>
    public static class Assistant
    {
        /// <summary>No language model is set up on the server (503: ".NotConfigured").</summary>
        public static readonly Error NotConfigured = new(
            "Assistant.NotConfigured", "No language model is set up on this server, so the assistant answers from its built-in rules.");

        /// <summary>The model was asked but did not answer properly (502, ResultExtensions).</summary>
        public static Error ModelFailed(string why) => new("Assistant.ModelFailed", why);

        public static readonly Error QuestionRequired = new("Assistant.QuestionRequired", "Ask a question first.");

        public static readonly Error QuestionTooLong = new("Assistant.QuestionTooLong", "That question is too long. Keep it under 2,000 characters.");

        public static Error LimitInvalid(int max) => new(
            "Assistant.LimitInvalid", $"The monthly limit must be a whole number from 0 to {max.ToString("N0", CultureInfo.InvariantCulture)}, the most this server allows.");

        /// <summary>Someone saved the settings after the version this edit was made from (409).</summary>
        public static readonly Error SettingsChanged = new(
            "AssistantSettings.Changed.Conflict", "Someone changed the assistant's settings since you opened them. Reload to see theirs, then make your change again.");
    }
}
