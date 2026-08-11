using System.Security.Claims;
using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Assignments;

public sealed class AssignmentsModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assignments").WithTags("Assignments").RequireAuthorization("ManagerOnly");

        group.MapPost("", async (CreateAssignmentRequest request, AssignmentWorkflowService workflow) =>
        {
            var (success, errors, assignedCount, skippedDuplicateCount) = await workflow.CreateAssignmentsAsync(request);
            if (!success)
            {
                return ApiResult<object>.ValidationFailed(errors).ToHttpResult();
            }

            var message = skippedDuplicateCount > 0
                ? $"Assignments created successfully. {skippedDuplicateCount} learner(s) already had an active assignment for this course and were skipped."
                : "Assignments created successfully.";

            return ApiResult<object>.Ok(
                new { assigned = assignedCount, skippedDuplicates = skippedDuplicateCount },
                message).ToHttpResult();
        });

        group.MapGet("", async (Guid? departmentId, Guid? teamId, int? page, int? pageSize, HttpResponse response, LmsDbContext db, CancellationToken ct) =>
        {
            var query =
                from assignment in db.Assignments.AsNoTracking()
                join learner in db.Learners.AsNoTracking() on assignment.LearnerId equals learner.Id
                join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
                join team in db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                join department in db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
                from department in deptJoin.DefaultIfEmpty()
                select new { assignment, learner, course, team, department };

            if (departmentId.HasValue)
            {
                query = query.Where(x => x.department != null && x.department.Id == departmentId.Value);
            }

            if (teamId.HasValue)
            {
                query = query.Where(x => x.team != null && x.team.Id == teamId.Value);
            }

            var projected = query
                .OrderByDescending(x => x.assignment.CreatedAtUtc)
                .Select(x => new AssignmentView(
                    x.assignment.Id,
                    x.assignment.LearnerId,
                    x.learner.EmployeeCode,
                    x.learner.Name,
                    x.department != null ? x.department.Id : null,
                    x.department != null ? x.department.Name : null,
                    x.team != null ? x.team.Name : null,
                    x.assignment.CourseId,
                    x.course.Title,
                    x.course.Provider,
                    x.course.LaunchUrl,
                    x.assignment.AccessType.ToString(),
                    x.assignment.DueDate.HasValue ? x.assignment.DueDate.Value.ToString("yyyy-MM-dd") : null,
                    x.assignment.ProgressPercent,
                    x.assignment.Status.ToString()));

            var rows = await PagingHelper.ApplyOptionalPagingAsync(projected, page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<AssignmentView>>.Ok(rows).ToHttpResult();
        });

        group.MapPatch("/{assignmentId:guid}/progress", async (Guid assignmentId, UpdateProgressRequest request, AssignmentWorkflowService workflow, CancellationToken ct) =>
        {
            var result = await workflow.UpdateProgressAsync(assignmentId, request.ProgressPercent, ct);
            if (!result.Success)
            {
                return result.ErrorCode == "NOT_FOUND"
                    ? ApiResult<object>.NotFound("Assignment").ToHttpResult()
                    : ApiResult<object>.Fail(result.ErrorMessage!, result.ErrorCode).ToHttpResult();
            }

            var assignment = result.Assignment!;
            return ApiResult<object>.Ok(
                new { assignment.Id, assignment.ProgressPercent, Status = assignment.Status.ToString() }).ToHttpResult();
        });

        // Separate MapGroup, same reasoning as LearnersModule's /me - RequireAuthorization
        // policies accumulate rather than replace, so LearnerOnly can't be layered onto a
        // ManagerOnly group.
        var selfServiceGroup = app.MapGroup("/api/assignments").WithTags("Assignments").RequireAuthorization("LearnerOnly");

        selfServiceGroup.MapGet("/mine", async (int? page, int? pageSize, string? sortBy, string? sortDir, HttpResponse response, ClaimsPrincipal user, LmsDbContext db, CancellationToken ct) =>
        {
            var learnerId = user.GetLearnerId();
            if (learnerId is null)
            {
                return ApiResult<IReadOnlyList<AssignmentView>>.Fail("Token is missing a learner identity.", "UNAUTHORIZED").ToHttpResult();
            }

            var query =
                from assignment in db.Assignments.AsNoTracking()
                where assignment.LearnerId == learnerId.Value
                join learner in db.Learners.AsNoTracking() on assignment.LearnerId equals learner.Id
                join course in db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
                join team in db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                join department in db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
                from department in deptJoin.DefaultIfEmpty()
                select new { assignment, learner, course, team, department };

            // sortBy is optional - unrecognized or omitted falls back to the original, always-on
            // "most recently assigned first" ordering, so existing callers see no behavior change.
            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sortBy?.ToLowerInvariant() switch
            {
                "coursetitle" => descending ? query.OrderByDescending(x => x.course.Title) : query.OrderBy(x => x.course.Title),
                "provider" => descending ? query.OrderByDescending(x => x.course.Provider) : query.OrderBy(x => x.course.Provider),
                "accesstype" => descending ? query.OrderByDescending(x => x.assignment.AccessType) : query.OrderBy(x => x.assignment.AccessType),
                "duedate" => descending ? query.OrderByDescending(x => x.assignment.DueDate) : query.OrderBy(x => x.assignment.DueDate),
                "progresspercent" => descending ? query.OrderByDescending(x => x.assignment.ProgressPercent) : query.OrderBy(x => x.assignment.ProgressPercent),
                "status" => descending ? query.OrderByDescending(x => x.assignment.Status) : query.OrderBy(x => x.assignment.Status),
                _ => query.OrderByDescending(x => x.assignment.CreatedAtUtc),
            };

            var projected = query
                .Select(x => new AssignmentView(
                    x.assignment.Id,
                    x.assignment.LearnerId,
                    x.learner.EmployeeCode,
                    x.learner.Name,
                    x.department != null ? x.department.Id : null,
                    x.department != null ? x.department.Name : null,
                    x.team != null ? x.team.Name : null,
                    x.assignment.CourseId,
                    x.course.Title,
                    x.course.Provider,
                    x.course.LaunchUrl,
                    x.assignment.AccessType.ToString(),
                    x.assignment.DueDate.HasValue ? x.assignment.DueDate.Value.ToString("yyyy-MM-dd") : null,
                    x.assignment.ProgressPercent,
                    x.assignment.Status.ToString()));

            var rows = await PagingHelper.ApplyOptionalPagingAsync(projected, page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<AssignmentView>>.Ok(rows).ToHttpResult();
        });
    }
}
