using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Features.Idempotency;

public enum IdempotencyOutcome { Proceed, Replay, Conflict }

public sealed record IdempotencyDecision(IdempotencyOutcome Outcome, int ReplayStatus = 0, string? ReplayBody = null);

/// <summary>
/// Lock-or-replay over transactions.idempotency_records (doc 04 Part 9 rule 4): insert-first with
/// the unique key; on conflict return the stored response; mismatched request_hash → conflict.
/// A concurrent in-flight request with the same key waits briefly for the original's response.
/// Failed (non-2xx) attempts release the key so the client can retry.
/// </summary>
public sealed class IdempotencyService(TransactionsDbContext db, IClock clock)
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan InFlightWait = TimeSpan.FromSeconds(5);

    public static string ComposeKey(string clientKey, string route, Guid actorId) =>
        $"{clientKey}:{route}:{actorId}";

    public async Task<IdempotencyDecision> BeginAsync(string key, string requestHash, CancellationToken ct)
    {
        var deadline = clock.UtcNow + InFlightWait;

        while (true)
        {
            var now = clock.UtcNow;
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO transactions.idempotency_records (key, request_hash, locked_at, expires_at)
                VALUES ({key}, {requestHash}, {now}, {now + Retention})
                ON CONFLICT (key) DO NOTHING", ct);

            if (inserted == 1)
            {
                return new IdempotencyDecision(IdempotencyOutcome.Proceed);
            }

            var existing = await db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key, ct);
            if (existing is null)
            {
                continue; // released between our insert and read — try again
            }

            if (existing.RequestHash != requestHash)
            {
                return new IdempotencyDecision(IdempotencyOutcome.Conflict);
            }

            if (existing.ResponseBody is not null)
            {
                return new IdempotencyDecision(
                    IdempotencyOutcome.Replay, existing.ResponseStatus ?? 200, existing.ResponseBody);
            }

            if (clock.UtcNow >= deadline)
            {
                return new IdempotencyDecision(IdempotencyOutcome.Conflict);
            }

            await Task.Delay(100, ct); // original request is in flight — wait for its response
        }
    }

    /// <summary>Stores the successful response for future replays.</summary>
    public Task CompleteAsync(string key, int status, string body, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE transactions.idempotency_records
            SET response_status = {status}, response_body = {body}::jsonb
            WHERE key = {key}", ct);

    /// <summary>Releases the key after a failed attempt so the client can retry.</summary>
    public Task ReleaseAsync(string key, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM transactions.idempotency_records WHERE key = {key}", ct);
}
