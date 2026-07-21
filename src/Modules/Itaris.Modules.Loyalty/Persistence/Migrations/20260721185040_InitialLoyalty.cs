using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itaris.Modules.Loyalty.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "loyalty");

            migrationBuilder.CreateTable(
                name: "customer_memberships",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points_balance = table.Column<long>(type: "bigint", nullable: false),
                    stamps_filled = table.Column<int>(type: "integer", nullable: false),
                    stamp_card_cycle = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    join_source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_programs",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_rules",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_memberships_customer_id_merchant_id",
                schema: "loyalty",
                table: "customer_memberships",
                columns: new[] { "customer_id", "merchant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loyalty_programs_one_active_per_merchant",
                schema: "loyalty",
                table: "loyalty_programs",
                column: "merchant_id",
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_rules_program_id_version",
                schema: "loyalty",
                table: "loyalty_rules",
                columns: new[] { "program_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_memberships",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "loyalty_programs",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "loyalty_rules",
                schema: "loyalty");
        }
    }
}
