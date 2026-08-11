using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentCourseStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_CourseId",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CourseId_Status",
                table: "Assignments",
                columns: new[] { "CourseId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_CourseId_Status",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CourseId",
                table: "Assignments",
                column: "CourseId");
        }
    }
}
