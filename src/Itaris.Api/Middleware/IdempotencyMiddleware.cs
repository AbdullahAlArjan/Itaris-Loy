namespace Itaris.Api.Middleware;

/// <summary>
/// Phase 1 SHELL for doc 05 idempotency: endpoints marked [IDEM] require an
/// Idempotency-Key: &lt;uuid&gt; header; replays return the original response; a conflicting
/// payload with the same key returns 409 IDEMPOTENCY_CONFLICT.
///
/// The real lock-or-replay store lives in the transactions schema
/// (idempotency_records, doc 04 Part 8) and lands with the Transactions module (Phase 4).
/// Until then this middleware only captures the header into HttpContext.Items so handlers
/// and logs can see it — deliberately a no-op passthrough, per docs/decisions.md.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ItemKey = "IdempotencyKey";

    public Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Headers.TryGetValue(HeaderName, out var key))
        {
            context.Items[ItemKey] = key.ToString();
        }

        return next(context);
    }
}
