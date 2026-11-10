using ACI.Domain.Enums;

namespace ACI.Domain.Common;

/// <summary>A permission a member of an organization can hold: its key, and how the Team page names it.</summary>
public sealed record PermissionInfo(string Key, string Label, string Description);

/// <summary>A named set of permissions to start a member from: what a role in a club or a sales team needs.</summary>
public sealed record PermissionPreset(string Key, string Label, string Description, OrgMemberRole Role, IReadOnlyList<string> Permissions);

/// <summary>
/// What a member of an organization may do, beyond reading its records: permission sets,
/// started from presets and adjustable per member by the owner.
/// </summary>
/// <remarks>
/// <para>
/// There were four fixed roles, and Members could delete any lead, deal, charge or gift
/// card. A club needs more than that: a front desk that checks members in but does not
/// touch the books, finance that takes payments but does not edit the pipeline, an
/// accountant who reads and exports and changes nothing.
/// </para>
/// <para>
/// A member's permissions are their own list when the owner set one, else their preset's,
/// else (a member from before permission sets) what their old role allowed. Nobody loses
/// access on the way: an old Member keeps everything a Member could do, shown as a
/// customised Sales rep, so the owner sees it and can tighten it.
/// </para>
/// <para>
/// The coarse <see cref="OrgMemberRole"/> stays beside the permissions, set from the
/// preset, because older clients and a few checks still read it; the owner is the one
/// member whose permissions cannot be narrowed.
/// </para>
/// </remarks>
public static class MemberPermissions
{
    public const string EditRecords = "records.edit";
    public const string DeleteRecords = "records.delete";
    public const string ExportRecords = "records.export";
    public const string SeeAllDeals = "deals.all";
    public const string ManageSettings = "settings.manage";
    public const string ManageMembers = "members.manage";
    public const string ManageMoney = "money.manage";
    public const string VoidMoney = "money.void";
    public const string ClubMembers = "club.members";
    public const string FrontDesk = "club.frontdesk";

    /// <summary>Every permission, in the order the Team page lists them.</summary>
    public static readonly IReadOnlyList<PermissionInfo> All =
    [
        new(EditRecords, "Create and edit records", "Leads, deals, contacts, companies, tasks, notes, files and emails to them."),
        new(DeleteRecords, "Delete records", "Delete or merge leads, deals, contacts and companies, and remove club records."),
        new(ExportRecords, "Export lists", "Download lists, activity history and reports as spreadsheets."),
        new(SeeAllDeals, "See all deals", "Every deal, private ones too, and the team's saved forecasts."),
        new(ManageSettings, "Manage settings", "Pipelines, statuses, lead routing, automations, custom fields, email sending, integrations and club settings."),
        new(ManageMembers, "Manage the team", "Invite, add and remove members, change their roles, and run sales teams."),
        new(ManageMoney, "Take payments and charge accounts", "Charges, payments, subscriptions, gift cards and donations."),
        new(VoidMoney, "Void money", "Void charges, payments and gift cards, with a reason."),
        new(ClubMembers, "Manage club members", "Members, households, applications, events, campaigns, communications and loyalty points."),
        new(FrontDesk, "Run the front desk", "Check members in, take reservations, run tabs and redeem gift cards."),
    ];

    /// <summary>The permissions that change something. A member with none of them can only read (and export).</summary>
    public static readonly IReadOnlySet<string> Writes = new HashSet<string>(StringComparer.Ordinal)
    {
        EditRecords, DeleteRecords, ManageSettings, ManageMembers, ManageMoney, VoidMoney, ClubMembers, FrontDesk,
    };

    private static readonly string[] Everything = All.Select(p => p.Key).ToArray();

    public static readonly PermissionPreset Owner = new("owner", "Owner",
        "Everything, and the only one who can change what each member may do.", OrgMemberRole.Owner, Everything);
    public static readonly PermissionPreset Manager = new("manager", "Manager",
        "Everything: the whole pipeline, settings, the team and the club's money.", OrgMemberRole.Manager, Everything);
    public static readonly PermissionPreset SalesRep = new("sales_rep", "Sales rep",
        "Works leads, deals, contacts and tasks. Cannot delete, export, or touch settings or money.", OrgMemberRole.Member,
        [EditRecords]);
    public static readonly PermissionPreset MembershipDirector = new("membership_director", "Membership director",
        "Runs membership: members, applications, households, events and the front desk, and works the sales records.", OrgMemberRole.Member,
        [EditRecords, ExportRecords, ClubMembers, FrontDesk]);
    public static readonly PermissionPreset Finance = new("finance", "Finance",
        "Takes payments, bills subscriptions, voids with a reason, and exports. Reads the rest.", OrgMemberRole.Member,
        [ManageMoney, VoidMoney, ExportRecords]);
    public static readonly PermissionPreset FrontDeskPreset = new("front_desk", "Front desk",
        "Checks members in, takes reservations, runs tabs and redeems gift cards. Reads the rest.", OrgMemberRole.Member,
        [FrontDesk]);
    public static readonly PermissionPreset ReadOnlyAccountant = new("read_only_accountant", "Read-only accountant",
        "Reads everything and exports lists. Changes nothing.", OrgMemberRole.Viewer,
        [ExportRecords]);

