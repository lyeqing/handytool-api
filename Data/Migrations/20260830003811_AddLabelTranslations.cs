using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "DescriptionTranslations",
                table: "ObjectDefinitions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "NameTranslations",
                table: "ObjectDefinitions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "LabelTranslations",
                table: "FieldOptions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "DescriptionTranslations",
                table: "FieldDefinitions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "NameTranslations",
                table: "FieldDefinitions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_DescriptionTranslations_IsObject",
                table: "ObjectDefinitions",
                sql: "jsonb_typeof(\"DescriptionTranslations\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectDefinitions_NameTranslations_IsObject",
                table: "ObjectDefinitions",
                sql: "jsonb_typeof(\"NameTranslations\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldOptions_LabelTranslations_IsObject",
                table: "FieldOptions",
                sql: "jsonb_typeof(\"LabelTranslations\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldDefinitions_DescriptionTranslations_IsObject",
                table: "FieldDefinitions",
                sql: "jsonb_typeof(\"DescriptionTranslations\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldDefinitions_NameTranslations_IsObject",
                table: "FieldDefinitions",
                sql: "jsonb_typeof(\"NameTranslations\") = 'object'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
