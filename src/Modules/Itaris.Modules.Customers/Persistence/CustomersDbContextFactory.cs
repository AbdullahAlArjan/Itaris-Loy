using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Customers.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CustomersDbContext.Schema))
            .Options;

        return new CustomersDbContext(options);
    }
}
