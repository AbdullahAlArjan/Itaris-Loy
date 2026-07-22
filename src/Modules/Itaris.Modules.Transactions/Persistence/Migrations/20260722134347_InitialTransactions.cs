using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itaris.Modules.Transactions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "transactions");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "transactions",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "points_ledger_entries",
                schema: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    points_delta = table.Column<long>(type: "bigint", nullable: false),
                    stamps_delta = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    source_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_points_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    points_clawback = table.Column<long>(type: "bigint", nullable: false),
                    stamps_clawback = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_items",
                schema: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refunded_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_points_ledger_entries_membership_id_id",
                schema: "transactions",
                table: "points_ledger_entries",
                columns: new[] { "membership_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_refunds_transaction_id",
                schema: "transactions",
                table: "refunds",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_transaction_id",
                schema: "transactions",
                table: "transaction_items",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_membership_id",
                schema: "transactions",
                table: "transactions",
                column: "membership_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_membership_id_amount_minor_recorded_at",
                schema: "transactions",
                table: "transactions",
                columns: new[] { "membership_id", "amount_minor", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_merchant_id_recorded_at",
                schema: "transactions",
                table: "transactions",
                columns: new[] { "merchant_id", "recorded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "transactions");

            migrationBuilder.DropTable(
                name: "points_ledger_entries",
                schema: "transactions");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "transactions");

            migrationBuilder.DropTable(
                name: "transaction_items",
                schema: "transactions");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "transactions");
        }
    }
}
