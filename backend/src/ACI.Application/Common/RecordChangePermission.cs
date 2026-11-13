using ACI.Domain.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// The permission that changing a record's tags and custom values takes: a club member's
/// are club work (club.members), as the member is; every other record's take records.edit.
/// The endpoints let either through (Requires … Any) and the services ask for the one the
/// kind of record needs, so a membership director tags members and a sales rep tags leads.
/// </summary>
public static class RecordChangePermission
{
    public static string For(string entityType) =>
        entityType == RecordTypes.Member ? MemberPermissions.ClubMembers : MemberPermissions.EditRecords;
}
