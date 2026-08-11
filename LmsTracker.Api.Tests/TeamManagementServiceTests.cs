using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Modules.Teams;

namespace LmsTracker.Api.Tests;

public sealed class TeamManagementServiceTests
{
    [Fact]
    public void Validate_ReturnsErrors_WhenTeamDetailsAreIncomplete()
    {
        var request = new CreateTeamRequest("A", Guid.NewGuid(), " ", "not-an-email");

        var errors = RequestValidationHelper.Validate(request);

        Assert.Contains(errors, error => error.Contains("team name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("manager name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_CreatesTeam_WhenDepartmentExistsAndNameIsUnique()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_CreatesTeam_WhenDepartmentExistsAndNameIsUnique));
        var department = new Department { Id = Guid.NewGuid(), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var service = new TeamManagementService(db);
        var result = await service.CreateAsync(new CreateTeamRequest("Platform", department.Id, "Sam", "sam@example.com"));

        Assert.True(result.Success);
        Assert.Single(db.Teams);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenDepartmentDoesNotExist()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenDepartmentDoesNotExist));
        var service = new TeamManagementService(db);

        var result = await service.CreateAsync(new CreateTeamRequest("Platform", Guid.NewGuid(), "Sam", "sam@example.com"));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Department not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenTeamNameAlreadyExistsInDepartment()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenTeamNameAlreadyExistsInDepartment));
        var department = new Department { Id = Guid.NewGuid(), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow };
        db.Departments.Add(department);
        db.Teams.Add(new Team { Id = Guid.NewGuid(), DepartmentId = department.Id, Name = "Platform", ManagerName = "Sam", ManagerEmail = "sam@example.com", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new TeamManagementService(db);
        var result = await service.CreateAsync(new CreateTeamRequest("platform", department.Id, "Jess", "jess@example.com"));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }
}
