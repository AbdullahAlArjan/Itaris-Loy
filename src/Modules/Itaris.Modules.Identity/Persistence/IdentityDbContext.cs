using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.Id).HasColumnName("id");
            b.Property(u => u.UserType).HasColumnName("user_type").HasMaxLength(32);
            b.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(16);
            b.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
            b.Property(u => u.PasswordHash).HasColumnName("password_hash");
            b.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("phone_number IS NOT NULL");
        });

        modelBuilder.Entity<OtpChallenge>(b =>
        {
            b.ToTable("otp_challenges");
            b.Property(c => c.Id).HasColumnName("id");
            b.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(16);
            b.Property(c => c.CodeHash).HasColumnName("code_hash");
            b.Property(c => c.Purpose).HasColumnName("purpose").HasMaxLength(32);
            b.Property(c => c.Attempts).HasColumnName("attempts");
            b.Property(c => c.ExpiresAt).HasColumnName("expires_at");
            b.Property(c => c.ConsumedAt).HasColumnName("consumed_at");
            b.HasIndex(c => new { c.PhoneNumber, c.ExpiresAt });
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.UserId).HasColumnName("user_id");
            b.Property(t => t.DeviceId).HasColumnName("device_id");
            b.Property(t => t.TokenHash).HasColumnName("token_hash");
            b.Property(t => t.FamilyId).HasColumnName("family_id");
            b.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            b.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
            b.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.FamilyId);
        });

        modelBuilder.Entity<Device>(b =>
        {
            b.ToTable("devices");
            b.Property(d => d.Id).HasColumnName("id");
            b.Property(d => d.UserId).HasColumnName("user_id");
            b.Property(d => d.Platform).HasColumnName("platform").HasMaxLength(32);
            b.Property(d => d.Model).HasColumnName("model").HasMaxLength(128);
            b.Property(d => d.FcmToken).HasColumnName("fcm_token");
            b.HasIndex(d => d.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
