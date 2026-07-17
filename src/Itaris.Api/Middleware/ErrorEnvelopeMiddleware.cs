using Itaris.SharedKernel;

namespace Itaris.Api.Middleware;

/// <summary>
/// Maps every unhandled exception to the doc 05 error envelope:
/// <c>{ "error": { "code": "...", "message": "...", "details": {...} } }</c>
/// with a stable machine code from <see cref="ErrorCodes"/> (§9.9).
/// </summary>
public sealed class ErrorEnvelopeMiddleware(RequestDelegate next, ILogger<ErrorEnvelopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                error = new { code = ex.Code, message = ex.Message, details = ex.Details },
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                error = new { code = ErrorCodes.ServerError, message = "An unexpected error occurred.", details = (object?)null },
            });
        }
    }
}
