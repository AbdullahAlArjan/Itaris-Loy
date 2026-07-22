using System.Text.Json;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Itaris.Modules.Transactions.Features.Idempotency;

/// <summary>
/// Wraps an [IDEM] endpoint (doc 05) with lock-or-replay. The key + request hash come from
/// HttpContext.Items (set by IdempotencyMiddleware). A replay returns the byte-identical original
/// response; a same-key different-payload request → 409 IDEMPOTENCY_CONFLICT; a failed attempt
/// releases the key so the client can retry. The endpoint must return its response DTO directly
/// (not an IResult) so it can be serialized for replay.
/// </summary>
public sealed class IdempotencyEndpointFilter(
    IdempotencyService service,
    ICurrentUser currentUser,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions) : IEndpointFilter
{
    private const string KeyItem = "IdempotencyKey";
    private const string HashItem = "IdempotencyRequestHash";
    private readonly JsonSerializerOptions _json = jsonOptions.Value.SerializerOptions;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.Items[KeyItem] is not string clientKey || string.IsNullOrWhiteSpace(clientKey))
        {
            throw new ApiException(400, ErrorCodes.ValidationError, "Idempotency-Key header is required.");
        }

        var requestHash = http.Items[HashItem] as string ?? string.Empty;
        var actor = currentUser.Id ?? Guid.Empty;
        var key = IdempotencyService.ComposeKey(clientKey, http.Request.Path, actor);

        var decision = await service.BeginAsync(key, requestHash, http.RequestAborted);
        switch (decision.Outcome)
        {
            case IdempotencyOutcome.Conflict:
                throw new ApiException(409, ErrorCodes.IdempotencyConflict,
                    "This Idempotency-Key was used with a different request.");
            case IdempotencyOutcome.Replay:
                return Results.Content(decision.ReplayBody!, "application/json", statusCode: decision.ReplayStatus);
        }

        try
        {
            var result = await next(context);
            var body = JsonSerializer.Serialize(result, _json);
            await service.CompleteAsync(key, StatusCodes.Status200OK, body, CancellationToken.None);
            return result;
        }
        catch (ApiException)
        {
            await service.ReleaseAsync(key, CancellationToken.None);
            throw;
        }
    }
}
