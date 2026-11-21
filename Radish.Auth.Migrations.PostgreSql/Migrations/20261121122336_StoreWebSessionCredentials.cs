using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radish.Auth.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class StoreWebSessionCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessTokenExpiresAtUtc",
                table: "WebAuthSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CookieDigest",
                table: "WebAuthSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedTokens",
                table: "WebAuthSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefreshLeaseId",
                table: "WebAuthSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshLeaseUntilUtc",
                table: "WebAuthSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshRetryAtUtc",
                table: "WebAuthSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WebAuthLogins",
                columns: table => new
                {
                    StateDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BrowserDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedRequest = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAuthLogins", x => x.StateDigest);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthSessions_CookieDigest",
                table: "WebAuthSessions",
                column: "CookieDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthLogins_ExpiresAtUtc",
                table: "WebAuthLogins",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebAuthLogins");

            migrationBuilder.DropIndex(
                name: "IX_WebAuthSessions_CookieDigest",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "AccessTokenExpiresAtUtc",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "CookieDigest",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "ProtectedTokens",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "RefreshLeaseId",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "RefreshLeaseUntilUtc",
                table: "WebAuthSessions");

            migrationBuilder.DropColumn(
                name: "RefreshRetryAtUtc",
                table: "WebAuthSessions");
        }
    }
}
