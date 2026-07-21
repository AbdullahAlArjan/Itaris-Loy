using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Ops.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Ops.Persistence;

public sealed class OpsDbContext(DbContextOptions<OpsDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "ops";

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.Property(a => a.Id).HasColumnName("id");
            b.Property(a => a.MerchantId).HasColumnName("merchant_id");
            b.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
            b.Property(a => a.ActorType).HasColumnName("actor_type").HasMaxLength(16);
            b.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(128);
            b.Property(a => a.EntityId).HasColumnName("entity_id");
            b.Property(a => a.Action).HasColumnName("action").HasMaxLength(32);
            b.Property(a => a.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
            b.Property(a => a.DeviceId).HasColumnName("device_id");
            b.Property(a => a.Reason).HasColumnName("reason");
            b.HasIndex(a => new { a.MerchantId, a.CreatedAt });
            b.HasIndex(a => a.ActorUserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
