using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Ops.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class OpsDbContextFactory : IDesignTimeDbContextFactory<OpsDbContext>
{
    public OpsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OpsDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", OpsDbContext.Schema))
            .Options;

        return new OpsDbContext(options);
    }
}
