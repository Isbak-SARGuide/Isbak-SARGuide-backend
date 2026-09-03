using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations;

/// <inheritdoc />
public partial class AddPublishedContentBookIdContentIdVersionIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_PublishedContents_BookId_ContentId_Version",
            table: "PublishedContents",
            columns: new[] { "BookId", "ContentId", "Version" },
            descending: new[] { false, false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PublishedContents_BookId_ContentId_Version",
            table: "PublishedContents");
    }
}
