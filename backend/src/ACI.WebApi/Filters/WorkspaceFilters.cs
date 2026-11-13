using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ACI.WebApi.Filters;

/// <summary>
/// The action needs an organization. Club records, automations and teams belong to one.
/// </summary>
/// <remarks>
/// Without X-Organization-Id the caller is in their personal workspace, and these
/// tables can't keep one person's personal rows apart from another's: they scope by
/// organization alone, so "OrganizationId IS NULL" was a single bucket that every user
/// without an organization read and wrote — members' emails and balances, and rules
/// that then ran on other people's leads. Sequences had the same hole; see
/// SequencesController.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class WorkspaceRequiredAttribute : Attribute, IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        if (user.UserId == null)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }
        if (user.CurrentOrganizationId == null)
        {
            context.Result = WorkspaceProblem.Result(StatusCodes.Status400BadRequest, "Organization required",
                "This belongs to an organization. Create or join one in Settings, then try again.");
            return Task.CompletedTask;
        }
        return next();
    }
}

/// <summary>
/// In an organization, the caller must hold these permissions (see MemberPermissions);
/// in a personal workspace everything is the caller's own, so it is theirs to do.
/// </summary>
/// <remarks>
/// <para>
/// Read fresh on every request from the membership, like the role before it, so a change
/// an owner makes takes effect at once, whatever token the member holds.
/// </para>
/// <para>
/// On a controller with <see cref="WritesOnly"/>, reads (GET, HEAD) pass and only changes
/// are checked; on an action it applies to that action. Several on one action all apply.
/// EndpointPermissionTests fails when an endpoint that changes something has no
/// permission and is not on its list of self-service or anonymous endpoints.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    /// <param name="permissions">The permissions needed: all of them, or with <see cref="Any"/> one.</param>
    public RequiresAttribute(params string[] permissions) => Permissions = permissions;

    /// <summary>
    /// Before the check of the request's body (-2000), so someone who may not do this hears
    /// that, not what was wrong with what they sent.
    /// </summary>
    public int Order => -3000;

    public string[] Permissions { get; }

    /// <summary>Any one of the permissions is enough.</summary>
    public bool Any { get; init; }

    /// <summary>Only requests that change something are checked; reads pass.</summary>
    public bool WritesOnly { get; init; }

    /// <summary>Finishes "Your role here does not let you …"; by default the first missing permission's own words.</summary>
    public string? What { get; init; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (WritesOnly && (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)))
        {
            await next();
            return;
        }
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUserService>();
        if (user.UserId is not { } userId)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (user.CurrentOrganizationId is { } organizationId)
        {
            var held = await services.GetRequiredService<IPermissionService>()
                .PermissionsAsync(userId, organizationId, context.HttpContext.RequestAborted);
            var missing = Permissions.Where(p => !held.Contains(p)).ToList();
            if (Any ? missing.Count == Permissions.Length : missing.Count > 0)
            {
                context.Result = WorkspaceProblem.Result(StatusCodes.Status403Forbidden, "Not allowed for your role",
                    MemberPermissions.Refusal(What ?? MemberPermissions.Phrase(missing[0])), missing[0]);
                return;
            }
        }
        await next();
    }
}

/// <summary>
/// The controller's endpoints belong to one of the workspace's modules (Sales or Club, see
/// WorkspaceModules), and answer one refusal while the workspace has that module turned off:
/// 403 with errorCode Module.Off and the module named, whoever asks and whatever they ask.
/// </summary>
/// <remarks>
/// <para>
/// Checked before anything else about the request (the caller's permissions, its body), so
/// the answer is the same for everyone. A personal workspace has no modules to turn off.
/// </para>
/// <para>
/// Endpoints people outside the workspace use are left out on purpose and keep answering
/// with a module off: a pay link, a payment provider's webhook, a public application or
/// capture form, an unsubscribe link, a contract sent for signing. Turning a module off
/// hides it from the team; it does not refuse money coming in or people answering what was
/// sent to them. EndpointModuleTests fails when a controller says neither which module it
/// is in nor why it is in none.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InModuleAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    /// <param name="module">WorkspaceModules.Sales or WorkspaceModules.Club.</param>
    public InModuleAttribute(string module) => Module = module;

    public string Module { get; }

    /// <summary>Before the permission check (-3000) and the body's (-2000).</summary>
    public int Order => -4000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUserService>();
        // A member in the member portal names their club in their session token, and a header
        // they add counts for nothing: OrganizationAccessMiddleware checks the header only for
        // staff, so when it was read first, a member whose club had turned the module off could
        // name another workspace (any id at all) and be answered as if the module were on.
        var member = MemberPortalClaims.Of(context.HttpContext.User);
        var workspace = member != null ? member.OrganizationId : user.CurrentOrganizationId;
        if (workspace is Guid organizationId
            && !await services.GetRequiredService<IWorkspaceModules>().IsOnAsync(organizationId, Module, context.HttpContext.RequestAborted))
        {
            var refusal = ACI.WebApi.Extensions.ResultExtensions.ToProblemResult(DomainErrors.Modules.Off(Module));
            if (refusal.Value is ProblemDetails problem) problem.Extensions["module"] = Module;
            context.Result = refusal;
            return;
        }
        await next();
    }
}

/// <summary>
/// The action works on the organization its route names (/api/organizations/{id}/…), which
/// need not be the one in X-Organization-Id. When the caller belongs to it, the action
/// reads as that organization, so what it reads is that organization's and nobody else's.
/// </summary>
/// <remarks>
/// Reads are filtered to the request's workspace (AppDbContext), which is the header's.
/// An owner looking at the webhook deliveries or the suppression list of one of their
/// organizations from another one's tab would otherwise see an empty list. A caller who
/// is not a member is left as they are, and the action's own check refuses them.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ActsForRouteOrganizationAttribute : Attribute, IAsyncActionFilter
{
    /// <param name="routeKey">The route value that holds the organization's id.</param>
    public ActsForRouteOrganizationAttribute(string routeKey) => RouteKey = routeKey;

    public string RouteKey { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUserService>();
        if (user.UserId is Guid userId
            && Guid.TryParse(context.RouteData.Values[RouteKey]?.ToString(), out var organizationId)
            && await services.GetRequiredService<IOrganizationRepository>().IsMemberAsync(userId, organizationId, context.HttpContext.RequestAborted))
        {
            // Its settings, members, keys and invitations are the workspace's too: one that
            // requires single sign-on refuses them to a session that began another way.
            if (await SsoGate.RefusalAsync(context.HttpContext, organizationId, context.HttpContext.RequestAborted, knownMember: true) is { } refusal)
            {
                context.Result = refusal;
                return;
            }
            using (services.GetRequiredService<ITenantContext>().ActFor(organizationId, userId))
                await next();
            return;
        }
        await next();
    }
}

file static class WorkspaceProblem
{
    // A sentence in `detail`, because the app shows it as the error toast. A refusal for
    // want of a permission also names it, so the app can say which one is missing.
    public static ObjectResult Result(int status, string title, string detail, string? permission = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (permission != null)
        {
            problem.Extensions["errorCode"] = "Permission.Required";
            problem.Extensions["permission"] = permission;
        }
        return new ObjectResult(problem) { StatusCode = status };
    }
}
