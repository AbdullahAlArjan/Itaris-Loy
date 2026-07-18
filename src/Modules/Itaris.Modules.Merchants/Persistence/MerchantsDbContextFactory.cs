using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Merchants.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class MerchantsDbContextFactory : IDesignTimeDbContextFactory<MerchantsDbContext>
{
    public MerchantsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MerchantsDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MerchantsDbContext.Schema))
            .Options;

        return new MerchantsDbContext(options);
    }
}
