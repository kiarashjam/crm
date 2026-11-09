namespace ACI.Application.DTOs;

/// <summary>
/// A rep's open work, or the team's: the records the assistant's queue works out the next
/// step for. <paramref name="Truncated"/> is true when a list held more than was sent.
/// </summary>
public record WorkQueueDto(
    string Scope,
    IReadOnlyList<TaskDto> Tasks,
    IReadOnlyList<LeadDto> Leads,
    IReadOnlyList<DealDto> Deals,
    bool Truncated);
