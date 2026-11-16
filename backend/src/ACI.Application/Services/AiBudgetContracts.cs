using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// What calls to the language model may cost a workspace: a monthly allowance, kept on the
/// server and set by a manager below the server's own ceiling, and a short-term throttle on a
/// whole team asking at once.
/// </summary>
/// <remarks>
/// Every call to the model is billed on the server's key: the assistant's questions (each turn
/// of the model counts, as each is billed), its morning briefs and the AI writer's drafts. The
/// AI writer was unlimited for anyone who signed up; now each workspace is held to
/// Assistant:DefaultMonthlyLimit until a manager sets its own, never above
/// Assistant:MaxMonthlyLimit, and outside any organization a person is held to
/// Assistant:PersonalMonthlyLimit. One person asking too fast is slowed by the "assistant"
/// and "ai-copy" rate limits, per user, before any of this.
/// </remarks>
public interface IAiBudget
{
    /// <summary>The month's allowance as it stands: how many calls are made, out of how many.</summary>
    /// <param name="organizationId">The workspace; null for a person's use outside any organization.</param>
    Task<AiAllowance> AllowanceAsync(Guid? organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Takes one call from the month's allowance, before the call is made; two at the same moment
    /// cannot both take the last one. <paramref name="personWaiting"/> also counts it against the
    /// workspace's short-term throttle (a background job, such as the morning brief, is not).
    /// </summary>
    /// <returns>The allowance after taking it, or 429 Ai.MonthlyLimit / Ai.Busy.</returns>
    Task<Result<AiAllowance>> TakeAsync(Guid? organizationId, Guid userId, bool personWaiting, CancellationToken ct = default);

    /// <summary>Adds what a call used to the month's figures. Never fails the call it follows.</summary>
    Task RecordTokensAsync(Guid? organizationId, Guid userId, int inputTokens, int outputTokens, CancellationToken ct = default);
}

/// <summary>A month's allowance of calls to the language model.</summary>
/// <param name="Used">Calls made this month.</param>
/// <param name="Limit">Calls the month allows; 0 means the AI is off here.</param>
/// <param name="Month">The month, as its first day (UTC).</param>
/// <param name="ResetsOn">The first day of next month, when the count starts again.</param>
/// <param name="Refused">Calls refused this month because the allowance was used up.</param>
public sealed record AiAllowance(int Used, int Limit, DateOnly Month, DateOnly ResetsOn, int Refused, long InputTokens, long OutputTokens)
{
    public int Left => Math.Max(0, Limit - Used);
}

/// <summary>A workspace's settings for the assistant, read by anyone in it and changed by those who manage settings.</summary>
public interface IAssistantSettingsService
{
    Task<Result<AssistantSettingsDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <param name="expectedVersion">The version the edit was made from, from the body or If-Match; null skips the check.</param>
    Task<Result<AssistantSettingsDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateAssistantSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Whether the workspace's people get a morning brief (its own switch; on when it saved nothing).</summary>
    Task<bool> MorningBriefsOnAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>What Settings › Assistant shows.</summary>
/// <param name="Configured">Whether a language model is set up on the server.</param>
/// <param name="Provider">Who answers when one is ("OpenAI").</param>
/// <param name="Model">The model asked.</param>
/// <param name="MonthlyLimit">The limit that applies: the workspace's own, else the server's default.</param>
/// <param name="CustomLimit">The workspace's own limit; null when it uses the server's default.</param>
/// <param name="DefaultLimit">The server's default (Assistant:DefaultMonthlyLimit).</param>
/// <param name="MaxLimit">The highest limit the server allows a workspace (Assistant:MaxMonthlyLimit).</param>
/// <param name="MorningBriefs">Whether the workspace's people get a morning brief.</param>
/// <param name="Usage">This month's calls so far.</param>
/// <param name="Version">The version to send back with a change.</param>
public sealed record AssistantSettingsDto(
    bool Configured,
    string Provider,
    string Model,
    int MonthlyLimit,
    int? CustomLimit,
    int DefaultLimit,
    int MaxLimit,
    bool MorningBriefs,
    AiAllowance Usage,
    Guid? Version);

/// <summary>A change to the assistant's settings; a field left out keeps its value.</summary>
public sealed class UpdateAssistantSettingsRequest
{
    /// <summary>The workspace's own monthly limit, 0 up to the server's maximum.</summary>
    public int? MonthlyLimit { get; set; }

    /// <summary>True goes back to the server's default limit (and ignores <see cref="MonthlyLimit"/>).</summary>
    public bool? UseDefaultLimit { get; set; }

    public bool? MorningBriefs { get; set; }

    public Guid? Version { get; set; }
}
