namespace ACI.Domain.Common;

/// <summary>
/// The two halves of Cadence a workspace can use, each of which can be turned off: Sales
/// (leads, deals and pipelines, outreach, contracts and the sales reports) and Club
/// (members, households and applications, the house accounts and payments, events and
/// bookings, campaigns and the club's reports).
/// </summary>
/// <remarks>
/// <para>
/// A sales team saw twenty club pages it never uses, and a club saw all the sales tooling.
/// A module that is off leaves the menu, its pages say it is turned off, and its endpoints
/// refuse with one answer (Module.Off). Nothing is deleted: turning it back on brings
/// everything back as it was.
/// </para>
/// <para>
/// Contacts and companies, tasks, activities, the calendar, the team, the audit log,
/// automations and settings belong to both and are always there. Both modules are on for
/// every workspace until someone who manages its settings turns one off.
/// </para>
/// </remarks>
public static class WorkspaceModules
{
    public const string Sales = "sales";
    public const string Club = "club";

    public static readonly IReadOnlyList<string> All = [Sales, Club];

    public static bool IsModule(string? module) => module is Sales or Club;

    /// <summary>How the app names the module: "Sales" or "Club".</summary>
    public static string Label(string module) => module == Club ? "Club" : "Sales";

    /// <summary>The sentence a refused request says.</summary>
    public static string Refusal(string module) =>
        $"The {Label(module)} module is turned off in this workspace. Someone who manages its settings can turn it on again in Settings.";
}
