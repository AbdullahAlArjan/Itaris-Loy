using Itaris.Infrastructure.Persistence;
using Itaris.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Persistence;

public sealed class TransactionsDbContext(DbContextOptions<TransactionsDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "transactions";

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<PointsLedgerEntry> Ledger => Set<PointsLedgerEntry>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(b =>
        {
            b.ToTable("transactions");
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.MerchantId).HasColumnName("merchant_id");
            b.Property(t => t.BranchId).HasColumnName("branch_id");
            b.Property(t => t.MembershipId).HasColumnName("membership_id");
            b.Property(t => t.StaffMemberId).HasColumnName("staff_member_id");
            b.Property(t => t.AmountMinor).HasColumnName("amount_minor");
            b.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(3);
            b.Property(t => t.Note).HasColumnName("note");
            b.Property(t => t.Status).HasColumnName("status").HasMaxLength(24);
            b.Property(t => t.OccurredAt).HasColumnName("occurred_at");
            b.Property(t => t.RecordedAt).HasColumnName("recorded_at");
            b.Property(t => t.Source).HasColumnName("source").HasMaxLength(16);
            b.Property(t => t.RuleId).HasColumnName("rule_id");
            b.Property(t => t.RefundedAmountMinor).HasColumnName("refunded_amount_minor");
            b.HasIndex(t => new { t.MerchantId, t.RecordedAt });
            b.HasIndex(t => t.MembershipId);
            // Duplicate detection probe: same membership + amount in a short window.
            b.HasIndex(t => new { t.MembershipId, t.AmountMinor, t.RecordedAt });
        });

        modelBuilder.Entity<TransactionItem>(b =>
        {
            b.ToTable("transaction_items");
            b.Property(i => i.Id).HasColumnName("id");
            b.Property(i => i.TransactionId).HasColumnName("transaction_id");
            b.Property(i => i.Name).HasColumnName("name").HasMaxLength(200);
            b.Property(i => i.Quantity).HasColumnName("quantity");
            b.Property(i => i.UnitAmountMinor).HasColumnName("unit_amount_minor");
            b.HasIndex(i => i.TransactionId);
        });

        modelBuilder.Entity<PointsLedgerEntry>(b =>
        {
            b.ToTable("points_ledger_entries");
            b.Property(e => e.Id).HasColumnName("id");
            b.Property(e => e.MembershipId).HasColumnName("membership_id");
            b.Property(e => e.EntryType).HasColumnName("entry_type").HasMaxLength(24);
            b.Property(e => e.PointsDelta).HasColumnName("points_delta");
            b.Property(e => e.StampsDelta).HasColumnName("stamps_delta");
            b.Property(e => e.BalanceAfter).HasColumnName("balance_after");
            b.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(24);
            b.Property(e => e.SourceId).HasColumnName("source_id");
            b.Property(e => e.Reason).HasColumnName("reason");
            b.Property(e => e.CreatedBy).HasColumnName("created_by");
            b.HasIndex(e => new { e.MembershipId, e.Id }); // v7 id = chronological per membership
        });

        modelBuilder.Entity<Refund>(b =>
        {
            b.ToTable("refunds");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.TransactionId).HasColumnName("transaction_id");
            b.Property(r => r.Type).HasColumnName("type").HasMaxLength(8);
            b.Property(r => r.AmountMinor).HasColumnName("amount_minor");
            b.Property(r => r.PointsClawback).HasColumnName("points_clawback");
            b.Property(r => r.StampsClawback).HasColumnName("stamps_clawback");
            b.Property(r => r.Reason).HasColumnName("reason");
            b.Property(r => r.RequestedBy).HasColumnName("requested_by");
            b.Property(r => r.ApprovedBy).HasColumnName("approved_by");
            b.HasIndex(r => r.TransactionId);
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_records");
            b.HasKey(r => r.Key);
            b.Property(r => r.Key).HasColumnName("key");
            b.Property(r => r.RequestHash).HasColumnName("request_hash");
            b.Property(r => r.ResponseStatus).HasColumnName("response_status");
            b.Property(r => r.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
            b.Property(r => r.LockedAt).HasColumnName("locked_at");
            b.Property(r => r.ExpiresAt).HasColumnName("expires_at");
        });

        base.OnModelCreating(modelBuilder);
    }
}
