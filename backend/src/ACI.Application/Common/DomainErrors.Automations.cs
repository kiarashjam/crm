namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>Workspace automations: their rules and their runs.</summary>
    public static class Automation
    {
        public static readonly Error NotFound = new("Automation.NotFound", "That rule was not found.");

        public static readonly Error Changed = new(
            "Automation.Changed.Conflict",
            "Someone else changed this rule since you opened it. Reload it and make your change again.");

        public static Error Invalid(string why) => new("Automation.Invalid", why);

        public static readonly Error RunNotFound = new("AutomationRun.NotFound", "That run was not found.");

        public static readonly Error RunNotFailed = new(
            "AutomationRun.NotFailed.Conflict", "Only a run that failed can be tried again.");

        public static readonly Error RunFinished = new(
            "AutomationRun.Finished.Conflict", "That run has already finished, so there is nothing to stop.");

        public static readonly Error RunBusy = new(
            "AutomationRun.Busy.Conflict", "That run is doing a step right now. Try again in a moment.");
    }
}
