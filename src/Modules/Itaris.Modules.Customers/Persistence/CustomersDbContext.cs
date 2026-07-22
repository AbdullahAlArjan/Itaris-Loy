using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Persistence;

public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "customers";

    public DbSet<CustomerProfile> Profiles => Set<CustomerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerProfile>(b =>
        {
            b.ToTable("customer_profiles");
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.UserId).HasColumnName("user_id");
            b.Property(p => p.PhoneNumber).HasColumnName("phone_number").HasMaxLength(16);
            b.Property(p => p.FirstName).HasColumnName("first_name").HasMaxLength(50);
            b.Property(p => p.Gender).HasColumnName("gender").HasMaxLength(16);
            b.Property(p => p.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(4);
            b.Property(p => p.BirthDate).HasColumnName("birth_date");
            b.Property(p => p.IsShadow).HasColumnName("is_shadow");
            b.Property(p => p.ClaimedAt).HasColumnName("claimed_at");
            b.HasIndex(p => p.UserId).IsUnique();
            b.HasIndex(p => p.PhoneNumber);
        });

        base.OnModelCreating(modelBuilder);
    }
}
