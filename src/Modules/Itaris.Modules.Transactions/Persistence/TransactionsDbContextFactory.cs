using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Itaris.Modules.Transactions.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations` only; never used at runtime.</summary>
public sealed class TransactionsDbContextFactory : IDesignTimeDbContextFactory<TransactionsDbContext>
{
    public TransactionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseNpgsql("Host=localhost;Database=itaris;Username=itaris;Password=itaris",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TransactionsDbContext.Schema))
            .Options;

        return new TransactionsDbContext(options);
    }
}
