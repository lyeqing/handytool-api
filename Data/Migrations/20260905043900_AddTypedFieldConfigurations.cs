using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace handytool_api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedFieldConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$ BEGIN
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Text' AND NOT (k = ANY(ARRAY['minimumLength','maximumLength','placeholder']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Text; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Text' AND "Settings" ? 'minimumLength' AND jsonb_typeof("Settings" -> 'minimumLength') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Text minimumLength type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Text' AND ("Settings" ->> 'minimumLength')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting minimumLength'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Text' AND "Settings" ? 'maximumLength' AND jsonb_typeof("Settings" -> 'maximumLength') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Text maximumLength type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Text' AND ("Settings" ->> 'maximumLength')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting maximumLength'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Text' AND "Settings" ? 'placeholder' AND jsonb_typeof("Settings" -> 'placeholder') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Text placeholder type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'LongText' AND NOT (k = ANY(ARRAY['minimumLength','maximumLength','placeholder','rows']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on LongText; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND "Settings" ? 'minimumLength' AND jsonb_typeof("Settings" -> 'minimumLength') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid LongText minimumLength type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND ("Settings" ->> 'minimumLength')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting minimumLength'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND "Settings" ? 'maximumLength' AND jsonb_typeof("Settings" -> 'maximumLength') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid LongText maximumLength type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND ("Settings" ->> 'maximumLength')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting maximumLength'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND "Settings" ? 'placeholder' AND jsonb_typeof("Settings" -> 'placeholder') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid LongText placeholder type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND "Settings" ? 'rows' AND jsonb_typeof("Settings" -> 'rows') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid LongText rows type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'LongText' AND ("Settings" ->> 'rows')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting rows'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Integer' AND NOT (k = ANY(ARRAY['minimum','maximum','step','placeholder']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Integer; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND "Settings" ? 'minimum' AND jsonb_typeof("Settings" -> 'minimum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Integer minimum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND ("Settings" ->> 'minimum')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting minimum'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND "Settings" ? 'maximum' AND jsonb_typeof("Settings" -> 'maximum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Integer maximum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND ("Settings" ->> 'maximum')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting maximum'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND "Settings" ? 'step' AND jsonb_typeof("Settings" -> 'step') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Integer step type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND ("Settings" ->> 'step')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting step'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Integer' AND "Settings" ? 'placeholder' AND jsonb_typeof("Settings" -> 'placeholder') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Integer placeholder type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Decimal' AND NOT (k = ANY(ARRAY['minimum','maximum','step','placeholder']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Decimal; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Decimal' AND "Settings" ? 'minimum' AND jsonb_typeof("Settings" -> 'minimum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Decimal minimum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Decimal' AND "Settings" ? 'maximum' AND jsonb_typeof("Settings" -> 'maximum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Decimal maximum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Decimal' AND "Settings" ? 'step' AND jsonb_typeof("Settings" -> 'step') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Decimal step type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Decimal' AND "Settings" ? 'placeholder' AND jsonb_typeof("Settings" -> 'placeholder') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Decimal placeholder type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Range' AND NOT (k = ANY(ARRAY['minimum','maximum','step']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Range; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Range' AND "Settings" ? 'minimum' AND jsonb_typeof("Settings" -> 'minimum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Range minimum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Range' AND "Settings" ? 'maximum' AND jsonb_typeof("Settings" -> 'maximum') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Range maximum type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Range' AND "Settings" ? 'step' AND jsonb_typeof("Settings" -> 'step') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid Range step type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Date' AND NOT (k = ANY(ARRAY['minimumDate','maximumDate']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Date; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Date' AND "Settings" ? 'minimumDate' AND jsonb_typeof("Settings" -> 'minimumDate') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Date minimumDate type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Date' AND "Settings" ? 'maximumDate' AND jsonb_typeof("Settings" -> 'maximumDate') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Date maximumDate type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'DateTime' AND NOT (k = ANY(ARRAY['minimumDateTime','maximumDateTime']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on DateTime; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'DateTime' AND "Settings" ? 'minimumDateTime' AND jsonb_typeof("Settings" -> 'minimumDateTime') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid DateTime minimumDateTime type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'DateTime' AND "Settings" ->> 'minimumDateTime' IS NOT NULL AND "Settings" ->> 'minimumDateTime' !~ '(Z|[+-][0-9]{2}:[0-9]{2})$') THEN RAISE EXCEPTION 'DateTime setting minimumDateTime requires an explicit offset'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'DateTime' AND "Settings" ? 'maximumDateTime' AND jsonb_typeof("Settings" -> 'maximumDateTime') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid DateTime maximumDateTime type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'DateTime' AND "Settings" ->> 'maximumDateTime' IS NOT NULL AND "Settings" ->> 'maximumDateTime' !~ '(Z|[+-][0-9]{2}:[0-9]{2})$') THEN RAISE EXCEPTION 'DateTime setting maximumDateTime requires an explicit offset'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Dropdown' AND NOT (k = ANY(ARRAY['placeholder']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Dropdown; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'Dropdown' AND "Settings" ? 'placeholder' AND jsonb_typeof("Settings" -> 'placeholder') NOT IN ('string','null')) THEN RAISE EXCEPTION 'Invalid Dropdown placeholder type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'MultiSelect' AND NOT (k = ANY(ARRAY['minimumItems','maximumItems']::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on MultiSelect; review before migration'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'MultiSelect' AND "Settings" ? 'minimumItems' AND jsonb_typeof("Settings" -> 'minimumItems') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid MultiSelect minimumItems type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'MultiSelect' AND ("Settings" ->> 'minimumItems')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting minimumItems'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'MultiSelect' AND "Settings" ? 'maximumItems' AND jsonb_typeof("Settings" -> 'maximumItems') NOT IN ('number','null')) THEN RAISE EXCEPTION 'Invalid MultiSelect maximumItems type'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" WHERE "FieldType" = 'MultiSelect' AND ("Settings" ->> 'maximumItems')::numeric % 1 <> 0) THEN RAISE EXCEPTION 'Fractional integer setting maximumItems'; END IF;
                IF EXISTS (SELECT 1 FROM "FieldDefinitions" d, LATERAL jsonb_object_keys(d."Settings") k WHERE d."FieldType" = 'Boolean' AND NOT (k = ANY(ARRAY[]::text[]))) THEN RAISE EXCEPTION 'Unmapped settings on Boolean; review before migration'; END IF;
                END $guard$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_FieldDefinitions_Settings_IsObject",
                table: "FieldDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "Placeholder",
                table: "FieldDefinitionTranslations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ObjectDefinitionId",
                table: "FieldDefinitions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.Sql("""ALTER TABLE "FieldDefinitions" ALTER COLUMN "FieldType" TYPE integer USING CASE "FieldType" WHEN 'Text' THEN 1 WHEN 'LongText' THEN 2 WHEN 'Integer' THEN 3 WHEN 'Decimal' THEN 4 WHEN 'Range' THEN 5 WHEN 'Date' THEN 6 WHEN 'DateTime' THEN 8 WHEN 'Dropdown' THEN 9 WHEN 'MultiSelect' THEN 12 WHEN 'Boolean' THEN 13 ELSE NULL END;""");

            migrationBuilder.CreateTable(
                name: "BooleanFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BooleanFields", x => x.FieldDefinitionId);
                    table.ForeignKey(
                        name: "FK_BooleanFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumSelections = table.Column<int>(type: "integer", nullable: true),
                    MaximumSelections = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_ChecklistFields_Settings", "(\"MinimumSelections\" IS NULL OR \"MaximumSelections\" IS NULL OR \"MinimumSelections\" <= \"MaximumSelections\") AND (\"MinimumSelections\" IS NULL OR \"MinimumSelections\" >= 0) AND (\"MaximumSelections\" IS NULL OR \"MaximumSelections\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ChecklistFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumItems = table.Column<int>(type: "integer", nullable: true),
                    MaximumItems = table.Column<int>(type: "integer", nullable: true),
                    ItemDefinitionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_CollectionFields_Settings", "(\"MinimumItems\" IS NULL OR \"MaximumItems\" IS NULL OR \"MinimumItems\" <= \"MaximumItems\") AND (\"MinimumItems\" IS NULL OR \"MinimumItems\" >= 0) AND (\"MaximumItems\" IS NULL OR \"MaximumItems\" >= 0) AND (\"ItemDefinitionId\" <> \"FieldDefinitionId\")");
                    table.ForeignKey(
                        name: "FK_CollectionFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionFields_FieldDefinitions_ItemDefinitionId",
                        column: x => x.ItemDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DateFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaximumDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_DateFields_Settings", "(\"MinimumDate\" IS NULL OR \"MaximumDate\" IS NULL OR \"MinimumDate\" <= \"MaximumDate\")");
                    table.ForeignKey(
                        name: "FK_DateFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DateTimeFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaximumDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateTimeFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_DateTimeFields_Settings", "(\"MinimumDateTime\" IS NULL OR \"MaximumDateTime\" IS NULL OR \"MinimumDateTime\" <= \"MaximumDateTime\")");
                    table.ForeignKey(
                        name: "FK_DateTimeFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecimalFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Minimum = table.Column<decimal>(type: "numeric", nullable: true),
                    Maximum = table.Column<decimal>(type: "numeric", nullable: true),
                    Step = table.Column<decimal>(type: "numeric", nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecimalFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_DecimalFields_Settings", "(\"Minimum\" IS NULL OR \"Maximum\" IS NULL OR \"Minimum\" <= \"Maximum\") AND (\"Step\" IS NULL OR \"Step\" > 0)");
                    table.ForeignKey(
                        name: "FK_DecimalFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DropdownFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropdownFields", x => x.FieldDefinitionId);
                    table.ForeignKey(
                        name: "FK_DropdownFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegerFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Minimum = table.Column<long>(type: "bigint", nullable: true),
                    Maximum = table.Column<long>(type: "bigint", nullable: true),
                    Step = table.Column<long>(type: "bigint", nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegerFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_IntegerFields_Settings", "(\"Minimum\" IS NULL OR \"Maximum\" IS NULL OR \"Minimum\" <= \"Maximum\") AND (\"Step\" IS NULL OR \"Step\" > 0)");
                    table.ForeignKey(
                        name: "FK_IntegerFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LongTextFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumLength = table.Column<int>(type: "integer", nullable: true),
                    MaximumLength = table.Column<int>(type: "integer", nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Rows = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LongTextFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_LongTextFields_Settings", "(\"MinimumLength\" IS NULL OR \"MaximumLength\" IS NULL OR \"MinimumLength\" <= \"MaximumLength\") AND (\"MinimumLength\" IS NULL OR \"MinimumLength\" >= 0) AND (\"MaximumLength\" IS NULL OR \"MaximumLength\" >= 0) AND (\"Rows\" IS NULL OR \"Rows\" > 0)");
                    table.ForeignKey(
                        name: "FK_LongTextFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarkdownFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumLength = table.Column<int>(type: "integer", nullable: true),
                    MaximumLength = table.Column<int>(type: "integer", nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkdownFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_MarkdownFields_Settings", "(\"MinimumLength\" IS NULL OR \"MaximumLength\" IS NULL OR \"MinimumLength\" <= \"MaximumLength\") AND (\"MinimumLength\" IS NULL OR \"MinimumLength\" >= 0) AND (\"MaximumLength\" IS NULL OR \"MaximumLength\" >= 0)");
                    table.ForeignKey(
                        name: "FK_MarkdownFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MultiSelectFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumSelections = table.Column<int>(type: "integer", nullable: true),
                    MaximumSelections = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiSelectFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_MultiSelectFields_Settings", "(\"MinimumSelections\" IS NULL OR \"MaximumSelections\" IS NULL OR \"MinimumSelections\" <= \"MaximumSelections\") AND (\"MinimumSelections\" IS NULL OR \"MinimumSelections\" >= 0) AND (\"MaximumSelections\" IS NULL OR \"MaximumSelections\" >= 0)");
                    table.ForeignKey(
                        name: "FK_MultiSelectFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ObjectFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    ReferencedObjectDefinitionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectFields", x => x.FieldDefinitionId);
                    table.ForeignKey(
                        name: "FK_ObjectFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ObjectFields_ObjectDefinitions_ReferencedObjectDefinitionId",
                        column: x => x.ReferencedObjectDefinitionId,
                        principalTable: "ObjectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadioGroupFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadioGroupFields", x => x.FieldDefinitionId);
                    table.ForeignKey(
                        name: "FK_RadioGroupFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RangeFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Minimum = table.Column<decimal>(type: "numeric", nullable: true),
                    Maximum = table.Column<decimal>(type: "numeric", nullable: true),
                    Step = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RangeFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_RangeFields_Settings", "(\"Minimum\" IS NULL OR \"Maximum\" IS NULL OR \"Minimum\" <= \"Maximum\") AND (\"Step\" IS NULL OR \"Step\" > 0)");
                    table.ForeignKey(
                        name: "FK_RangeFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShortTextFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumLength = table.Column<int>(type: "integer", nullable: true),
                    MaximumLength = table.Column<int>(type: "integer", nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortTextFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_ShortTextFields_Settings", "(\"MinimumLength\" IS NULL OR \"MaximumLength\" IS NULL OR \"MinimumLength\" <= \"MaximumLength\") AND (\"MinimumLength\" IS NULL OR \"MinimumLength\" >= 0) AND (\"MaximumLength\" IS NULL OR \"MaximumLength\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ShortTextFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeFields",
                columns: table => new
                {
                    FieldDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    MinimumTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MaximumTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    StepSeconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeFields", x => x.FieldDefinitionId);
                    table.CheckConstraint("CK_TimeFields_Settings", "(\"MinimumTime\" IS NULL OR \"MaximumTime\" IS NULL OR \"MinimumTime\" <= \"MaximumTime\") AND (\"StepSeconds\" IS NULL OR \"StepSeconds\" > 0)");
                    table.ForeignKey(
                        name: "FK_TimeFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FieldDefinitions_FieldType",
                table: "FieldDefinitions",
                sql: "\"FieldType\" BETWEEN 1 AND 16");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionFields_ItemDefinitionId",
                table: "CollectionFields",
                column: "ItemDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectFields_ReferencedObjectDefinitionId",
                table: "ObjectFields",
                column: "ReferencedObjectDefinitionId");
            // Copy settings before dropping JSON; invalid or unmapped settings abort the transaction.
            migrationBuilder.Sql("""
                INSERT INTO "ShortTextFields" ("FieldDefinitionId", "MinimumLength", "MaximumLength", "Placeholder") SELECT "Id", ("Settings" ->> 'minimumLength')::integer, ("Settings" ->> 'maximumLength')::integer, ("Settings" ->> 'placeholder')::text FROM "FieldDefinitions" WHERE "FieldType" = 1;
                INSERT INTO "LongTextFields" ("FieldDefinitionId", "MinimumLength", "MaximumLength", "Placeholder", "Rows") SELECT "Id", ("Settings" ->> 'minimumLength')::integer, ("Settings" ->> 'maximumLength')::integer, ("Settings" ->> 'placeholder')::text, ("Settings" ->> 'rows')::integer FROM "FieldDefinitions" WHERE "FieldType" = 2;
                INSERT INTO "IntegerFields" ("FieldDefinitionId", "Minimum", "Maximum", "Step", "Placeholder") SELECT "Id", ("Settings" ->> 'minimum')::bigint, ("Settings" ->> 'maximum')::bigint, ("Settings" ->> 'step')::bigint, ("Settings" ->> 'placeholder')::text FROM "FieldDefinitions" WHERE "FieldType" = 3;
                INSERT INTO "DecimalFields" ("FieldDefinitionId", "Minimum", "Maximum", "Step", "Placeholder") SELECT "Id", ("Settings" ->> 'minimum')::numeric, ("Settings" ->> 'maximum')::numeric, ("Settings" ->> 'step')::numeric, ("Settings" ->> 'placeholder')::text FROM "FieldDefinitions" WHERE "FieldType" = 4;
                INSERT INTO "RangeFields" ("FieldDefinitionId", "Minimum", "Maximum", "Step") SELECT "Id", ("Settings" ->> 'minimum')::numeric, ("Settings" ->> 'maximum')::numeric, ("Settings" ->> 'step')::numeric FROM "FieldDefinitions" WHERE "FieldType" = 5;
                INSERT INTO "DateFields" ("FieldDefinitionId", "MinimumDate", "MaximumDate") SELECT "Id", ("Settings" ->> 'minimumDate')::date, ("Settings" ->> 'maximumDate')::date FROM "FieldDefinitions" WHERE "FieldType" = 6;
                INSERT INTO "DateTimeFields" ("FieldDefinitionId", "MinimumDateTime", "MaximumDateTime") SELECT "Id", ("Settings" ->> 'minimumDateTime')::timestamp with time zone, ("Settings" ->> 'maximumDateTime')::timestamp with time zone FROM "FieldDefinitions" WHERE "FieldType" = 8;
                INSERT INTO "DropdownFields" ("FieldDefinitionId", "Placeholder") SELECT "Id", ("Settings" ->> 'placeholder')::text FROM "FieldDefinitions" WHERE "FieldType" = 9;
                INSERT INTO "MultiSelectFields" ("FieldDefinitionId", "MinimumSelections", "MaximumSelections") SELECT "Id", ("Settings" ->> 'minimumItems')::integer, ("Settings" ->> 'maximumItems')::integer FROM "FieldDefinitions" WHERE "FieldType" = 12;
                INSERT INTO "BooleanFields" ("FieldDefinitionId") SELECT "Id" FROM "FieldDefinitions" WHERE "FieldType" = 13;
                """);
            migrationBuilder.DropColumn(name: "Settings", table: "FieldDefinitions");
            migrationBuilder.Sql("""
                CREATE FUNCTION "ValidateTypedFieldConfiguration"() RETURNS trigger LANGUAGE plpgsql AS $fn$
                DECLARE ids bigint[]; target bigint; expected_kind integer; owner_id bigint; config_count integer; matching_count integer;
                BEGIN
                    IF TG_TABLE_NAME = 'FieldDefinitions' THEN
                        IF TG_OP = 'INSERT' THEN ids := ARRAY[NEW."Id"];
                        ELSIF TG_OP = 'DELETE' THEN ids := ARRAY[OLD."Id"];
                        ELSE ids := ARRAY[OLD."Id", NEW."Id"]; END IF;
                    ELSE
                        IF TG_OP = 'INSERT' THEN ids := ARRAY[NEW."FieldDefinitionId"];
                        ELSIF TG_OP = 'DELETE' THEN ids := ARRAY[OLD."FieldDefinitionId"];
                        ELSE ids := ARRAY[OLD."FieldDefinitionId", NEW."FieldDefinitionId"]; END IF;
                    END IF;
                    FOREACH target IN ARRAY ids LOOP
                        SELECT "FieldType", "ObjectDefinitionId" INTO expected_kind, owner_id
                        FROM "FieldDefinitions" WHERE "Id" = target FOR UPDATE;
                        IF NOT FOUND THEN CONTINUE; END IF;
                        SELECT count(*), count(*) FILTER (WHERE c.kind = expected_kind)
                        INTO config_count, matching_count FROM (
                SELECT "FieldDefinitionId", 1 AS kind FROM "ShortTextFields"
                UNION ALL
                SELECT "FieldDefinitionId", 2 AS kind FROM "LongTextFields"
                UNION ALL
                SELECT "FieldDefinitionId", 3 AS kind FROM "IntegerFields"
                UNION ALL
                SELECT "FieldDefinitionId", 4 AS kind FROM "DecimalFields"
                UNION ALL
                SELECT "FieldDefinitionId", 5 AS kind FROM "RangeFields"
                UNION ALL
                SELECT "FieldDefinitionId", 6 AS kind FROM "DateFields"
                UNION ALL
                SELECT "FieldDefinitionId", 7 AS kind FROM "TimeFields"
                UNION ALL
                SELECT "FieldDefinitionId", 8 AS kind FROM "DateTimeFields"
                UNION ALL
                SELECT "FieldDefinitionId", 9 AS kind FROM "DropdownFields"
                UNION ALL
                SELECT "FieldDefinitionId", 10 AS kind FROM "RadioGroupFields"
                UNION ALL
                SELECT "FieldDefinitionId", 11 AS kind FROM "ChecklistFields"
                UNION ALL
                SELECT "FieldDefinitionId", 12 AS kind FROM "MultiSelectFields"
                UNION ALL
                SELECT "FieldDefinitionId", 13 AS kind FROM "BooleanFields"
                UNION ALL
                SELECT "FieldDefinitionId", 14 AS kind FROM "MarkdownFields"
                UNION ALL
                SELECT "FieldDefinitionId", 15 AS kind FROM "ObjectFields"
                UNION ALL
                SELECT "FieldDefinitionId", 16 AS kind FROM "CollectionFields"
                        ) c WHERE c."FieldDefinitionId" = target;
                        IF config_count <> 1 OR matching_count <> 1 THEN
                            RAISE EXCEPTION 'Field % requires exactly one configuration matching type %', target, expected_kind USING ERRCODE = '23514';
                        END IF;
                        IF owner_id IS NOT NULL AND EXISTS (SELECT 1 FROM "CollectionFields" WHERE "ItemDefinitionId" = target) THEN
                            RAISE EXCEPTION 'Collection item definition % must be standalone', target USING ERRCODE = '23514';
                        END IF;
                        IF expected_kind = 16 THEN
                            PERFORM d."Id" FROM "FieldDefinitions" d JOIN "CollectionFields" c ON c."ItemDefinitionId" = d."Id"
                                WHERE c."FieldDefinitionId" = target FOR UPDATE OF d;
                            IF EXISTS (SELECT 1 FROM "CollectionFields" c JOIN "FieldDefinitions" d ON d."Id" = c."ItemDefinitionId"
                                WHERE c."FieldDefinitionId" = target AND d."ObjectDefinitionId" IS NOT NULL) THEN
                                RAISE EXCEPTION 'Collection % must reference a standalone item definition', target USING ERRCODE = '23514';
                            END IF;
                            IF EXISTS (
                                WITH RECURSIVE items(id) AS (
                                    SELECT "ItemDefinitionId" FROM "CollectionFields" WHERE "FieldDefinitionId" = target
                                    UNION SELECT c."ItemDefinitionId" FROM "CollectionFields" c JOIN items i ON c."FieldDefinitionId" = i.id
                                ) SELECT 1 FROM items WHERE id = target
                            ) THEN
                                RAISE EXCEPTION 'Collection definition cycle at %', target USING ERRCODE = '23514';
                            END IF;
                        END IF;
                        IF expected_kind NOT IN (9, 10, 11, 12) AND EXISTS (SELECT 1 FROM "FieldOptions" WHERE "FieldDefinitionId" = target) THEN
                            RAISE EXCEPTION 'Field % does not support options', target USING ERRCODE = '23514';
                        END IF;
                    END LOOP;
                    RETURN NULL;
                END $fn$;
                CREATE CONSTRAINT TRIGGER "CT_FieldDefinitions_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "FieldDefinitions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_FieldOptions_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "FieldOptions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_ShortTextFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "ShortTextFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_LongTextFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "LongTextFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_IntegerFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "IntegerFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_DecimalFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "DecimalFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_RangeFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "RangeFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_DateFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "DateFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_TimeFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "TimeFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_DateTimeFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "DateTimeFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_DropdownFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "DropdownFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_RadioGroupFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "RadioGroupFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_ChecklistFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "ChecklistFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_MultiSelectFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "MultiSelectFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_BooleanFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "BooleanFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_MarkdownFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "MarkdownFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_ObjectFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "ObjectFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                CREATE CONSTRAINT TRIGGER "CT_CollectionFields_TypedConfiguration" AFTER INSERT OR UPDATE OR DELETE ON "CollectionFields" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateTypedFieldConfiguration"();
                UPDATE "FieldDefinitions" SET "FieldType" = "FieldType";
                SET CONSTRAINTS ALL IMMEDIATE;
                SET CONSTRAINTS ALL DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "New field types and reusable collection item definitions require an explicit data-preserving rollback plan.");
        }
    }
}
