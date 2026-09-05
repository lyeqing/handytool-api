using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordCompanyOwnershipAndRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObjectRecords_UserAccounts_UserId",
                table: "ObjectRecords");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ObjectRecords",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectRecords_UserId",
                table: "ObjectRecords",
                newName: "IX_ObjectRecords_CreatedByUserId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ObjectRecords",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ObjectRecords",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldDefaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ObjectRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "ObjectRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_ObjectRecords_CompanyId",
                table: "ObjectRecords",
                column: "CompanyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ObjectRecords_Revision",
                table: "ObjectRecords",
                sql: "\"Revision\" >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectRecords_CompanyAccounts_CompanyId",
                table: "ObjectRecords",
                column: "CompanyId",
                principalTable: "CompanyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectRecords_UserAccounts_CreatedByUserId",
                table: "ObjectRecords",
                column: "CreatedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObjectRecords_CompanyAccounts_CompanyId",
                table: "ObjectRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ObjectRecords_UserAccounts_CreatedByUserId",
                table: "ObjectRecords");

            migrationBuilder.DropIndex(
                name: "IX_ObjectRecords_CompanyId",
                table: "ObjectRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ObjectRecords_Revision",
                table: "ObjectRecords");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ObjectRecords");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ObjectRecords");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "ObjectRecords",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectRecords_CreatedByUserId",
                table: "ObjectRecords",
                newName: "IX_ObjectRecords_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ObjectRecords",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ObjectRecords",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectRecords_UserAccounts_UserId",
                table: "ObjectRecords",
                column: "UserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
