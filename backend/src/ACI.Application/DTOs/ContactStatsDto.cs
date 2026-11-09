namespace ACI.Application.DTOs;

/// <summary>
/// The figures above the contacts list, counted over the whole workspace. They were worked
/// out in the browser from the twenty contacts on screen, so 640 contacts showed as
/// "Total Contacts 20". Every count is of active contacts except <c>Archived</c>.
/// </summary>
/// <param name="ActiveLast30Days">With activity in the last 30 days.</param>
/// <param name="QuietOver90Days">With no activity in the last 90 days, or none at all.</param>
public record ContactStatsDto(
    int Total,
    int WithCompany,
    int WithPhone,
    int ActiveLast30Days,
    int QuietOver90Days,
    int AddedLast7Days,
    int Archived);

/// <summary>A file the API hands back for download, and how many of the rows asked for it holds.</summary>
public record ExportFile(string FileName, byte[] Content, int Exported, int Total);
