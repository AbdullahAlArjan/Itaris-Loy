using System.Data.Common;
using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.Modules.Loyalty.PublicApi;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.TransactionParticipation;

/// <summary>
/// Implements <see cref="ILoyaltyTransactionParticipant"/>. For enlisted operations it opens a
/// LoyaltyDbContext on the CALLER'S connection and joins the caller's transaction, so the balance
/// projection update commits or rolls back together with the Transactions module's ledger write.
/// Concurrency guard: SELECT … FOR UPDATE on the membership row (doc 04) serializes all earns and
/// reversals per membership.
/// </summary>
public sealed class LoyaltyTransactionParticipant(LoyaltyDbContext readDb, IClock clock)
    : ILoyaltyTransactionParticipant
{
    public async Task<EarnOutcome> ApplyEarnAsync(
        Guid merchantId, Guid customerId, long amountMinor,
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var db = CreateEnlisted(connection);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var program = await db.Programs.FirstOrDefaultAsync(
            p => p.MerchantId == merchantId && p.Status == ProgramStatuses.Active, cancellationToken);
        if (program is null || program.CurrentRuleId is null)
        {
            return new EarnOutcome(null, EarnFailure.ProgramInactive);
        }

        var rule = await db.Rules.FirstAsync(r => r.Id == program.CurrentRuleId, cancellationToken);
        var config = LoyaltyJson.Deserialize(rule.ConfigJson);

        var (membership, isNew, welcomeApplied) =
            await LockOrCreateMembershipAsync(db, program, config, merchantId, customerId, cancellationToken);

        var earn = LoyaltyCalculator.Calculate(program.Type, config, amountMinor);

        membership.PointsBalance += earn.PointsEarned;
        membership.StampsFilled += earn.StampsEarned;

        var completed = false;
        while (config.CardSize > 0 && membership.StampsFilled >= config.CardSize)
        {
            membership.StampsFilled -= config.CardSize; // carry-over across completion (doc 06 test)
            membership.StampCardCycle++;
            completed = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new EarnOutcome(new EarnApplication(
            membership.Id, customerId, isNew, program.Type, rule.Id,
            earn.PointsEarned, earn.StampsEarned,
            membership.PointsBalance, membership.StampsFilled, config.CardSize,
            completed, membership.StampCardCycle, welcomeApplied), EarnFailure.None);
    }

    public async Task<ReversalApplication> ApplyReversalAsync(
        Guid membershipId, long pointsDelta, int stampsDelta,
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var db = CreateEnlisted(connection);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        var membership = await db.Memberships
            .FromSqlInterpolated($"SELECT *, xmin FROM loyalty.customer_memberships WHERE id = {membershipId} FOR UPDATE")
            .FirstAsync(cancellationToken);

        membership.PointsBalance += pointsDelta; // negative for clawback; may go negative (doc 06)
        membership.StampsFilled = Math.Max(0, membership.StampsFilled + stampsDelta);

        await db.SaveChangesAsync(cancellationToken);
        return new ReversalApplication(membership.PointsBalance, membership.StampsFilled);
    }

    public async Task<EarnApplication?> GetMembershipSnapshotAsync(
        Guid merchantId, Guid customerId, CancellationToken cancellationToken)
    {
        var membership = await readDb.Memberships.AsNoTracking().FirstOrDefaultAsync(
            m => m.CustomerId == customerId && m.MerchantId == merchantId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var program = await readDb.Programs.AsNoTracking()
            .FirstAsync(p => p.Id == membership.ProgramId, cancellationToken);
        var cardSize = 0;
        if (program.CurrentRuleId is { } ruleId)
        {
            var rule = await readDb.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
            cardSize = rule is null ? 0 : LoyaltyJson.Deserialize(rule.ConfigJson).CardSize;
        }

        return new EarnApplication(
            membership.Id, customerId, IsNewMember: false, program.Type,
            program.CurrentRuleId ?? Guid.Empty, 0, 0,
            membership.PointsBalance, membership.StampsFilled, cardSize,
            CardCompleted: false, membership.StampCardCycle, WelcomeBonusApplied: 0);
    }

    public Task<Guid?> GetMembershipOwnerAsync(Guid membershipId, CancellationToken cancellationToken) =>
        readDb.Memberships.AsNoTracking()
            .Where(m => m.Id == membershipId)
            .Select(m => (Guid?)m.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<(CustomerMembership Membership, bool IsNew, int WelcomeApplied)> LockOrCreateMembershipAsync(
        LoyaltyDbContext db, LoyaltyProgram program, RuleConfig config,
        Guid merchantId, Guid customerId, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var locked = await db.Memberships
                .FromSqlInterpolated(
                    $"SELECT *, xmin FROM loyalty.customer_memberships WHERE customer_id = {customerId} AND merchant_id = {merchantId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
            if (locked is not null)
            {
                return (locked, false, 0);
            }

            // Auto-join on first transaction (doc 04 Loyalty command), counter-sourced.
            var membership = new CustomerMembership
            {
                CustomerId = customerId,
                MerchantId = merchantId,
                ProgramId = program.Id,
                JoinedAt = clock.UtcNow,
                JoinSource = MembershipJoinSources.Counter,
            };

            var welcome = config.WelcomeBonus;
            if (welcome > 0)
            {
                if (program.Type == ProgramTypes.Points)
                {
                    membership.PointsBalance = welcome;
                }
                else
                {
                    membership.StampsFilled = welcome;
                }
            }

            db.Memberships.Add(membership);
            try
            {
                await db.SaveChangesAsync(ct);
                return (membership, true, welcome);
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // Lost an auto-join race on the (customer, merchant) unique index — re-lock theirs.
                db.Entry(membership).State = EntityState.Detached;
            }
        }
    }

    private static LoyaltyDbContext CreateEnlisted(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<LoyaltyDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new LoyaltyDbContext(options);
    }
}
