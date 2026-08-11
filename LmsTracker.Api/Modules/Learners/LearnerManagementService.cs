using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Learners;

public sealed class LearnerManagementService(LmsDbContext dbContext)
{
    public async Task<LearnerCreationResult> CreateAsync(CreateLearnerRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = RequestValidationHelper.Validate(request);
        if (validationErrors.Count > 0)
        {
            return LearnerCreationResult.Fail(validationErrors);
        }

        if (request.TeamId is not null)
        {
            var teamExists = await dbContext.Teams.AnyAsync(t => t.Id == request.TeamId.Value, cancellationToken);
            if (!teamExists)
            {
                return LearnerCreationResult.Fail(new[] { "Team not found." });
            }
        }

        var normalizedEmployeeCode = request.EmployeeCode.Trim().ToUpperInvariant();
        var normalizedEmail = request.Email.Trim();

        var duplicateExists = await dbContext.Learners.AnyAsync(
            l => l.EmployeeCode == normalizedEmployeeCode || l.Email == normalizedEmail,
            cancellationToken);
        if (duplicateExists)
        {
            return LearnerCreationResult.Fail(new[] { "A learner with this email or employee code already exists." });
        }

        var learner = new Learner
        {
            Id = Guid.NewGuid(),
            EmployeeCode = normalizedEmployeeCode,
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            Designation = request.Designation.Trim(),
            TeamId = request.TeamId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Learners.Add(learner);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AssignStarterCoursesAsync(learner, cancellationToken);

        return LearnerCreationResult.Ok(learner);
    }

    private async Task AssignStarterCoursesAsync(Learner learner, CancellationToken cancellationToken)
    {
        var mandatoryCourses = await dbContext.Courses
            .AsNoTracking()
            .Where(c => c.IsMandatory)
            .OrderBy(c => c.Title)
            .ToListAsync(cancellationToken);

        var targetCourses = mandatoryCourses;
        if (targetCourses.Count == 0)
        {
            var fallback = await dbContext.Courses.AsNoTracking().OrderBy(c => c.Title).FirstOrDefaultAsync(cancellationToken);
            if (fallback is not null)
            {
                targetCourses = [fallback];
            }
        }

        if (targetCourses.Count == 0)
        {
            return;
        }

        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        dbContext.Assignments.AddRange(targetCourses.Select(course => new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learner.Id,
            TeamId = learner.TeamId,
            CourseId = course.Id,
            AccessType = AccessType.Temporary,
            DueDate = dueDate,
            ProgressPercent = 0,
            Status = AssignmentStatus.NotStarted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record LearnerCreationResult(bool Success, Learner? Learner, IReadOnlyList<string> Errors)
{
    public static LearnerCreationResult Ok(Learner learner) => new(true, learner, Array.Empty<string>());

    public static LearnerCreationResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}
