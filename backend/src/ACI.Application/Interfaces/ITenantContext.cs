namespace ACI.Application.Interfaces;

/// <summary>
/// The workspace a request or a background job reads as. Every read of a workspace's
/// records is filtered to it by the database layer (AppDbContext's query filters),
/// whichever repository or service does the reading.
/// </summary>
/// <remarks>
/// <para>
/// For a signed-in request it is the organization in X-Organization-Id, once
/// OrganizationAccessMiddleware has checked the caller belongs to it, and with none the
/// caller's personal workspace. Isolation used to be a predicate each repository had to
/// remember, and the copies had drifted apart; now a repository that forgets one still
/// reads only the caller's workspace.
/// </para>
/// <para>
/// A request with nobody signed in, and every background job, starts with no workspace
/// and reads none of the workspaces' records. A public form, the lead webhook, the
/// SendGrid webhooks, a contract signing link, the MCP endpoint and the integration API
/// act for the organization their token or key names (<see cref="ActFor"/>). A job acts
/// for each record's workspace while it works on it, and reads across workspaces only
/// where it says so to EF (IgnoreQueryFilters), next to a comment saying why.
/// </para>
/// </remarks>
public interface ITenantContext
{
    /// <summary>The organization reads are scoped to; null in a personal workspace, or with no workspace at all.</summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// Whose personal workspace reads are scoped to when there is no organization; null with
    /// no workspace at all, which reads nothing.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Read as this workspace (an organization, or with none <paramref name="userId"/>'s
    /// personal workspace) until the returned scope is disposed. The workspace that applied
    /// before it applies again after.
    /// </summary>
    /// <param name="organizationId">The organization to act for; null for a personal workspace.</param>
    /// <param name="userId">
    /// Who is acting. It decides only which personal workspace applies when there is no
    /// organization; inside one, the organization's records are read whoever acts.
    /// </param>
    IDisposable ActFor(Guid? organizationId, Guid? userId);
}
