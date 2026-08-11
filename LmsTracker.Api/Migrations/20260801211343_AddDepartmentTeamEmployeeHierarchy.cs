using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentTeamEmployeeHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ManagerEmail",
                table: "Teams",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                table: "Teams",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "Learners",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "Learners",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.Sql("""
                DECLARE @LegacyDepartmentId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

                IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Id] = @LegacyDepartmentId)
                BEGIN
                    INSERT INTO [Departments] ([Id], [Name], [Code], [CreatedAtUtc])
                    VALUES (@LegacyDepartmentId, N'General Operations', N'GEN', SYSUTCDATETIME());
                END

                UPDATE [Teams]
                SET [DepartmentId] = @LegacyDepartmentId
                WHERE [DepartmentId] = '00000000-0000-0000-0000-000000000000';

                UPDATE [Teams]
                SET [ManagerName] = N'Team Manager'
                WHERE [ManagerName] = N'';

                UPDATE [Teams]
                SET [ManagerEmail] = LOWER(REPLACE([Name], N' ', N'.')) + N'@company.local'
                WHERE [ManagerEmail] = N'';

                ;WITH OrderedLearners AS
                (
                    SELECT [Id], ROW_NUMBER() OVER (ORDER BY [CreatedAtUtc], [Id]) AS [RowNo]
                    FROM [Learners]
                )
                UPDATE l
                SET [EmployeeCode] = N'EMP' + RIGHT(N'0000' + CAST(o.[RowNo] AS nvarchar(10)), 4)
                FROM [Learners] l
                INNER JOIN OrderedLearners o ON o.[Id] = l.[Id]
                WHERE l.[EmployeeCode] = N'';

                UPDATE [Learners]
                SET [Designation] = N'Individual Contributor'
                WHERE [Designation] = N'';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DepartmentId_Name",
                table: "Teams",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Learners_EmployeeCode",
                table: "Learners",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Departments_DepartmentId",
                table: "Teams",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Departments_DepartmentId",
                table: "Teams");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Teams_DepartmentId_Name",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Learners_EmployeeCode",
                table: "Learners");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ManagerEmail",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "Learners");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "Learners");
        }
    }
}
