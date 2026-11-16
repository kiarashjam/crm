using ACI.Domain.Common;

namespace ACI.Application.Common;

public static partial class DomainErrors
{
    /// <summary>The Sales and Club modules a workspace can turn off (see <see cref="WorkspaceModules"/>).</summary>
    public static class Modules
    {
        /// <summary>
        /// The one answer every endpoint of a module that is turned off gives, whichever it
        /// is and whoever asks: 403, with this code and the module named (InModuleAttribute).
        /// </summary>
        public const string OffCode = "Module.Off";

        public static Error Off(string module) => new(OffCode, WorkspaceModules.Refusal(module));

        public static readonly Error OneStaysOn = new(
            "Module.OneStaysOn", "Sales or Club stays on: a workspace with neither has nothing to work in.");

        public static readonly Error Unknown = new(
            "Module.Unknown", "Say which module to turn on or off: sales or club.");

        /// <summary>
        /// A module is not turned off while its automatic work is still switched on: once its
        /// pages were gone, nobody could see that work or stop it.
        /// </summary>
        /// <param name="module">The module someone tried to turn off.</param>
        /// <param name="running">What is still switched on, each as a short phrase.</param>
        public static Error StillRunning(string module, IReadOnlyList<string> running) => new(
            "Module.StillRunning.Conflict",
            $"The {WorkspaceModules.Label(module)} module still has work running on its own: {Join(running)}. " +
            "Stop that first, then turn the module off.");

        private static string Join(IReadOnlyList<string> parts) => parts.Count switch
        {
            0 => "",
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }
}
