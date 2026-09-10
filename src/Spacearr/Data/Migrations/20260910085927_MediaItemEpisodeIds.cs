using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spacearr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaItemEpisodeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EpisodeIds",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EpisodeIds",
                table: "MediaItems");
        }
    }
}
