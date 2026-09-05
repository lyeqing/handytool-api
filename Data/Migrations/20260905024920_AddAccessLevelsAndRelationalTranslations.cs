using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessLevelsAndRelationalTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail before changing anything if a custom plan needs an explicit migration decision.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountTypes" WHERE "Code" NOT IN ('free', 'paid_level_1', 'paid_level_2'))
                        OR (SELECT count(*) FROM "AccountTypes") <> 3 THEN
                        RAISE EXCEPTION 'Expected Free and two paid plans; map custom plans before applying this migration.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "UserAccounts" WHERE "CompanyRole" NOT IN ('Member', 'Admin', 'Owner')) THEN
                        RAISE EXCEPTION 'Unknown company role; migration cannot infer its permissions.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyAccounts_AccountTypes_AccountTypeCode",
                table: "CompanyAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ObjectDefinitions_UserAccounts_UserId",
                table: "ObjectDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_AccountTypes_AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserAccounts_CompanyMembership",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectDefinitions_DescriptionTranslations_IsObject",
                table: "ObjectDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectDefinitions_NameTranslations_IsObject",
                table: "ObjectDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldOptions_LabelTranslations_IsObject",
                table: "FieldOptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldDefinitions_DescriptionTranslations_IsObject",
                table: "FieldDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldDefinitions_NameTranslations_IsObject",
                table: "FieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CompanyAccounts_AccountTypeCode",
                table: "CompanyAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AccountTypes",
                table: "AccountTypes");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ObjectDefinitions",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_UserId_Name",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_CreatedByUserId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_UserId",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_CreatedByUserId");

            migrationBuilder.Sql("""
                ALTER TABLE "UserAccounts" ALTER COLUMN "CompanyRole" TYPE integer
                USING CASE "CompanyRole"
                    WHEN 'Member' THEN 0
                    WHEN 'Admin' THEN 1
                    WHEN 'Owner' THEN 2
                    ELSE NULL END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "AccountTypeId",
                table: "UserAccounts",
                type: "integer",
                nullable: true,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ObjectDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredAccessLevel",
                table: "ObjectDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "ObjectDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccountTypeId",
                table: "CompanyAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "AccountTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:IdentitySequenceOptions", "'4', '1', '', '', 'False', '1'")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "AccountTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "AccountTypes"
                SET "Id" = CASE "Code" WHEN 'free' THEN 1 WHEN 'paid_level_1' THEN 2 WHEN 'paid_level_2' THEN 3 END,
                    "AccessLevel" = CASE "Code" WHEN 'free' THEN 1 WHEN 'paid_level_1' THEN 2 WHEN 'paid_level_2' THEN 3 END;

                UPDATE "UserAccounts" AS u SET "AccountTypeId" = a."Id"
                FROM "AccountTypes" AS a WHERE u."AccountTypeCode" = a."Code";
                UPDATE "UserAccounts" SET "AccountTypeId" = NULL WHERE "CompanyId" IS NOT NULL;
                UPDATE "CompanyAccounts" AS c SET "AccountTypeId" = a."Id"
                FROM "AccountTypes" AS a WHERE c."AccountTypeCode" = a."Code";

                UPDATE "AccountTypes" SET "Code" = 'light', "Name" = 'Light' WHERE "Id" = 2;
                UPDATE "AccountTypes" SET "Code" = 'full', "Name" = 'Full' WHERE "Id" = 3;
                SELECT setval(pg_get_serial_sequence('"AccountTypes"', 'Id'), 4, false);
                """);
            migrationBuilder.AddPrimaryKey(
                name: "PK_AccountTypes",
                table: "AccountTypes",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FieldDefinitionTranslations",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDefinitionTranslations", x => new { x.FieldDefinitionId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_FieldDefinitionTranslations_FieldDefinitions_FieldDefinitio~",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldOptionTranslations",
                columns: table => new
                {
                    FieldOptionId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldOptionTranslations", x => new { x.FieldOptionId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_FieldOptionTranslations_FieldOptions_FieldOptionId",
                        column: x => x.FieldOptionId,
                        principalTable: "FieldOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MasterCategoryTranslations",
                columns: table => new
                {
                    MasterCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterCategoryTranslations", x => new { x.MasterCategoryId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_MasterCategoryTranslations_MasterCategories_MasterCategoryId",
                        column: x => x.MasterCategoryId,
                        principalTable: "MasterCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ObjectDefinitionTranslations",
                columns: table => new
                {
                    ObjectDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectDefinitionTranslations", x => new { x.ObjectDefinitionId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_ObjectDefinitionTranslations_ObjectDefinitions_ObjectDefini~",
                        column: x => x.ObjectDefinitionId,
                        principalTable: "ObjectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubcategoryTranslations",
                columns: table => new
                {
                    SubcategoryId = table.Column<long>(type: "bigint", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcategoryTranslations", x => new { x.SubcategoryId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_SubcategoryTranslations_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy all languages, including translations that exist only for a description.
            // Unexpected non-string JSON is rejected instead of silently losing it.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ObjectDefinitions" d, LATERAL (SELECT value FROM jsonb_each(d."NameTranslations") UNION ALL SELECT value FROM jsonb_each(d."DescriptionTranslations")) j WHERE jsonb_typeof(j.value) <> 'string'
                        UNION ALL
                        SELECT 1 FROM "FieldDefinitions" d, LATERAL (SELECT value FROM jsonb_each(d."NameTranslations") UNION ALL SELECT value FROM jsonb_each(d."DescriptionTranslations")) j WHERE jsonb_typeof(j.value) <> 'string'
                        UNION ALL
                        SELECT 1 FROM "FieldOptions" d, LATERAL jsonb_each(d."LabelTranslations") j WHERE jsonb_typeof(j.value) <> 'string'
                    ) THEN
                        RAISE EXCEPTION 'Non-string translation found; correct it before migration.';
                    END IF;
                END $guard$;

                INSERT INTO "ObjectDefinitionTranslations" ("ObjectDefinitionId", "LanguageCode", "Name", "Description")
                SELECT d."Id", lang.code, d."NameTranslations" ->> lang.code, d."DescriptionTranslations" ->> lang.code
                FROM "ObjectDefinitions" d
                CROSS JOIN LATERAL (
                    SELECT jsonb_object_keys(d."NameTranslations") AS code
                    UNION SELECT jsonb_object_keys(d."DescriptionTranslations")
                ) lang;

                INSERT INTO "FieldDefinitionTranslations" ("FieldDefinitionId", "LanguageCode", "Name", "Description")
                SELECT d."Id", lang.code, d."NameTranslations" ->> lang.code, d."DescriptionTranslations" ->> lang.code
                FROM "FieldDefinitions" d
                CROSS JOIN LATERAL (
                    SELECT jsonb_object_keys(d."NameTranslations") AS code
                    UNION SELECT jsonb_object_keys(d."DescriptionTranslations")
                ) lang;

                INSERT INTO "FieldOptionTranslations" ("FieldOptionId", "LanguageCode", "Label")
                SELECT d."Id", j.key, j.value
                FROM "FieldOptions" d CROSS JOIN LATERAL jsonb_each_text(d."LabelTranslations") j;
                """);
            migrationBuilder.DropColumn(
                name: "AccountTypeCode",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "DescriptionTranslations",
                table: "ObjectDefinitions");

            migrationBuilder.DropColumn(
                name: "NameTranslations",
                table: "ObjectDefinitions");

            migrationBuilder.DropColumn(
                name: "LabelTranslations",
                table: "FieldOptions");

            migrationBuilder.DropColumn(
                name: "DescriptionTranslations",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "NameTranslations",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "AccountTypeCode",
                table: "CompanyAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_AccountTypeId",
                table: "UserAccounts",
                column: "AccountTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserAccounts_CompanyMembership",
                table: "UserAccounts",
                sql: "(\"CompanyId\" IS NULL AND \"CompanyRole\" IS NULL AND \"AccountTypeId\" IS NOT NULL) OR (\"CompanyId\" IS NOT NULL AND \"CompanyRole\" IS NOT NULL AND \"CompanyRole\" IN (0, 1, 2) AND \"AccountTypeId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDefinitions_CompanyId",
                table: "ObjectDefinitions",
                column: "CompanyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_CompanyVisibility",
                table: "ObjectDefinitions",
                sql: "(\"Visibility\" = 1 AND \"CompanyId\" IS NOT NULL) OR (\"Visibility\" IN (0, 2) AND \"CompanyId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_RequiredAccessLevel",
                table: "ObjectDefinitions",
                sql: "\"RequiredAccessLevel\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_Visibility",
                table: "ObjectDefinitions",
                sql: "\"Visibility\" IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAccounts_AccountTypeId",
                table: "CompanyAccounts",
                column: "AccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_Code",
                table: "AccountTypes",
                column: "Code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountTypes_AccessLevel",
                table: "AccountTypes",
                sql: "\"AccessLevel\" BETWEEN 1 AND 3");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyAccounts_AccountTypes_AccountTypeId",
                table: "CompanyAccounts",
                column: "AccountTypeId",
                principalTable: "AccountTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDefinitions_CompanyAccounts_CompanyId",
                table: "ObjectDefinitions",
                column: "CompanyId",
                principalTable: "CompanyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDefinitions_UserAccounts_CreatedByUserId",
                table: "ObjectDefinitions",
                column: "CreatedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_AccountTypes_AccountTypeId",
                table: "UserAccounts",
                column: "AccountTypeId",
                principalTable: "AccountTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Old tables cannot represent company Owners, visibility, access levels, or category translations.
            // Refuse an automatic rollback that would discard those decisions and translated content.
            throw new NotSupportedException(
                "This migration requires an explicit data-preserving rollback plan; automatic downgrade is disabled.");
        }
    }
}
