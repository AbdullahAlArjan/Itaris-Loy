using Itaris.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Renders auth failures in the doc 05 error envelope: 401 UNAUTHORIZED when unauthenticated,
/// 403 FORBIDDEN when authenticated but lacking the required permission.
/// </summary>
public sealed class ItarisAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await WriteEnvelope(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Authentication required.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await WriteEnvelope(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "You do not have permission to perform this action.");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task WriteEnvelope(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            error = new { code, message, details = (object?)null },
        });
    }
}
