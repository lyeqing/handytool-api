using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "UserAccounts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "CompanyAccounts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "CompanyAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "CompanyAccounts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "CompanyAccounts");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "CompanyAccounts");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "CompanyAccounts");
        }
    }
}
