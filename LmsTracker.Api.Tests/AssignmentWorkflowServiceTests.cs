using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Modules.Assignments;
using Microsoft.Extensions.Logging.Abstractions;

namespace LmsTracker.Api.Tests;

public sealed class AssignmentWorkflowServiceTests
{
    private static (LmsDbContext Db, Course Course, Learner Learner, Team Team) SeedCourseAndTeam(string dbName)
    {
        var db = TestDbContextFactory.CreateNew(dbName);

        var department = new Department { Id = Guid.NewGuid(), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow };
        var team = new Team { Id = Guid.NewGuid(), DepartmentId = department.Id, Name = "Platform", ManagerName = "Sam", ManagerEmail = "sam@example.com", CreatedAtUtc = DateTime.UtcNow };
        var learner = new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1", Name = "Ada", Email = "ada@example.com", Designation = "Engineer", TeamId = team.Id, CreatedAtUtc = DateTime.UtcNow };
        var course = new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-1", Title = "Cloud Fundamentals", Provider = "Udemy", LaunchUrl = "https://example.com" };

        db.Departments.Add(department);
        db.Teams.Add(team);
        db.Learners.Add(learner);
        db.Courses.Add(course);
        db.SaveChanges();

        return (db, course, learner, team);
    }

    [Fact]
    public async Task CreateAssignmentsAsync_CreatesOneAssignment_ForIndividualLearner()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_CreatesOneAssignment_ForIndividualLearner));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(course.Id, learner.Id, null, "Permanent", null);

        var (success, errors, assignedCount, skippedDuplicateCount) = await workflow.CreateAssignmentsAsync(request);

        Assert.True(success);
        Assert.Empty(errors);
        Assert.Equal(1, assignedCount);
        Assert.Equal(0, skippedDuplicateCount);
        Assert.Single(db.Assignments, a => a.LearnerId == learner.Id && a.CourseId == course.Id);
    }

    [Fact]
    public async Task CreateAssignmentsAsync_SkipsLearner_WhoAlreadyHasAnActiveAssignmentForTheCourse()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_SkipsLearner_WhoAlreadyHasAnActiveAssignmentForTheCourse));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(course.Id, learner.Id, null, "Permanent", null);

        var first = await workflow.CreateAssignmentsAsync(request);
        Assert.True(first.Success);
        Assert.Equal(1, first.AssignedCount);

        // Simulates a double-submitted form / retried request for the same learner + course.
        var second = await workflow.CreateAssignmentsAsync(request);

        Assert.True(second.Success);
        Assert.Equal(0, second.AssignedCount);
        Assert.Equal(1, second.SkippedDuplicateCount);
        Assert.Single(db.Assignments, a => a.LearnerId == learner.Id && a.CourseId == course.Id);
    }

    [Fact]
    public async Task CreateAssignmentsAsync_AllowsReassignment_AfterThePriorAssignmentIsCompleted()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_AllowsReassignment_AfterThePriorAssignmentIsCompleted));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(course.Id, learner.Id, null, "Permanent", null);

        await workflow.CreateAssignmentsAsync(request);
        var existing = Assert.Single(db.Assignments);
        existing.Status = AssignmentStatus.Completed;
        await db.SaveChangesAsync();

        var second = await workflow.CreateAssignmentsAsync(request);

        Assert.True(second.Success);
        Assert.Equal(1, second.AssignedCount);
        Assert.Equal(0, second.SkippedDuplicateCount);
        Assert.Equal(2, db.Assignments.Count(a => a.LearnerId == learner.Id && a.CourseId == course.Id));
    }

    [Fact]
    public async Task CreateAssignmentsAsync_CreatesAssignmentPerMember_ForTeamTarget()
    {
        var (db, course, learner, team) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_CreatesAssignmentPerMember_ForTeamTarget));
        var secondLearner = new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2", Name = "Grace", Email = "grace@example.com", Designation = "Engineer", TeamId = team.Id, CreatedAtUtc = DateTime.UtcNow };
        db.Learners.Add(secondLearner);
        await db.SaveChangesAsync();

        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(course.Id, null, team.Id, "Permanent", null);

        var (success, errors, assignedCount, skippedDuplicateCount) = await workflow.CreateAssignmentsAsync(request);

        Assert.True(success);
        Assert.Empty(errors);
        Assert.Equal(2, assignedCount);
        Assert.Equal(0, skippedDuplicateCount);
        Assert.Contains(db.Assignments, a => a.LearnerId == learner.Id);
        Assert.Contains(db.Assignments, a => a.LearnerId == secondLearner.Id);
    }

    [Fact]
    public async Task CreateAssignmentsAsync_ReturnsValidationErrors_WhenTemporaryAccessHasNoDueDate()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_ReturnsValidationErrors_WhenTemporaryAccessHasNoDueDate));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(course.Id, learner.Id, null, "Temporary", null);

        var (success, errors, assignedCount, _) = await workflow.CreateAssignmentsAsync(request);

        Assert.False(success);
        Assert.Equal(0, assignedCount);
        Assert.Contains(errors, e => e.Contains("due date", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.Assignments);
    }

    [Fact]
    public async Task CreateAssignmentsAsync_ReturnsError_WhenCourseDoesNotExist()
    {
        var (db, _, learner, _) = SeedCourseAndTeam(nameof(CreateAssignmentsAsync_ReturnsError_WhenCourseDoesNotExist));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        var request = new CreateAssignmentRequest(Guid.NewGuid(), learner.Id, null, "Permanent", null);

        var (success, errors, _, _) = await workflow.CreateAssignmentsAsync(request);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Course not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateProgressAsync_UpdatesProgressAndStatus_ForExistingAssignment()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(UpdateProgressAsync_UpdatesProgressAndStatus_ForExistingAssignment));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        await workflow.CreateAssignmentsAsync(new CreateAssignmentRequest(course.Id, learner.Id, null, "Permanent", null));
        var assignmentId = Assert.Single(db.Assignments).Id;

        var result = await workflow.UpdateProgressAsync(assignmentId, 55);

        Assert.True(result.Success);
        Assert.Equal(55, result.Assignment!.ProgressPercent);
        Assert.Equal(AssignmentStatus.InProgress, result.Assignment.Status);
    }

    [Fact]
    public async Task UpdateProgressAsync_ClampsOutOfRangeValues()
    {
        var (db, course, learner, _) = SeedCourseAndTeam(nameof(UpdateProgressAsync_ClampsOutOfRangeValues));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);
        await workflow.CreateAssignmentsAsync(new CreateAssignmentRequest(course.Id, learner.Id, null, "Permanent", null));
        var assignmentId = Assert.Single(db.Assignments).Id;

        var result = await workflow.UpdateProgressAsync(assignmentId, 250);

        Assert.True(result.Success);
        Assert.Equal(100, result.Assignment!.ProgressPercent);
        Assert.Equal(AssignmentStatus.Completed, result.Assignment.Status);
    }

    [Fact]
    public async Task UpdateProgressAsync_ReturnsNotFound_ForMissingAssignment()
    {
        var db = TestDbContextFactory.CreateNew(nameof(UpdateProgressAsync_ReturnsNotFound_ForMissingAssignment));
        var workflow = new AssignmentWorkflowService(db, NullLogger<AssignmentWorkflowService>.Instance);

        var result = await workflow.UpdateProgressAsync(Guid.NewGuid(), 50);

        Assert.False(result.Success);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }
}
