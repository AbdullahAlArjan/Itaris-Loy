using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itaris.Modules.Rewards.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rewards");

            migrationBuilder.CreateTable(
                name: "redemptions",
                schema: "rewards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    points_held = table.Column<long>(type: "bigint", nullable: false),
                    stamp_card_consumed = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redemptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rewards",
                schema: "rewards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    cost_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    points_cost = table.Column<long>(type: "bigint", nullable: true),
                    stock_remaining = table.Column<long>(type: "bigint", nullable: true),
                    per_customer_limit = table.Column<int>(type: "integer", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rewards", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_code",
                schema: "rewards",
                table: "redemptions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_customer_id_merchant_id_status",
                schema: "rewards",
                table: "redemptions",
                columns: new[] { "customer_id", "merchant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_reward_id",
                schema: "rewards",
                table: "redemptions",
                column: "reward_id");

            migrationBuilder.CreateIndex(
                name: "ux_redemptions_one_pending_per_customer_merchant",
                schema: "rewards",
                table: "redemptions",
                columns: new[] { "customer_id", "merchant_id" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_rewards_merchant_id",
                schema: "rewards",
                table: "rewards",
                column: "merchant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "redemptions",
                schema: "rewards");

            migrationBuilder.DropTable(
                name: "rewards",
                schema: "rewards");
        }
    }
}
