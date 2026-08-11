using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Reports;

public sealed class ReportsModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization("ManagerOnly");

        group.MapGet("/progress", async (Guid? departmentId, Guid? teamId, LmsDbContext db) =>
        {
            var now = DateOnly.FromDateTime(DateTime.UtcNow);
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

            var rows = await query
                .Select(x => new
                {
                    Assignment = new AssignmentView(
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
                        x.assignment.Status.ToString()),
                    DueState = !x.assignment.DueDate.HasValue
                        ? "NoDueDate"
                        : x.assignment.DueDate.Value < now ? "Overdue" : x.assignment.DueDate.Value <= now.AddDays(2) ? "DueSoon" : "OnTrack"
                })
                .ToListAsync();

            return ApiResult<object>.Ok(rows).ToHttpResult();
        });

        group.MapGet("/mandatory-compliance", async (Guid? departmentId, Guid? teamId, LmsDbContext db) =>
        {
            var mandatoryCourses = await db.Courses
                .AsNoTracking()
                .Where(c => c.IsMandatory)
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            if (mandatoryCourses.Count == 0)
            {
                return ApiResult<IReadOnlyList<MandatoryComplianceRow>>.Ok(Array.Empty<MandatoryComplianceRow>()).ToHttpResult();
            }

            var learnersQuery =
                from learner in db.Learners.AsNoTracking()
                join team in db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                join department in db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
                from department in deptJoin.DefaultIfEmpty()
                orderby learner.Name
                select new { learner, team, department };

            if (departmentId.HasValue)
            {
                learnersQuery = learnersQuery.Where(x => x.department != null && x.department.Id == departmentId.Value);
            }

            if (teamId.HasValue)
            {
                learnersQuery = learnersQuery.Where(x => x.team != null && x.team.Id == teamId.Value);
            }

            var learners = await learnersQuery.ToListAsync();
            if (learners.Count == 0)
            {
                return ApiResult<IReadOnlyList<MandatoryComplianceRow>>.Ok(Array.Empty<MandatoryComplianceRow>()).ToHttpResult();
            }

            var learnerIds = learners.Select(x => x.learner.Id).ToHashSet();
            var mandatoryIds = mandatoryCourses.Select(c => c.Id).ToHashSet();

            var mandatoryAssignments = await db.Assignments
                .AsNoTracking()
                .Where(a => learnerIds.Contains(a.LearnerId) && mandatoryIds.Contains(a.CourseId))
                .ToListAsync();

            var rows = learners
                .Select(x =>
                {
                    var byCourse = mandatoryAssignments
                        .Where(a => a.LearnerId == x.learner.Id)
                        .GroupBy(a => a.CourseId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Any(a => a.Status == LmsTracker.Api.Domain.AssignmentStatus.Completed));

                    var pendingTitles = mandatoryCourses
                        .Where(c => !byCourse.TryGetValue(c.Id, out var completed) || !completed)
                        .Select(c => c.Title)
                        .ToList();

                    return new MandatoryComplianceRow(
                        x.learner.Id,
                        x.learner.EmployeeCode,
                        x.learner.Name,
                        x.department?.Name,
                        x.team?.Name,
                        mandatoryCourses.Count,
                        mandatoryCourses.Count - pendingTitles.Count,
                        pendingTitles.Count,
                        string.Join(", ", pendingTitles));
                })
                .Where(r => r.PendingMandatoryCourses > 0)
                .OrderByDescending(r => r.PendingMandatoryCourses)
                .ThenBy(r => r.LearnerName)
                .ToList();

            return ApiResult<IReadOnlyList<MandatoryComplianceRow>>.Ok(rows).ToHttpResult();
        });

        group.MapGet("/skill-match", async (string? skills, Guid? departmentId, Guid? teamId, int? top, SkillMatchReportService reportService) =>
        {
            var requestedSkills = ParseSkillTokens(skills);
            if (requestedSkills.Count == 0)
            {
                return ApiResult<IReadOnlyList<SkillMatchRow>>.Fail(
                    "Provide at least one skill keyword. Example: cloud, security, ai",
                    "VALIDATION_FAILED").ToHttpResult();
            }

            var maxRows = Math.Clamp(top ?? 30, 1, 200);
            var rows = await reportService.BuildAsync(requestedSkills, departmentId, teamId, maxRows);
            return ApiResult<IReadOnlyList<SkillMatchRow>>.Ok(rows).ToHttpResult();
        });
    }

    private static List<string> ParseSkillTokens(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
