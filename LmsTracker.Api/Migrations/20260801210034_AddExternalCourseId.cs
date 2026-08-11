using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCourseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCourseId",
                table: "Courses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Courses SET ExternalCourseId = CAST(Id AS nvarchar(36)) WHERE ExternalCourseId = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Provider_ExternalCourseId",
                table: "Courses",
                columns: new[] { "Provider", "ExternalCourseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_Provider_ExternalCourseId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ExternalCourseId",
                table: "Courses");
        }
    }
}
