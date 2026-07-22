using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itaris.Modules.Customers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_phone_number",
                schema: "customers",
                table: "customer_profiles",
                column: "phone_number");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_user_id",
                schema: "customers",
                table: "customer_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_profiles",
                schema: "customers");
        }
    }
}
