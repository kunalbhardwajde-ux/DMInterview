using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMandatoryCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMandatory",
                table: "Courses");
        }
    }
}
