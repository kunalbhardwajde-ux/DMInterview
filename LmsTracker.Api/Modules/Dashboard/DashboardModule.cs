using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using LmsTracker.Api.Contracts;

namespace LmsTracker.Api.Modules.Dashboard;

public sealed class DashboardModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (Guid? departmentId, Guid? teamId, LmsDbContext db) =>
        {
            var scopedAssignments =
                from assignment in db.Assignments
                join learner in db.Learners on assignment.LearnerId equals learner.Id
                join team in db.Teams on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                select new { assignment, learner, team };

            if (departmentId.HasValue)
            {
                scopedAssignments = scopedAssignments.Where(x => x.team != null && x.team.DepartmentId == departmentId.Value);
            }

            if (teamId.HasValue)
            {
                scopedAssignments = scopedAssignments.Where(x => x.team != null && x.team.Id == teamId.Value);
            }

            var scopedLearners =
                from learner in db.Learners
                join team in db.Teams on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                select new { learner, team };

            if (departmentId.HasValue)
            {
                scopedLearners = scopedLearners.Where(x => x.team != null && x.team.DepartmentId == departmentId.Value);
            }

            if (teamId.HasValue)
            {
                scopedLearners = scopedLearners.Where(x => x.team != null && x.team.Id == teamId.Value);
            }

            var scopedTeams = db.Teams.AsQueryable();
            if (departmentId.HasValue)
            {
                scopedTeams = scopedTeams.Where(t => t.DepartmentId == departmentId.Value);
            }
            if (teamId.HasValue)
            {
                scopedTeams = scopedTeams.Where(t => t.Id == teamId.Value);
            }

            var scopedDepartments = db.Departments.AsQueryable();
            if (departmentId.HasValue)
            {
                scopedDepartments = scopedDepartments.Where(d => d.Id == departmentId.Value);
            }

            var now = DateOnly.FromDateTime(DateTime.UtcNow);

            // One aggregate query instead of 5 separate CountAsync round trips for the
            // assignment-scoped numbers - status/overdue are all conditional counts over the
            // same scoped set, so there's no reason to hit the DB once per bucket.
            var assignmentStats = await scopedAssignments
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(x => x.assignment.Status == AssignmentStatus.Completed),
                    InProgress = g.Count(x => x.assignment.Status == AssignmentStatus.InProgress),
                    NotStarted = g.Count(x => x.assignment.Status == AssignmentStatus.NotStarted),
                    Overdue = g.Count(x => x.assignment.DueDate.HasValue && x.assignment.DueDate.Value < now && x.assignment.Status != AssignmentStatus.Completed),
                })
                .FirstOrDefaultAsync();

            var total = assignmentStats?.Total ?? 0;
            var completed = assignmentStats?.Completed ?? 0;
            var inProgress = assignmentStats?.InProgress ?? 0;
            var notStarted = assignmentStats?.NotStarted ?? 0;
            var overdue = assignmentStats?.Overdue ?? 0;

            var teams = await scopedTeams.CountAsync();
            var learners = await scopedLearners.CountAsync();
            var departments = await scopedDepartments.CountAsync();

            var completionRate = total == 0
                ? 0
                : Math.Round(completed * 100.0 / total, 1, MidpointRounding.AwayFromZero);

            return ApiResult<DashboardScopeResponse>.Ok(new DashboardScopeResponse(
                departments,
                teams,
                learners,
                total,
                completed,
                inProgress,
                notStarted,
                overdue,
                completionRate)).ToHttpResult();
        });
    }
}
