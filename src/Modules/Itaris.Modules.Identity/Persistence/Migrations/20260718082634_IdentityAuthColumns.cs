using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itaris.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAuthColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "identity",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "claims",
                schema: "identity",
                table: "refresh_tokens",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "claims",
                schema: "identity",
                table: "refresh_tokens");
        }
    }
}