    /// <summary>The presets, in the order the Team page offers them.</summary>
    public static readonly IReadOnlyList<PermissionPreset> Presets =
        [Owner, Manager, SalesRep, MembershipDirector, Finance, FrontDeskPreset, ReadOnlyAccountant];

    /// <summary>
    /// What a Member could do before permission sets: everything but the settings, the
    /// team, voids and other people's private deals. A Member without a preset keeps it.
    /// </summary>
    public static readonly IReadOnlyList<string> LegacyMember =
        [EditRecords, DeleteRecords, ExportRecords, ManageMoney, ClubMembers, FrontDesk];

    /// <summary>What a permission lets someone do, to finish "Your role here does not let you …".</summary>
    public static string Phrase(string permission) => permission switch
    {
        EditRecords => "create or edit records",
        DeleteRecords => "delete records",
        ExportRecords => "export lists",
        SeeAllDeals => "see the whole team's deals",
        ManageSettings => "change this workspace's settings",
        ManageMembers => "manage the team",
        ManageMoney => "take payments or charge accounts",
        VoidMoney => "void money",
        ClubMembers => "manage club members",
        FrontDesk => "run the front desk",
        _ => "do that",
    };

    /// <summary>The sentence a refusal says: "Your role here does not let you …".</summary>
    public static string Refusal(string what) => $"Your role here does not let you {what}.";

    public static PermissionPreset? FindPreset(string? key) =>
        Presets.FirstOrDefault(p => string.Equals(p.Key, key?.Trim(), StringComparison.Ordinal));

    public static bool IsPermission(string? key) => key != null && Everything.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// The coarse role that goes with a preset and the permissions it ends up with: a
    /// viewer when nothing in it changes anything, a manager for the manager preset, else
    /// a member. Older clients and a few checks still read the role.
    /// </summary>
    public static OrgMemberRole RoleFor(PermissionPreset preset, IReadOnlySet<string> permissions) =>
        !permissions.Any(Writes.Contains) ? OrgMemberRole.Viewer
        : preset == Manager ? OrgMemberRole.Manager
        : OrgMemberRole.Member;

    /// <summary>The preset a member is shown under: theirs, or the one their old role maps to.</summary>
    public static PermissionPreset PresetOf(OrgMemberRole role, string? presetKey) =>
        role == OrgMemberRole.Owner ? Owner
        : FindPreset(presetKey) is { } preset && preset != Owner ? preset
        : role switch
        {
            OrgMemberRole.Manager => Manager,
            OrgMemberRole.Viewer => ReadOnlyAccountant,
            _ => SalesRep,
        };

    /// <summary>
    /// What a member may do: everything for the owner; else their own list, their
    /// preset's, or (with neither) what their old role allowed.
    /// </summary>
    public static IReadOnlySet<string> Effective(OrgMemberRole role, string? presetKey, string? stored)
    {
        if (role == OrgMemberRole.Owner) return new HashSet<string>(Everything, StringComparer.Ordinal);
        if (stored != null) return Parse(stored);
        if (FindPreset(presetKey) is { } preset && preset != Owner) return new HashSet<string>(preset.Permissions, StringComparer.Ordinal);
        return new HashSet<string>(role switch
        {
            OrgMemberRole.Manager => Manager.Permissions,
            OrgMemberRole.Viewer => ReadOnlyAccountant.Permissions,
            _ => LegacyMember,
        }, StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether a member's permissions differ from their preset's: an owner's own choice,
    /// or an old Member's, who keeps what Members could do.
    /// </summary>
    public static bool IsCustomised(OrgMemberRole role, string? presetKey, string? stored) =>
        role != OrgMemberRole.Owner
        && !Effective(role, presetKey, stored).SetEquals(PresetOf(role, presetKey).Permissions);

    /// <summary>
    /// The stored form of a list: known keys only, in catalogue order, each between commas
    /// (",records.edit,deals.all,") so a query can ask for one with a plain LIKE.
    /// </summary>
    public static string Format(IEnumerable<string> permissions)
    {
        var wanted = permissions.ToHashSet(StringComparer.Ordinal);
        return "," + string.Join(",", Everything.Where(wanted.Contains)) + ",";
    }

    /// <summary>A stored list read back; unknown keys (from a newer version, say) are left out.</summary>
    public static IReadOnlySet<string> Parse(string stored) =>
        stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsPermission)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>How a permission is held in the stored list, for a query: ",key,".</summary>
    public static string Stored(string permission) => "," + permission + ",";

    /// <summary>The presets that hold <paramref name="permission"/>, for a query over members with no list of their own.</summary>
    public static string[] PresetsWith(string permission) =>
        Presets.Where(p => p.Permissions.Contains(permission)).Select(p => p.Key).ToArray();
}
