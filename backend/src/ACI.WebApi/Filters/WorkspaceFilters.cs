using ACI.Application.Interfaces;
using ACI.Domain.Enums;
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
/// In an organization, only its owner or a manager may do this. In a personal
/// workspace everything is the caller's own, so it is theirs to do.
/// </summary>
/// <remarks>
/// The same rule the pipeline, stage and lead-routing settings use. A Member could
/// otherwise set up workspace-wide rules that reassign records and send mail.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ManagersOnlyAttribute : Attribute, IAsyncActionFilter
{
    /// <param name="what">Finishes "Only the owner or a manager can …".</param>
    public ManagersOnlyAttribute(string what) => What = what;

    public string What { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUserService>();
        if (user.UserId is not { } userId)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (user.CurrentOrganizationId is { } organizationId)
        {
            var role = await services.GetRequiredService<IOrganizationRepository>()
                .GetMemberRoleAsync(userId, organizationId, context.HttpContext.RequestAborted);
            if (role is not (OrgMemberRole.Owner or OrgMemberRole.Manager))
            {
                context.Result = WorkspaceProblem.Result(StatusCodes.Status403Forbidden, "Owner or manager only",
                    $"Only the owner or a manager can {What}.");
                return;
            }
        }
        await next();
    }
}

file static class WorkspaceProblem
{
    // A sentence in `detail`, because the app shows it as the error toast.
    public static ObjectResult Result(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
