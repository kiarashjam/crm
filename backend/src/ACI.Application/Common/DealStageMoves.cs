using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// How a deal comes into a stage, wherever it happens: a new deal, a converted lead, a
/// move on the board, a close from the deal page.
/// </summary>
/// <remarks>
/// Every arrival writes one <see cref="DealStageChange"/>, the first one included, so the
/// history says when the deal entered each stage and the board can tell how long it has
/// been in the one it is in. Before, only a move made by stage id was written: a deal
/// created in a stage, a move by name and a close from the deal page left no trace.
/// </remarks>
public static class DealStageMoves
{
    /// <summary>The outcome a stage gives the deals in it: won, lost, or null for an open stage.</summary>
    public static bool? OutcomeOf(DealStage stage) => stage.IsWon ? true : stage.IsLost ? false : null;

    /// <summary>
    /// Puts a deal being created in <paramref name="stage"/>: its pipeline is the stage's,
    /// its stage name the stage's own, and a won or lost stage closes it.
    /// </summary>
    public static void Place(Deal deal, DealStage stage, DateTime now)
    {
        deal.DealStageId = stage.Id;
        deal.PipelineId = stage.PipelineId;
        deal.Stage = stage.Name;
        if (OutcomeOf(stage) is bool outcome)
        {
            deal.IsWon = outcome;
            deal.ClosedAtUtc = now;
        }
    }

    /// <summary>
    /// The history line for a deal that has just come into the stage it is in, or null
    /// when it is in none (no stage id and no stage name).
    /// </summary>
    /// <param name="fromStageId">The stage it left; null for a new deal.</param>
    /// <param name="fromStageName">That stage's name, kept in case the stage is later renamed or deleted.</param>
    public static DealStageChange? Entered(Deal deal, string? toStageName, Guid? fromStageId, string? fromStageName, Guid userId, DateTime at)
    {
        var name = string.IsNullOrWhiteSpace(toStageName) ? deal.Stage : toStageName;
        if (deal.DealStageId == null && string.IsNullOrWhiteSpace(name)) return null;
        // No id: the database layer gives it one as the deal is saved. A line that came
        // with an id, found on a deal already stored, was taken for a line already stored
        // too, and saved as an update of nothing.
        return new DealStageChange
        {
            DealId = deal.Id,
            FromDealStageId = fromStageId,
            FromStageName = Fit(fromStageName),
            ToDealStageId = deal.DealStageId,
            ToStageName = Fit(name),
            ChangedByUserId = userId,
            ChangedAtUtc = at,
        };
    }

    /// <summary>Whether two stage names are the same stage, as people type them: case and outer spaces aside.</summary>
    public static bool SameName(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    // The history keeps names in 128 characters, as stages and deals do.
    private static string? Fit(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= 128 ? trimmed : trimmed[..128];
    }
}
