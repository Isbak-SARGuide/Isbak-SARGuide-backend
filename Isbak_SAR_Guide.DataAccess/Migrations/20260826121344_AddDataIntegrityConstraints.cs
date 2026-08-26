using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations;

/// <inheritdoc />
public partial class AddDataIntegrityConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Modules_BookId_DisplayOrder",
            table: "Modules");

        migrationBuilder.DropIndex(
            name: "IX_Media_Checksum",
            table: "Media");

        migrationBuilder.DropIndex(
            name: "IX_Contents_ModuleId_DisplayOrder",
            table: "Contents");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PublishedContents_Version",
            table: "PublishedContents",
            sql: "\"Version\" > 0");

        migrationBuilder.CreateIndex(
            name: "IX_Modules_BookId_DisplayOrder",
            table: "Modules",
            columns: new[] { "BookId", "DisplayOrder" },
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.CreateIndex(
            name: "IX_Media_Checksum",
            table: "Media",
            column: "Checksum",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Media_MediaType",
            table: "Media",
            sql: "\"MediaType\" BETWEEN 1 AND 4");

        migrationBuilder.CreateIndex(
            name: "IX_Contents_ModuleId_DisplayOrder",
            table: "Contents",
            columns: new[] { "ModuleId", "DisplayOrder" },
            unique: true,
            filter: "\"IsDeleted\" = false");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ContentBlocks_Type",
            table: "ContentBlocks",
            sql: "\"Type\" BETWEEN 1 AND 6");

        migrationBuilder.AddCheckConstraint(
            name: "CK_BookPublications_Version",
            table: "BookPublications",
            sql: "\"Version\" > 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PublishedContents_Version",
            table: "PublishedContents");

        migrationBuilder.DropIndex(
            name: "IX_Modules_BookId_DisplayOrder",
            table: "Modules");

        migrationBuilder.DropIndex(
            name: "IX_Media_Checksum",
            table: "Media");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Media_MediaType",
            table: "Media");

        migrationBuilder.DropIndex(
            name: "IX_Contents_ModuleId_DisplayOrder",
            table: "Contents");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ContentBlocks_Type",
            table: "ContentBlocks");

        migrationBuilder.DropCheckConstraint(
            name: "CK_BookPublications_Version",
            table: "BookPublications");

        migrationBuilder.CreateIndex(
            name: "IX_Modules_BookId_DisplayOrder",
            table: "Modules",
            columns: new[] { "BookId", "DisplayOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_Media_Checksum",
            table: "Media",
            column: "Checksum");

        migrationBuilder.CreateIndex(
            name: "IX_Contents_ModuleId_DisplayOrder",
            table: "Contents",
            columns: new[] { "ModuleId", "DisplayOrder" });
    }
}
