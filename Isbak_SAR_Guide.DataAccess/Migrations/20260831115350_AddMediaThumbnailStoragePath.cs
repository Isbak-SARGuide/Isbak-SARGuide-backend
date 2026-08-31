using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isbak_SAR_Guide.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaThumbnailStoragePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStoragePath",
                table: "Media",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailStoragePath",
                table: "Media");
        }
    }
}
