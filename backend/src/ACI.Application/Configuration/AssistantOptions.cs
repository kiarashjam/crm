namespace ACI.Application.Configuration;

/// <summary>
/// The assistant's language model and what it may cost: section "Assistant". The model's key
/// is the AI writer's (OpenAI:ApiKey); nothing here is a secret.
/// </summary>
/// <remarks>
/// Every limit has a default, so a server with only OpenAI:ApiKey set already holds each
/// workspace to <see cref="DefaultMonthlyLimit"/> calls a month. Generation used to be
/// unlimited on the server's key for anyone who signed up.
/// </remarks>
public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>The switch for every model call the assistant and the morning brief make; off answers "not configured".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Which provider answers. "OpenAI" is the one built; any other leaves the assistant not configured.</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>The model to ask; empty means the AI writer's (OpenAI:Model).</summary>
    public string? Model { get; set; }

    /// <summary>The most the model may write in one turn.</summary>
    public int MaxOutputTokens { get; set; } = 800;

    /// <summary>
    /// How freely the model words its answers; low keeps it close to what the tools returned.
    /// Leave it empty for a model that takes no temperature (it is then not sent).
    /// </summary>
    public double? Temperature { get; set; } = 0.2;

    /// <summary>How long one call to the model may take before it counts as not answered.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>A workspace's monthly limit on calls to the model until a manager sets one in Settings.</summary>
    public int DefaultMonthlyLimit { get; set; } = 300;

    /// <summary>The highest monthly limit a workspace may set itself: the server's own ceiling on its key.</summary>
    public int MaxMonthlyLimit { get; set; } = 3000;

    /// <summary>The monthly limit for someone using the AI writer outside any organization (a personal workspace).</summary>
    public int PersonalMonthlyLimit { get; set; } = 100;

    /// <summary>How many questions one person may ask the assistant at once before being asked to slow down.</summary>
    public int UserBurst { get; set; } = 20;

    /// <summary>How many more questions a person may ask each minute, once the burst is used.</summary>
    public int UserPerMinute { get; set; } = 3;

    /// <summary>How many calls to the model a whole workspace may make at once (its team together).</summary>
    public int WorkspaceBurst { get; set; } = 60;

    /// <summary>How many more calls a workspace may make each minute, once its burst is used.</summary>
    public int WorkspacePerMinute { get; set; } = 30;

    /// <summary>The most model turns one question may take (each turn may look records up).</summary>
    public int MaxTurnsPerQuestion { get; set; } = 5;
}
