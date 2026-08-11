using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseSkillTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SkillTags",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillTags",
                table: "Courses");
        }
    }
}
