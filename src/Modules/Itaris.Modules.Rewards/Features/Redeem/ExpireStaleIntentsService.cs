using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>
/// doc 04 ExpireStaleIntents (background job) — releases holds for pending redemptions past their TTL
/// (doc 06 test: expiry releases hold). Each redemption is released in its own transaction under a
/// row lock so it never races the cashier's confirm.
/// </summary>
public sealed class ExpireStaleIntentsService(RewardsDbContext db, RedemptionReleaser releaser, IClock clock)
{
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var stale = await db.Redemptions.AsNoTracking()
            .Where(r => r.Status == RedemptionStatuses.Pending && r.ExpiresAt <= now)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var id in stale)
        {
            if (await TryReleaseAsync(id, cancellationToken))
            {
                released++;
            }
        }

        return released;
    }

    private async Task<bool> TryReleaseAsync(Guid redemptionId, CancellationToken cancellationToken)
    {
        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        var redemption = await db.Redemptions
            .FromSqlInterpolated($"SELECT *, xmin FROM rewards.redemptions WHERE id = {redemptionId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        // Someone confirmed/cancelled it between the scan and the lock — skip.
        if (redemption is null || redemption.Status != RedemptionStatuses.Pending)
        {
            await dbTx.RollbackAsync(cancellationToken);
            return false;
        }

        await releaser.ReleaseAsync(db, redemption, RedemptionStatuses.Expired, connection, rawTx, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);
        return true;
    }
}

/// <summary>Runs the expiry sweep periodically.</summary>
public sealed class ExpireStaleIntentsWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sweeper = scope.ServiceProvider.GetRequiredService<ExpireStaleIntentsService>();
                await sweeper.SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Best-effort background job — swallow and retry next tick.
            }
        }
    }
}
