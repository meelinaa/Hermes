using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRefreshTokenAbsoluteExpiresAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "AbsoluteExpiresAt",
            table: "refresh_tokens",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            table: "notification_logs",
            type: "datetime(6)",
            nullable: false,
            defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledSlotUtc",
            table: "notification_logs",
            type: "datetime(6)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AbsoluteExpiresAt",
            table: "refresh_tokens");

        migrationBuilder.DropColumn(
            name: "CreatedAtUtc",
            table: "notification_logs");

        migrationBuilder.DropColumn(
            name: "ScheduledSlotUtc",
            table: "notification_logs");
    }
}
