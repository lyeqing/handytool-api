using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguagePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "UserAccounts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AnalyticsEvents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_Language",
                table: "AnalyticsEvents",
                column: "Language");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalyticsEvents_Language",
                table: "AnalyticsEvents");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AnalyticsEvents");
        }
    }
}
