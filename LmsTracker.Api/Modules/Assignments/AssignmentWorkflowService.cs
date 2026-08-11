using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LmsTracker.Api.Modules.Assignments;

public sealed class AssignmentWorkflowService
{
    private readonly LmsDbContext _db;
    private readonly ILogger<AssignmentWorkflowService> _logger;

    public AssignmentWorkflowService(LmsDbContext db, ILogger<AssignmentWorkflowService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Success, IReadOnlyList<string> Errors, int AssignedCount, int SkippedDuplicateCount)> CreateAssignmentsAsync(
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = RequestValidationHelper.Validate(request);
        if (validationErrors.Count > 0)
        {
            return (false, validationErrors, 0, 0);
        }

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return (false, ["Course not found."], 0, 0);
        }

        if (!Enum.TryParse<LmsTracker.Api.Domain.AccessType>(request.AccessType, true, out var accessType))
        {
            return (false, ["Access type must be Temporary or Permanent."], 0, 0);
        }

        var targetLearners = await ResolveTargetLearnersAsync(request, cancellationToken);
        if (targetLearners.Count == 0)
        {
            return (false, ["No learners found for the selected target."], 0, 0);
        }

        // Idempotency guard: a double-submitted form (or a client retry after a timeout) must
        // not create a second assignment for a learner who already has an active (non-completed)
        // assignment for this course. Re-assigning after completion is still allowed - that's a
        // legitimate "do it again" case, not a duplicate.
        var targetLearnerIds = targetLearners.Select(l => l.Id).ToHashSet();
        var learnersWithActiveAssignment = await _db.Assignments
            .Where(a => a.CourseId == request.CourseId
                && a.Status != AssignmentStatus.Completed
                && targetLearnerIds.Contains(a.LearnerId))
            .Select(a => a.LearnerId)
            .ToListAsync(cancellationToken);
        var alreadyAssignedIds = learnersWithActiveAssignment.ToHashSet();

        var learnersToAssign = targetLearners.Where(l => !alreadyAssignedIds.Contains(l.Id)).ToList();
        var skippedCount = targetLearners.Count - learnersToAssign.Count;

        if (learnersToAssign.Count == 0)
        {
            return (true, Array.Empty<string>(), 0, skippedCount);
        }

        var assignments = learnersToAssign.Select(learner => new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learner.Id,
            TeamId = learner.TeamId,
            CourseId = request.CourseId,
            AccessType = accessType,
            DueDate = request.DueDate,
            ProgressPercent = 0,
            Status = AssignmentStatus.NotStarted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }).ToList();

        try
        {
            _db.Assignments.AddRange(assignments);
            await _db.SaveChangesAsync(cancellationToken);
            return (true, Array.Empty<string>(), assignments.Count, skippedCount);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to create assignments for course {CourseId}", request.CourseId);
            return (false, ["Assignments could not be created due to a persistence failure."], 0, 0);
        }
    }

    private async Task<List<Learner>> ResolveTargetLearnersAsync(CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (request.LearnerId.HasValue)
        {
            var learner = await _db.Learners.FirstOrDefaultAsync(l => l.Id == request.LearnerId.Value, cancellationToken);
            return learner is null ? [] : [learner];
        }

        if (request.TeamId.HasValue)
        {
            return await _db.Learners.Where(l => l.TeamId == request.TeamId.Value).ToListAsync(cancellationToken);
        }

        return [];
    }

    /// <summary>Moved here from the PATCH endpoint lambda so this mutation goes through the service layer like every other write, instead of touching LmsDbContext directly from routing code.</summary>
    public async Task<UpdateProgressResult> UpdateProgressAsync(Guid assignmentId, int progressPercent, CancellationToken cancellationToken = default)
    {
        var assignment = await _db.Assignments.FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return UpdateProgressResult.NotFound();
        }

        var progress = Math.Clamp(progressPercent, 0, 100);
        assignment.ProgressPercent = progress;
        assignment.Status = AssignmentStatusExtensions.FromProgress(progress);
        assignment.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateProgressResult.Conflict();
        }

        return UpdateProgressResult.Ok(assignment);
    }
}

public sealed record UpdateProgressResult(bool Success, string? ErrorCode, string? ErrorMessage, Assignment? Assignment)
{
    public static UpdateProgressResult Ok(Assignment assignment) => new(true, null, null, assignment);

    public static UpdateProgressResult NotFound() => new(false, "NOT_FOUND", "Assignment not found.", null);

    public static UpdateProgressResult Conflict() => new(
        false, "CONFLICT", "This assignment was updated by another process. Reload and try again.", null);
}
