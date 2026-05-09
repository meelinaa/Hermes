using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Infrastructure.Migrations;

/// <summary>
/// Replaces mistaken unique index on news.UserId (one user may have many news rows).
/// MySQL requires dropping the foreign key before dropping the index the FK uses.
/// </summary>
public partial class FixNewsRelationshipAndNewsUserIndex : Migration
{
    private const string NEWS_USER_FK = "FK_news_users_UserId";
    private const string NEWS_USER_INDEX = "IX_news_UserId";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: NEWS_USER_FK,
            table: "news");

        migrationBuilder.DropIndex(
            name: NEWS_USER_INDEX,
            table: "news");

        migrationBuilder.CreateIndex(
            name: NEWS_USER_INDEX,
            table: "news",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: NEWS_USER_FK,
            table: "news",
            column: "UserId",
            principalTable: "users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: NEWS_USER_FK,
            table: "news");

        migrationBuilder.DropIndex(
            name: NEWS_USER_INDEX,
            table: "news");

        migrationBuilder.CreateIndex(
            name: NEWS_USER_INDEX,
            table: "news",
            column: "UserId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: NEWS_USER_FK,
            table: "news",
            column: "UserId",
            principalTable: "users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
