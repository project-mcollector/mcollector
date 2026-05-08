using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    EventName = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    PropertiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventCountry = table.Column<string>(type: "text", nullable: true),
                    EventBrowser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processed_events_EventName",
                table: "processed_events",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_processed_events_ProjectId",
                table: "processed_events",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_processed_events_Timestamp",
                table: "processed_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_processed_events_UserId",
                table: "processed_events",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_events");
        }
    }
}
