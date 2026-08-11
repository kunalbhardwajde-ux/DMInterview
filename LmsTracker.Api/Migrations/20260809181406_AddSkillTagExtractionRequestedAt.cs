using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillTagExtractionRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SkillTagExtractionRequestedAtUtc",
                table: "Courses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_SkillTagExtractionRequestedAtUtc",
                table: "Courses",
                column: "SkillTagExtractionRequestedAtUtc",
                filter: "[SkillTagExtractionRequestedAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_SkillTagExtractionRequestedAtUtc",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "SkillTagExtractionRequestedAtUtc",
                table: "Courses");
        }
    }
}
