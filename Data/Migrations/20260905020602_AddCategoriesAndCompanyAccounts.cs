using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndCompanyAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountTypeCode",
                table: "UserAccounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                defaultValue: "free");

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "UserAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRole",
                table: "UserAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "UserAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MasterCategoryId",
                table: "ObjectDefinitions",
                type: "bigint",
                nullable: false,
                defaultValue: -1L);

            migrationBuilder.AddColumn<long>(
                name: "SubcategoryId",
                table: "ObjectDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "MasterCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeatLimit = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    AccountTypeCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "free"),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyAccounts", x => x.Id);
                    table.CheckConstraint("CK_CompanyAccounts_SeatLimit", "\"SeatLimit\" >= 0");
                    table.ForeignKey(
                        name: "FK_CompanyAccounts_AccountTypes_AccountTypeCode",
                        column: x => x.AccountTypeCode,
                        principalTable: "AccountTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subcategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasterCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategories", x => x.Id);
                    table.UniqueConstraint("AK_Subcategories_Id_MasterCategoryId", x => new { x.Id, x.MasterCategoryId });
                    table.ForeignKey(
                        name: "FK_Subcategories_MasterCategories_MasterCategoryId",
                        column: x => x.MasterCategoryId,
                        principalTable: "MasterCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "Code", "Name" },
                values: new object[,]
                {
                    { "free", "Free" },
                    { "paid_level_1", "Paid Level 1" },
                    { "paid_level_2", "Paid Level 2" }
                });

            migrationBuilder.InsertData(
                table: "MasterCategories",
                columns: new[] { "Id", "CreatedDate", "Description", "IsActive", "ModifiedDate", "Name" },
                values: new object[] { -1L, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Default category for definitions awaiting classification.", true, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Uncategorized" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_AccountTypeCode",
                table: "UserAccounts",
                column: "AccountTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_CompanyId",
                table: "UserAccounts",
                column: "CompanyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_CompanyMembership",
                table: "UserAccounts",
                sql: "(\"CompanyId\" IS NULL AND \"CompanyRole\" IS NULL AND \"AccountTypeCode\" IS NOT NULL) OR (\"CompanyId\" IS NOT NULL AND \"CompanyRole\" IS NOT NULL AND \"CompanyRole\" IN ('Admin', 'Member') AND \"AccountTypeCode\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDefinitions_MasterCategoryId",
                table: "ObjectDefinitions",
                column: "MasterCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDefinitions_SubcategoryId_MasterCategoryId",
                table: "ObjectDefinitions",
                columns: new[] { "SubcategoryId", "MasterCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAccounts_AccountTypeCode",
                table: "CompanyAccounts",
                column: "AccountTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_MasterCategories_Name",
                table: "MasterCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcategories_MasterCategoryId_Name",
                table: "Subcategories",
                columns: new[] { "MasterCategoryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDefinitions_MasterCategories_MasterCategoryId",
                table: "ObjectDefinitions",
                column: "MasterCategoryId",
                principalTable: "MasterCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDefinitions_Subcategories_SubcategoryId_MasterCategor~",
                table: "ObjectDefinitions",
                columns: new[] { "SubcategoryId", "MasterCategoryId" },
                principalTable: "Subcategories",
                principalColumns: new[] { "Id", "MasterCategoryId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_AccountTypes_AccountTypeCode",
                table: "UserAccounts",
                column: "AccountTypeCode",
                principalTable: "AccountTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_CompanyAccounts_CompanyId",
                table: "UserAccounts",
                column: "CompanyId",
                principalTable: "CompanyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObjectDefinitions_MasterCategories_MasterCategoryId",
                table: "ObjectDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ObjectDefinitions_Subcategories_SubcategoryId_MasterCategor~",
                table: "ObjectDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_AccountTypes_AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_CompanyAccounts_CompanyId",
                table: "UserAccounts");

            migrationBuilder.DropTable(
                name: "CompanyAccounts");

            migrationBuilder.DropTable(
                name: "Subcategories");

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropTable(
                name: "MasterCategories");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_CompanyId",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_CompanyMembership",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ObjectDefinitions_MasterCategoryId",
                table: "ObjectDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ObjectDefinitions_SubcategoryId_MasterCategoryId",
                table: "ObjectDefinitions");

            migrationBuilder.DropColumn(
                name: "AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyRole",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "MasterCategoryId",
                table: "ObjectDefinitions");

            migrationBuilder.DropColumn(
                name: "SubcategoryId",
                table: "ObjectDefinitions");
        }
    }
}
