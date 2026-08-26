using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// Actions that can be attempted on a contract.
/// </summary>
public static class ContractActions
{
    public const string Edit = "edit";
    public const string Send = "send";
    public const string ClientSign = "client_sign";
    public const string Decline = "decline";
    public const string Countersign = "countersign";
    public const string Void = "void";
    public const string Resend = "resend";
}

/// <summary>
/// The authoritative contract lifecycle: what may follow what.
/// </summary>
/// <remarks>
/// <para>
/// The client has the same table in <c>contractLifecycle.ts</c> so it can grey out
/// buttons the server would refuse, but THIS is the one that decides. A UI copy of
/// a rule is a convenience; a server copy is the rule.
/// </para>
/// <para>
/// The four transitions that must be impossible, and what you would be holding if
/// any of them were allowed:
/// </para>
/// <list type="bullet">
/// <item>Countersign before the client signs — an "executed" contract carrying
/// only our own signature, which looks complete and is not.</item>
/// <item>Edit after sending — the counterparty signs one text while we keep
/// another. This one failure would make the whole feature worthless as evidence,
/// which is why <see cref="Contract.BodyHashAtSend"/> exists to catch it even if
/// the gate were ever bypassed.</item>
/// <item>Sign twice, or sign something already declined or voided.</item>
/// <item>Send twice — a second token would leave the first link live, so two
/// different people could each sign a copy.</item>
/// </list>
/// </remarks>
public static class ContractStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Transitions =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [ContractStatuses.Draft] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContractActions.Edit] = ContractStatuses.Draft,
                [ContractActions.Send] = ContractStatuses.Sent,
                [ContractActions.Void] = ContractStatuses.Voided,
            },
            [ContractStatuses.Sent] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // No Edit. See the class remarks.
                [ContractActions.ClientSign] = ContractStatuses.SignedByClient,
                [ContractActions.Decline] = ContractStatuses.Declined,
                [ContractActions.Resend] = ContractStatuses.Sent,
                [ContractActions.Void] = ContractStatuses.Voided,
            },
            [ContractStatuses.SignedByClient] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContractActions.Countersign] = ContractStatuses.Countersigned,
                [ContractActions.Void] = ContractStatuses.Voided,
            },
            [ContractStatuses.Countersigned] = new Dictionary<string, string>(StringComparer.Ordinal),
            [ContractStatuses.Declined] = new Dictionary<string, string>(StringComparer.Ordinal),
            [ContractStatuses.Voided] = new Dictionary<string, string>(StringComparer.Ordinal),
        };

    /// <summary>Statuses from which nothing further can happen.</summary>
    public static readonly IReadOnlyList<string> TerminalStatuses = new[]
    {
        ContractStatuses.Countersigned, ContractStatuses.Declined, ContractStatuses.Voided,
    };

    public static bool IsTerminal(string status) => TerminalStatuses.Contains(status, StringComparer.Ordinal);

    /// <summary>True when <paramref name="action"/> is legal in <paramref name="status"/>.</summary>
    public static bool Can(string? status, string action)
        => status is not null
            && Transitions.TryGetValue(status, out var allowed)
            && allowed.ContainsKey(action);

    /// <summary>
    /// The status after <paramref name="action"/>, or null when it is not permitted.
    /// </summary>
    public static string? Next(string? status, string action)
    {
        if (status is null) return null;
        if (!Transitions.TryGetValue(status, out var allowed)) return null;
        return allowed.TryGetValue(action, out var target) ? target : null;
    }

    /// <summary>Every action legal right now, sorted, for the API to advertise.</summary>
    public static IReadOnlyList<string> AllowedActions(string? status)
    {
        if (status is null || !Transitions.TryGetValue(status, out var allowed)) return Array.Empty<string>();
        return allowed.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A typed signature has to be a plausible name rather than a placeholder,
    /// because that mark is the entire substance of the signature.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT a check that it matches the name on file: people sign as
    /// "J. Dupont" for "Jean Dupont", and refusing that would block a real
    /// signing over a formatting opinion. Mirrors <c>isSignatureNameValid</c>.
    /// </remarks>
    public static bool IsSignatureNameValid(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        if (trimmed.Length < 2) return false;
        return trimmed.Count(char.IsLetter) >= 2;
    }
}
