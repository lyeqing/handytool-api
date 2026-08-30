using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationAndWebsiteTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "ObjectRecords",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectRecords_OwnerId",
                table: "ObjectRecords",
                newName: "IX_ObjectRecords_UserId");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "ObjectDefinitions",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_OwnerId_Name",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_UserId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_OwnerId",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_UserId");

            migrationBuilder.CreateTable(
                name: "TrackingClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstSeenDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordSalt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingSessions_TrackingClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "TrackingClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackingClientUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FirstIdentifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastIdentifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingClientUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingClientUsers_TrackingClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "TrackingClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackingClientUsers_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveSeconds = table.Column<int>(type: "integer", nullable: true),
                    Referrer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEvents", x => x.Id);
                    table.CheckConstraint("CK_AnalyticsEvents_ActiveSeconds_NonNegative", "\"ActiveSeconds\" IS NULL OR \"ActiveSeconds\" >= 0");
                    table.ForeignKey(
                        name: "FK_AnalyticsEvents_TrackingClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "TrackingClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalyticsEvents_TrackingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrackingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_ClientId_Timestamp",
                table: "AnalyticsEvents",
                columns: new[] { "ClientId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_EventType",
                table: "AnalyticsEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_SessionId_Timestamp",
                table: "AnalyticsEvents",
                columns: new[] { "SessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_Timestamp",
                table: "AnalyticsEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_UserId_Timestamp",
                table: "AnalyticsEvents",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingClients_ClientType",
                table: "TrackingClients",
                column: "ClientType");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingClients_LastSeenDate",
                table: "TrackingClients",
                column: "LastSeenDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingClientUsers_ClientId_UserId",
                table: "TrackingClientUsers",
                columns: new[] { "ClientId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingClientUsers_UserId",
                table: "TrackingClientUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingSessions_ClientId_LastActivityAt",
                table: "TrackingSessions",
                columns: new[] { "ClientId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Email",
                table: "UserAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ExpiresDate",
                table: "UserSessions",
                column: "ExpiresDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_TokenHash",
                table: "UserSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedDate",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedDate" });

            // Definitions and records predate authentication: they were owned by whatever X-Owner-Id
            // header happened to be sent. Give each of those ids a placeholder account so the new
            // foreign keys can be added without dropping anyone's data. They are inactive and hold no
            // usable password hash, so none of them can be signed in to - claim the data by moving
            // the rows to a real account.
            migrationBuilder.Sql("""
                INSERT INTO "UserAccounts"
                    ("Id", "Email", "PasswordHash", "PasswordSalt", "DisplayName", "IsActive", "CreatedDate", "ModifiedDate")
                SELECT owners."UserId",
                       'legacy-owner-' || owners."UserId" || '@handytool.invalid',
                       '',
                       '',
                       'Legacy owner ' || owners."UserId",
                       false,
                       now(),
                       now()
                FROM (
                    SELECT "UserId" FROM "ObjectDefinitions"
                    UNION
                    SELECT "UserId" FROM "ObjectRecords"
                ) AS owners
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserAccounts" existing WHERE existing."Id" = owners."UserId"
                );
                """);

            // Those ids were written explicitly, so the identity sequence has never seen them. Without
            // this, the first real registration would try to reuse id 1 and collide.
            migrationBuilder.Sql("""
                SELECT setval(
                    pg_get_serial_sequence('"UserAccounts"', 'Id'),
                    GREATEST(COALESCE((SELECT MAX("Id") FROM "UserAccounts"), 0), 1));
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDefinitions_UserAccounts_UserId",
                table: "ObjectDefinitions",
                column: "UserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectRecords_UserAccounts_UserId",
                table: "ObjectRecords",
                column: "UserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObjectDefinitions_UserAccounts_UserId",
                table: "ObjectDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ObjectRecords_UserAccounts_UserId",
                table: "ObjectRecords");

            migrationBuilder.DropTable(
                name: "AnalyticsEvents");

            migrationBuilder.DropTable(
                name: "TrackingClientUsers");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "TrackingSessions");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "TrackingClients");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ObjectRecords",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectRecords_UserId",
                table: "ObjectRecords",
                newName: "IX_ObjectRecords_OwnerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ObjectDefinitions",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_UserId_Name",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_OwnerId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ObjectDefinitions_UserId",
                table: "ObjectDefinitions",
                newName: "IX_ObjectDefinitions_OwnerId");
        }
    }
}
