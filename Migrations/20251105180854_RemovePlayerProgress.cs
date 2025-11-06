using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteractiveMapGame.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerProgress");

            migrationBuilder.AlterColumn<string>(
                name: "LLMResponse",
                table: "InteractionLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LLMPrompt",
                table: "InteractionLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LLMResponse",
                table: "InteractionLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LLMPrompt",
                table: "InteractionLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompletedQuests = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentLevel = table.Column<int>(type: "int", nullable: false),
                    DiscoveredObjects = table.Column<int>(type: "int", nullable: false),
                    LastActive = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastX = table.Column<double>(type: "float", nullable: false),
                    LastY = table.Column<double>(type: "float", nullable: false),
                    LastZ = table.Column<double>(type: "float", nullable: false),
                    PlayerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlayerPreferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TimeSpent = table.Column<int>(type: "int", nullable: false),
                    TotalExperience = table.Column<int>(type: "int", nullable: false),
                    TotalInteractions = table.Column<int>(type: "int", nullable: false),
                    UnlockedObjects = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideosWatched = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProgress_PlayerId",
                table: "PlayerProgress",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProgress_SessionId",
                table: "PlayerProgress",
                column: "SessionId");
        }
    }
}
