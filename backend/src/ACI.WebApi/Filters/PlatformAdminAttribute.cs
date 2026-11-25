using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ACI.WebApi.Filters;

/// <summary>
/// The action is for platform admins only (<see cref="IPlatformAdmins"/>, Platform:AdminEmails):
/// 401 without a session, 403 for anyone else, whatever workspace they are in or what they may do
/// there.
/// </summary>
/// <remarks>
/// Checked as the request is authorised, before its query or body is looked at, and read fresh
/// every time, so taking an address off the list takes effect at the next request. An action
/// that allows anonymous callers is left alone (the tracker's ingest, on the same controller).
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PlatformAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string ErrorCode = "Platform.AdminOnly";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;
        var services = context.HttpContext.RequestServices;
        if (services.GetRequiredService<ICurrentUserService>().UserId is not { } userId)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (await services.GetRequiredService<IPlatformAdmins>().IsPlatformAdminAsync(userId, context.HttpContext.RequestAborted)) return;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Platform admins only",
            Detail = "Only the people who run CadenceE can see this.",
        };
        problem.Extensions["errorCode"] = ErrorCode;
        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
