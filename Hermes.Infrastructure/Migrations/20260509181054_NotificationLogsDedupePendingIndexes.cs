using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationLogsDedupePendingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "notification_logs",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "notification_logs",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ReplacedByTokenId",
                table: "refresh_tokens",
                column: "ReplacedByTokenId");

            // Must exist before dropping IX_notification_logs_UserId: InnoDB needs an index whose leading column is UserId for FK_notification_logs_users_UserId.
            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_dedupe_window",
                table: "notification_logs",
                columns: new[] { "UserId", "NewsId", "Channel", "Status", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_pending_retry",
                table: "notification_logs",
                columns: new[] { "Status", "NextRetryAt", "Id" });

            migrationBuilder.DropIndex(
                name: "IX_notification_logs_UserId",
                table: "notification_logs");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_ReplacedByTokenId",
                table: "refresh_tokens",
                column: "ReplacedByTokenId",
                principalTable: "refresh_tokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_ReplacedByTokenId",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_ReplacedByTokenId",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_UserId",
                table: "notification_logs",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_notification_logs_dedupe_window",
                table: "notification_logs");

            migrationBuilder.DropIndex(
                name: "IX_notification_logs_pending_retry",
                table: "notification_logs");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "notification_logs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "notification_logs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
