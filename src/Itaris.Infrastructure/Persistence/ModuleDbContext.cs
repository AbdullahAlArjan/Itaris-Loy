using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for every module (doc 04: schema-per-module, shared database).
/// Applies the global persistence conventions from doc 04 Part 8:
/// snake_case-by-configuration is avoided in favor of explicit column names in each
/// module's entity configurations; UUIDv7 PKs are generated app-side (never by the DB);
/// created_at/updated_at are stamped automatically on SaveChanges;
/// optimistic concurrency uses PostgreSQL xmin on every Entity-derived type.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    private readonly string _schema = schema;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType, b =>
                {
                    b.Property<Guid>(nameof(Entity.Id)).ValueGeneratedNever();
                    b.Property<DateTimeOffset>(nameof(Entity.CreatedAt)).HasColumnName("created_at");
                    b.Property<DateTimeOffset>(nameof(Entity.UpdatedAt)).HasColumnName("updated_at");
                    b.Property<uint>("xmin").IsRowVersion();
                });
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
