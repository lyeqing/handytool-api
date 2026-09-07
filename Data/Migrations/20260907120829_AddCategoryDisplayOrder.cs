using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Subcategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "MasterCategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Subcategories");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "MasterCategories");
        }
    }
}
