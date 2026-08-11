using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Modules.Learners;

namespace LmsTracker.Api.Tests;

public sealed class LearnerManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesLearner_AndAssignsMandatoryCourses()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_CreatesLearner_AndAssignsMandatoryCourses));
        db.Courses.Add(new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-1", Title = "Code of Conduct", Provider = "Udemy", LaunchUrl = "https://example.com", IsMandatory = true });
        await db.SaveChangesAsync();

        var service = new LearnerManagementService(db);
        var result = await service.CreateAsync(new CreateLearnerRequest("emp1001", "Ada Lovelace", "ada@example.com", "Engineer", null));

        Assert.True(result.Success);
        Assert.Equal("EMP1001", result.Learner!.EmployeeCode);
        Assert.Single(db.Assignments, a => a.LearnerId == result.Learner.Id);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenTeamDoesNotExist()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenTeamDoesNotExist));
        var service = new LearnerManagementService(db);

        var result = await service.CreateAsync(new CreateLearnerRequest("emp1001", "Ada Lovelace", "ada@example.com", "Engineer", Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Team not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenEmployeeCodeAlreadyExists()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenEmployeeCodeAlreadyExists));
        db.Learners.Add(new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1001", Name = "Existing", Email = "existing@example.com", Designation = "Engineer", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new LearnerManagementService(db);
        var result = await service.CreateAsync(new CreateLearnerRequest("emp1001", "Ada Lovelace", "ada@example.com", "Engineer", null));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }
}
