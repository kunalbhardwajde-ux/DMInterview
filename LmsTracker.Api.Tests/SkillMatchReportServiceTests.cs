using LmsTracker.Api.Domain;
using LmsTracker.Api.Modules.Reports;

namespace LmsTracker.Api.Tests;

public sealed class SkillMatchReportServiceTests
{
    [Fact]
    public async Task BuildAsync_RanksLearnerWithMoreMatchedCourses_Higher()
    {
        var db = TestDbContextFactory.CreateNew(nameof(BuildAsync_RanksLearnerWithMoreMatchedCourses_Higher));

        var department = new Department { Id = Guid.NewGuid(), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow };
        var team = new Team { Id = Guid.NewGuid(), DepartmentId = department.Id, Name = "Platform", ManagerName = "Sam", ManagerEmail = "sam@example.com", CreatedAtUtc = DateTime.UtcNow };

        var strongLearner = new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1", Name = "Ada", Email = "ada@example.com", Designation = "Engineer", TeamId = team.Id, CreatedAtUtc = DateTime.UtcNow };
        var weakLearner = new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP2", Name = "Bob", Email = "bob@example.com", Designation = "Engineer", TeamId = team.Id, CreatedAtUtc = DateTime.UtcNow };

        var cloudCourse = new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-1", Title = "Cloud Security Fundamentals", Provider = "Udemy", LaunchUrl = "https://example.com" };
        var securityCourse = new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-2", Title = "Applied Security Practices", Provider = "Udemy", LaunchUrl = "https://example.com", IsMandatory = true };
        var unrelatedCourse = new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-3", Title = "Excel Basics", Provider = "Udemy", LaunchUrl = "https://example.com" };

        db.Departments.Add(department);
        db.Teams.Add(team);
        db.Learners.AddRange(strongLearner, weakLearner);
        db.Courses.AddRange(cloudCourse, securityCourse, unrelatedCourse);

        db.Assignments.AddRange(
            Completed(strongLearner.Id, cloudCourse.Id),
            Completed(strongLearner.Id, securityCourse.Id),
            Completed(weakLearner.Id, unrelatedCourse.Id));

        await db.SaveChangesAsync();

        var reportService = new SkillMatchReportService(db, new SkillMatchScoringService());
        var rows = await reportService.BuildAsync(["cloud", "security"], null, null, 30);

        Assert.Equal(2, rows.Count);
        var strongRow = rows.Single(r => r.LearnerId == strongLearner.Id);
        var weakRow = rows.Single(r => r.LearnerId == weakLearner.Id);

        Assert.True(strongRow.MatchScore > weakRow.MatchScore);
        Assert.Equal(2, strongRow.MatchedCourses);
        Assert.Equal(0, weakRow.MatchedCourses);
    }

    [Fact]
    public async Task BuildAsync_ReturnsEmpty_WhenNoSkillsRequested()
    {
        var db = TestDbContextFactory.CreateNew(nameof(BuildAsync_ReturnsEmpty_WhenNoSkillsRequested));
        var reportService = new SkillMatchReportService(db, new SkillMatchScoringService());

        var rows = await reportService.BuildAsync([], null, null, 30);

        Assert.Empty(rows);
    }

    private static Assignment Completed(Guid learnerId, Guid courseId) => new()
    {
        Id = Guid.NewGuid(),
        LearnerId = learnerId,
        CourseId = courseId,
        AccessType = AccessType.Permanent,
        ProgressPercent = 100,
        Status = AssignmentStatus.Completed,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };
}
