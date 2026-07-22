using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Rewards.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class RewardsDbContextFactory : IDesignTimeDbContextFactory<RewardsDbContext>
{
    public RewardsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RewardsDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", RewardsDbContext.Schema))
            .Options;

        return new RewardsDbContext(options);
    }
}
