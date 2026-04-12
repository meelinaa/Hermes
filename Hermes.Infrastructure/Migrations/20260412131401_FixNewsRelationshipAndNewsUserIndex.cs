using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixNewsRelationshipAndNewsUserIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_UserId",
                table: "news");

            migrationBuilder.CreateIndex(
                name: "IX_news_UserId",
                table: "news",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_UserId",
                table: "news");

            migrationBuilder.CreateIndex(
                name: "IX_news_UserId",
                table: "news",
                column: "UserId",
                unique: true);
        }
    }
}
