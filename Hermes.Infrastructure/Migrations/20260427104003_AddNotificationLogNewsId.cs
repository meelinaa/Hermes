using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLogNewsId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewsId",
                table: "notification_logs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewsId",
                table: "notification_logs");
        }
    }
}
