using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Merchants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Persistence;

public sealed class MerchantsDbContext(DbContextOptions<MerchantsDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "merchants";

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
    public DbSet<StaffInvite> StaffInvites => Set<StaffInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Merchant>(b =>
        {
            b.ToTable("merchants");
            b.Property(m => m.Id).HasColumnName("id");
            b.Property(m => m.Code).HasColumnName("code").HasMaxLength(32);
            b.Property(m => m.NameAr).HasColumnName("name_ar").HasMaxLength(200);
            b.Property(m => m.NameEn).HasColumnName("name_en").HasMaxLength(200);
            b.Property(m => m.Category).HasColumnName("category").HasMaxLength(64);
            b.Property(m => m.DescriptionAr).HasColumnName("description_ar");
            b.Property(m => m.DescriptionEn).HasColumnName("description_en");
            b.Property(m => m.LogoUrl).HasColumnName("logo_url");
            b.Property(m => m.Status).HasColumnName("status").HasMaxLength(16);
            b.Property(m => m.SettingsJson).HasColumnName("settings").HasColumnType("jsonb");
            b.HasIndex(m => m.Code).IsUnique();
        });

        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("branches");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.MerchantId).HasColumnName("merchant_id");
            b.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(200);
            b.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(200);
            b.Property(x => x.AreaAr).HasColumnName("area_ar").HasMaxLength(120);
            b.Property(x => x.AreaEn).HasColumnName("area_en").HasMaxLength(120);
            b.Property(x => x.AddressAr).HasColumnName("address_ar");
            b.Property(x => x.AddressEn).HasColumnName("address_en");
            b.Property(x => x.Latitude).HasColumnName("latitude");
            b.Property(x => x.Longitude).HasColumnName("longitude");
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
            b.Property(x => x.IsActive).HasColumnName("is_active");
            b.HasIndex(x => x.MerchantId);
        });

        modelBuilder.Entity<StaffMember>(b =>
        {
            b.ToTable("staff_members");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.MerchantId).HasColumnName("merchant_id");
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            b.Property(x => x.PhoneOrEmail).HasColumnName("phone_or_email").HasMaxLength(256);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            b.Property(x => x.RefundLimitMinor).HasColumnName("refund_limit_minor");
            b.Property(x => x.PinHash).HasColumnName("pin_hash");
            b.Property(x => x.FailedPinAttempts).HasColumnName("failed_pin_attempts");
            b.Property(x => x.LockedUntil).HasColumnName("locked_until");
            b.HasIndex(x => x.MerchantId);
            b.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("roles");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.MerchantId).HasColumnName("merchant_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(64);
            b.Property(x => x.IsSystem).HasColumnName("is_system");
            b.HasIndex(x => new { x.MerchantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("permissions");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);
            b.Property(x => x.Description).HasColumnName("description");
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions");
            b.Property(x => x.RoleId).HasColumnName("role_id");
            b.Property(x => x.PermissionId).HasColumnName("permission_id");
            b.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<StaffRole>(b =>
        {
            b.ToTable("staff_roles");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.StaffMemberId).HasColumnName("staff_member_id");
            b.Property(x => x.RoleId).HasColumnName("role_id");
            b.Property(x => x.BranchId).HasColumnName("branch_id");
            b.HasIndex(x => x.StaffMemberId);
        });

        modelBuilder.Entity<StaffInvite>(b =>
        {
            b.ToTable("staff_invites");
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.MerchantId).HasColumnName("merchant_id");
            b.Property(x => x.StaffMemberId).HasColumnName("staff_member_id");
            b.Property(x => x.TokenHash).HasColumnName("token_hash");
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            b.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
            b.HasIndex(x => x.TokenHash).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
