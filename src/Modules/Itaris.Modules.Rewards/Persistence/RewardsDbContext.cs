using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Rewards.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Rewards.Persistence;

public sealed class RewardsDbContext(DbContextOptions<RewardsDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "rewards";

    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reward>(b =>
        {
            b.ToTable("rewards");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.MerchantId).HasColumnName("merchant_id");
            b.Property(r => r.NameAr).HasColumnName("name_ar").HasMaxLength(200);
            b.Property(r => r.NameEn).HasColumnName("name_en").HasMaxLength(200);
            b.Property(r => r.DescriptionAr).HasColumnName("description_ar");
            b.Property(r => r.DescriptionEn).HasColumnName("description_en");
            b.Property(r => r.ImageUrl).HasColumnName("image_url");
            b.Property(r => r.CostType).HasColumnName("cost_type").HasMaxLength(24);
            b.Property(r => r.PointsCost).HasColumnName("points_cost");
            b.Property(r => r.StockRemaining).HasColumnName("stock_remaining");
            b.Property(r => r.PerCustomerLimit).HasColumnName("per_customer_limit");
            b.Property(r => r.ValidFrom).HasColumnName("valid_from");
            b.Property(r => r.ValidUntil).HasColumnName("valid_until");
            b.Property(r => r.Status).HasColumnName("status").HasMaxLength(16);
            b.HasIndex(r => r.MerchantId);
        });

        modelBuilder.Entity<Redemption>(b =>
        {
            b.ToTable("redemptions");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.MembershipId).HasColumnName("membership_id");
            b.Property(r => r.CustomerId).HasColumnName("customer_id");
            b.Property(r => r.MerchantId).HasColumnName("merchant_id");
            b.Property(r => r.RewardId).HasColumnName("reward_id");
            b.Property(r => r.Status).HasColumnName("status").HasMaxLength(16);
            b.Property(r => r.Code).HasColumnName("code").HasMaxLength(6);
            b.Property(r => r.PointsHeld).HasColumnName("points_held");
            b.Property(r => r.StampCardConsumed).HasColumnName("stamp_card_consumed");
            b.Property(r => r.ExpiresAt).HasColumnName("expires_at");
            b.Property(r => r.ConfirmedAt).HasColumnName("confirmed_at");
            b.Property(r => r.ConfirmedByStaffId).HasColumnName("confirmed_by_staff_id");
            b.HasIndex(r => r.Code).IsUnique();
            b.HasIndex(r => new { r.CustomerId, r.MerchantId, r.Status });
            b.HasIndex(r => r.RewardId);
            // At most one pending redemption per customer per merchant (doc 05 B9 PENDING_REDEMPTION_EXISTS).
            b.HasIndex(r => new { r.CustomerId, r.MerchantId })
                .IsUnique()
                .HasFilter($"status = '{RedemptionStatuses.Pending}'")
                .HasDatabaseName("ux_redemptions_one_pending_per_customer_merchant");
        });

        base.OnModelCreating(modelBuilder);
    }
}
