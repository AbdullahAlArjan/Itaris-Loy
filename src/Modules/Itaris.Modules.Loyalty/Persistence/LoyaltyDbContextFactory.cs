using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Loyalty.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class LoyaltyDbContextFactory : IDesignTimeDbContextFactory<LoyaltyDbContext>
{
    public LoyaltyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LoyaltyDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LoyaltyDbContext.Schema))
            .Options;

        return new LoyaltyDbContext(options);
    }
}
