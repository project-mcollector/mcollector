using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analytics.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdAndProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "ProcessedEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PropertiesJson",
                table: "ProcessedEvents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEvents_ProjectId",
                table: "ProcessedEvents",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessedEvents_ProjectId",
                table: "ProcessedEvents");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "ProcessedEvents");

            migrationBuilder.DropColumn(
                name: "PropertiesJson",
                table: "ProcessedEvents");
        }
    }
}
