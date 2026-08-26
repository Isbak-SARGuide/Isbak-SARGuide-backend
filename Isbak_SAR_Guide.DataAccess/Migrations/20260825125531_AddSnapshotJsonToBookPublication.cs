using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations;

/// <inheritdoc />
public partial class AddSnapshotJsonToBookPublication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SnapshotJson",
            table: "BookPublications",
            type: "json",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SnapshotJson",
            table: "BookPublications");
    }
}
