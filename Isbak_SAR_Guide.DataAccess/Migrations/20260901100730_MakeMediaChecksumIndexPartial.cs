using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations;

/// <inheritdoc />
public partial class MakeMediaChecksumIndexPartial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Media_Checksum",
            table: "Media");

        migrationBuilder.CreateIndex(
            name: "IX_Media_Checksum",
            table: "Media",
            column: "Checksum",
            unique: true,
            filter: "\"IsDeleted\" = false");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Media_Checksum",
            table: "Media");

        migrationBuilder.CreateIndex(
            name: "IX_Media_Checksum",
            table: "Media",
            column: "Checksum",
            unique: true);
    }
}
