using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations;

/// <inheritdoc />
public partial class AddContentVariantGrouping : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "VariantGroupKey",
            table: "Contents",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VariantLabel",
            table: "Contents",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Contents_ModuleId_VariantGroupKey",
            table: "Contents",
            columns: new[] { "ModuleId", "VariantGroupKey" },
            filter: "\"VariantGroupKey\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Contents_ModuleId_VariantGroupKey",
            table: "Contents");

        migrationBuilder.DropColumn(
            name: "VariantGroupKey",
            table: "Contents");

        migrationBuilder.DropColumn(
            name: "VariantLabel",
            table: "Contents");
    }
}
