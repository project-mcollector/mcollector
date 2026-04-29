using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analytics.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameProcessedEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ProcessedEvents",
                newName: "processed_events");

            migrationBuilder.Sql(@"ALTER TABLE processed_events RENAME CONSTRAINT ""PK_ProcessedEvents"" TO ""PK_processed_events"";");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedEvents_UserId",
                table: "processed_events",
                newName: "IX_processed_events_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedEvents_ProjectId",
                table: "processed_events",
                newName: "IX_processed_events_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ProcessedEvents_EventName",
                table: "processed_events",
                newName: "IX_processed_events_EventName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "processed_events",
                newName: "ProcessedEvents");

            migrationBuilder.Sql(@"ALTER TABLE ""ProcessedEvents"" RENAME CONSTRAINT ""PK_processed_events"" TO ""PK_ProcessedEvents"";");

            migrationBuilder.RenameIndex(
                name: "IX_processed_events_UserId",
                table: "ProcessedEvents",
                newName: "IX_ProcessedEvents_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_processed_events_ProjectId",
                table: "ProcessedEvents",
                newName: "IX_ProcessedEvents_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_processed_events_EventName",
                table: "ProcessedEvents",
                newName: "IX_ProcessedEvents_EventName");
        }
    }
}
