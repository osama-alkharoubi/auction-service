using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Filters;

public class TenantAuthorizationFilter : IAsyncActionFilter
{
    private const string OrgHeaderName = "X-Org-Id";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            await next();
            return;
        }
        if (!httpContext.Request.Headers.TryGetValue(OrgHeaderName, out var headerValue) ||
            !Guid.TryParse(headerValue.ToString(), out var headerOrgId))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }


        var tokenOrgClaim = httpContext.User.FindFirst("org_id")?.Value;
        if (string.IsNullOrEmpty(tokenOrgClaim) || !Guid.TryParse(tokenOrgClaim, out var tokenOrgId))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }


        if (headerOrgId != tokenOrgId)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

     
        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        tenantContext.SetTenantId(headerOrgId);

        await next();
    }
}