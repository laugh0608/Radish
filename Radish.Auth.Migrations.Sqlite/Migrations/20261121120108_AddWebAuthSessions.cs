using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radish.Auth.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAuthSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebAuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AuthorizationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActiveAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAuthSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthSessions_AuthorizationId",
                table: "WebAuthSessions",
                column: "AuthorizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebAuthSessions");
        }
    }
}
