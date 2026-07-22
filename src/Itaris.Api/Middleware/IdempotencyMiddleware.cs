using System.Security.Cryptography;

namespace Itaris.Api.Middleware;

/// <summary>
/// Front half of doc 05 idempotency: for POSTs carrying an Idempotency-Key header, captures the
/// key and a SHA-256 hash of the raw body into HttpContext.Items. The lock-or-replay logic lives
/// in the Transactions module's IdempotencyFilter over transactions.idempotency_records
/// (doc 04: the table belongs to the transactions schema).
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";
    public const string KeyItem = "IdempotencyKey";
    public const string HashItem = "IdempotencyRequestHash";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Headers.TryGetValue(HeaderName, out var key) &&
            !string.IsNullOrWhiteSpace(key))
        {
            context.Items[KeyItem] = key.ToString();

            context.Request.EnableBuffering();
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(context.Request.Body, context.RequestAborted);
            context.Request.Body.Position = 0;
            context.Items[HashItem] = Convert.ToHexString(hash);
        }

        await next(context);
    }
}
