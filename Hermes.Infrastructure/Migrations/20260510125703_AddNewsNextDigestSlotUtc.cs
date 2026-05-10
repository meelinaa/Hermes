using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsNextDigestSlotUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextDigestSlotUtc",
                table: "news",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_NextDigestSlotUtc",
                table: "news",
                column: "NextDigestSlotUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_NextDigestSlotUtc",
                table: "news");

            migrationBuilder.DropColumn(
                name: "NextDigestSlotUtc",
                table: "news");
        }
    }
}
