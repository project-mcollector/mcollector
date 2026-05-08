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
            migrationBuilder.DropIndex(
                name: "IX_processed_events_EventName",
                table: "processed_events");

            migrationBuilder.DropIndex(
                name: "IX_processed_events_ProjectId",
                table: "processed_events");

            migrationBuilder.DropIndex(
                name: "IX_processed_events_Timestamp",
                table: "processed_events");

            migrationBuilder.DropIndex(
                name: "IX_processed_events_UserId",
                table: "processed_events");
        }
    }
}
