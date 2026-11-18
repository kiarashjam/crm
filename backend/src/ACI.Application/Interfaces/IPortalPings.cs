namespace ACI.Application.Interfaces;

/// <summary>
/// The last answer each workspace's portal gave to a signed test post (Welcome &amp; portal's
/// "Test the connection"): from the daily portal ping, or from someone pressing the button.
/// </summary>
/// <remarks>
/// Kept in memory, per server instance, like <see cref="IJobHeartbeats"/>: a restart forgets
/// them, and the ping at start-up asks every portal again.
/// </remarks>
public interface IPortalPings
{
    /// <summary>What the workspace's portal answered just now.</summary>
    void Record(Guid organizationId, PortalPingResult result);

    /// <summary>The workspace's last answer; null when its portal has not been asked on this instance.</summary>
    PortalPingResult? For(Guid organizationId);

    /// <summary>Every workspace's last answer, by workspace.</summary>
    IReadOnlyDictionary<Guid, PortalPingResult> Snapshot();

    /// <summary>Forgets a workspace whose portal is no longer set up, so an old failure does not linger.</summary>
    void Forget(Guid organizationId);
}

/// <summary>One answer from a workspace's portal.</summary>
/// <param name="Ok">It took the signed post (answered 2xx).</param>
/// <param name="AtUtc">When it was asked.</param>
/// <param name="StatusCode">What it answered, when it answered at all.</param>
/// <param name="Error">Why it did not take it, in one line; null when it did.</param>
public sealed record PortalPingResult(bool Ok, DateTime AtUtc, int? StatusCode, string? Error);
