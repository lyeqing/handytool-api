using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthThrottling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthThrottles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    FirstFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthThrottles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthThrottles_LastFailedAt",
                table: "AuthThrottles",
                column: "LastFailedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuthThrottles_Scope_KeyHash",
                table: "AuthThrottles",
                columns: new[] { "Scope", "KeyHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthThrottles");
        }
    }
}
