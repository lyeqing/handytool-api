using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncRecordAndTrialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ObjectRecords_CompanyId",
                table: "ObjectRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectDefinitions_CompanyVisibility",
                table: "ObjectDefinitions");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedByUserId",
                table: "ObjectRecords",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "AnonymousDeviceHash",
                table: "ObjectRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "ObjectRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TrialUsage",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialUsage", x => x.Key);
                    table.CheckConstraint("CK_TrialUsage_Count", "\"CreatedCount\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectRecords_AnonymousDeviceHash",
                table: "ObjectRecords",
                column: "AnonymousDeviceHash");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectRecords_CompanyId_CreatedDate",
                table: "ObjectRecords",
                columns: new[] { "CompanyId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectRecords_CreatedByUserId_CreatedDate",
                table: "ObjectRecords",
                columns: new[] { "CreatedByUserId", "CreatedDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectRecords_Creator",
                table: "ObjectRecords",
                sql: "(\"CreatedByUserId\" IS NOT NULL AND \"AnonymousDeviceHash\" IS NULL) OR (\"CreatedByUserId\" IS NULL AND \"AnonymousDeviceHash\" IS NOT NULL AND \"CompanyId\" IS NULL AND \"Visibility\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectRecords_Visibility",
                table: "ObjectRecords",
                sql: " \"Visibility\" IN (0,1) AND (\"Visibility\" = 0 OR \"CompanyId\" IS NOT NULL) ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_CompanyVisibility",
                table: "ObjectDefinitions",
                sql: "\"Visibility\" <> 1 OR \"CompanyId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrialUsage");

            migrationBuilder.DropIndex(
                name: "IX_ObjectRecords_AnonymousDeviceHash",
                table: "ObjectRecords");

            migrationBuilder.DropIndex(
                name: "IX_ObjectRecords_CompanyId_CreatedDate",
                table: "ObjectRecords");

            migrationBuilder.DropIndex(
                name: "IX_ObjectRecords_CreatedByUserId_CreatedDate",
                table: "ObjectRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectRecords_Creator",
                table: "ObjectRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectRecords_Visibility",
                table: "ObjectRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectDefinitions_CompanyVisibility",
                table: "ObjectDefinitions");

            migrationBuilder.DropColumn(
                name: "AnonymousDeviceHash",
                table: "ObjectRecords");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "ObjectRecords");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedByUserId",
                table: "ObjectRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObjectRecords_CompanyId",
                table: "ObjectRecords",
                column: "CompanyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_CompanyVisibility",
                table: "ObjectDefinitions",
                sql: "(\"Visibility\" = 1 AND \"CompanyId\" IS NOT NULL) OR (\"Visibility\" IN (0, 2) AND \"CompanyId\" IS NULL)");
        }
    }
}
