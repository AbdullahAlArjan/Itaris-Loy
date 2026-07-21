using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Loyalty.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Persistence;

public sealed class LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "loyalty";

    public DbSet<LoyaltyProgram> Programs => Set<LoyaltyProgram>();
    public DbSet<LoyaltyRule> Rules => Set<LoyaltyRule>();
    public DbSet<CustomerMembership> Memberships => Set<CustomerMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoyaltyProgram>(b =>
        {
            b.ToTable("loyalty_programs");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.MerchantId).HasColumnName("merchant_id");
            b.Property(p => p.Type).HasColumnName("type").HasMaxLength(16);
            b.Property(p => p.NameAr).HasColumnName("name_ar").HasMaxLength(200);
            b.Property(p => p.NameEn).HasColumnName("name_en").HasMaxLength(200);
            b.Property(p => p.Status).HasColumnName("status").HasMaxLength(16);
            b.Property(p => p.CurrentRuleId).HasColumnName("current_rule_id");
            b.HasIndex(p => p.MerchantId);
            // At most one active program per merchant (doc 06 freeze), enforced at the DB level.
            b.HasIndex(p => p.MerchantId)
                .IsUnique()
                .HasFilter($"status = '{ProgramStatuses.Active}'")
                .HasDatabaseName("ux_loyalty_programs_one_active_per_merchant");
        });

        modelBuilder.Entity<LoyaltyRule>(b =>
        {
            b.ToTable("loyalty_rules");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.ProgramId).HasColumnName("program_id");
            b.Property(r => r.Version).HasColumnName("version");
            b.Property(r => r.ConfigJson).HasColumnName("config").HasColumnType("jsonb");
            b.Property(r => r.EffectiveFrom).HasColumnName("effective_from");
            b.HasIndex(r => new { r.ProgramId, r.Version }).IsUnique();
        });

        modelBuilder.Entity<CustomerMembership>(b =>
        {
            b.ToTable("customer_memberships");
            b.Property(m => m.Id).HasColumnName("id");
            b.Property(m => m.CustomerId).HasColumnName("customer_id");
            b.Property(m => m.MerchantId).HasColumnName("merchant_id");
            b.Property(m => m.ProgramId).HasColumnName("program_id");
            b.Property(m => m.PointsBalance).HasColumnName("points_balance");
            b.Property(m => m.StampsFilled).HasColumnName("stamps_filled");
            b.Property(m => m.StampCardCycle).HasColumnName("stamp_card_cycle");
            b.Property(m => m.JoinedAt).HasColumnName("joined_at");
            b.Property(m => m.JoinSource).HasColumnName("join_source").HasMaxLength(16);
            // A customer joins a given merchant's program once (doc 05 C3 ALREADY_MEMBER).
            b.HasIndex(m => new { m.CustomerId, m.MerchantId }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
