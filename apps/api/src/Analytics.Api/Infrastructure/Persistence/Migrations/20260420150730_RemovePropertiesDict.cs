using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analytics.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePropertiesDict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventBrowser",
                table: "ProcessedEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventCountry",
                table: "ProcessedEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "ProcessedEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventBrowser",
                table: "ProcessedEvents");

            migrationBuilder.DropColumn(
                name: "EventCountry",
                table: "ProcessedEvents");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "ProcessedEvents");
        }
    }
}
