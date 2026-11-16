using System.Text.Json.Nodes;

namespace ACI.WebApi.Assistant;

/// <summary>
/// A change the assistant proposes and the person must confirm. Nothing is written when it is
/// proposed: the app shows it as a card with these details, and only pressing Confirm sends
/// <see cref="Payload"/> to the endpoint the app's own page would use for it, with that
/// endpoint's permission checks.
/// </summary>
/// <param name="Id">For the app to send it once, however often Confirm is pressed.</param>
/// <param name="Kind">
/// task, activity, deal-stage, email-draft (opens the composer filled in; nothing is sent until
/// the person sends it there), booking or charge.
/// </param>
/// <param name="Title">What it does, in a few words: "Create a task".</param>
/// <param name="Details">The facts the card shows, read from the records, one per line.</param>
/// <param name="Payload">The request body for the endpoint, as the app's own forms send it.</param>
/// <param name="Link">The record it is about, to open: "/leads/{id}".</param>
public sealed record AssistantProposal(
    string Id,
    string Kind,
    string Title,
    IReadOnlyList<string> Details,
    JsonObject Payload,
    string? Link);

/// <summary>The kinds of change the assistant can propose.</summary>
public static class AssistantProposalKinds
{
    public const string Task = "task";
    public const string Activity = "activity";
    public const string DealStage = "deal-stage";
    public const string EmailDraft = "email-draft";
    public const string Booking = "booking";
    public const string Charge = "charge";
}
