namespace ACI.Application.Common;

/// <summary>
/// When an open deal counts as stale: nothing has been logged on it, and it has not
/// moved stage, for a number of days. The workspace sets the number; a pipeline whose
/// deals move at another pace (club memberships wait weeks for a committee) can set its
/// own. A deal created that long ago with nothing since is stale too.
/// </summary>
public static class StaleDeals
{
    /// <summary>A workspace's setting until a manager changes it, and a personal workspace's.</summary>
    public const int DefaultDays = 30;

    public const int MaxDays = 365;

    /// <summary>Whether a person may store this: 0, which never flags a deal, or 1 to 365 days.</summary>
    public static bool IsValidSetting(int days) => days is >= 0 and <= MaxDays;

    /// <summary>The days that apply to a pipeline's deals: its own when it has them, else the workspace's. 0 is never.</summary>
    public static int DaysFor(int? pipelineDays, int workspaceDays) => pipelineDays ?? workspaceDays;
}
