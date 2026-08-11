using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Reports;

public sealed class SkillMatchReportService
{
    private readonly LmsDbContext _db;
    private readonly SkillMatchScoringService _scoringService;

    public SkillMatchReportService(LmsDbContext db, SkillMatchScoringService scoringService)
    {
        _db = db;
        _scoringService = scoringService;
    }

    public async Task<IReadOnlyList<SkillMatchRow>> BuildAsync(
        IReadOnlyList<string> requestedSkills,
        Guid? departmentId,
        Guid? teamId,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        if (requestedSkills.Count == 0)
        {
            return Array.Empty<SkillMatchRow>();
        }

        var learnersQuery = BuildLearnersQuery(departmentId, teamId);
        var learners = await learnersQuery.ToListAsync(cancellationToken);

        if (learners.Count == 0)
        {
            return Array.Empty<SkillMatchRow>();
        }

        var learnerIds = learners.Select(x => x.Learner.Id).ToHashSet();
        var completedAssignments = await (
            from assignment in _db.Assignments.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on assignment.CourseId equals course.Id
            where learnerIds.Contains(assignment.LearnerId) && assignment.Status == AssignmentStatus.Completed
            select new
            {
                assignment.LearnerId,
                course.Title,
                course.IsMandatory,
                course.SkillTags
            })
            .ToListAsync(cancellationToken);

        var byLearner = completedAssignments
            .GroupBy(x => x.LearnerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return learners
            .Select(x =>
            {
                var completed = byLearner.TryGetValue(x.Learner.Id, out var learnerCompleted)
                    ? learnerCompleted
                    : [];

                var summary = _scoringService.Evaluate(
                    completed.Select(course => new SkillMatchCourseSummary(course.Title, course.IsMandatory, course.SkillTags)),
                    requestedSkills);

                return new SkillMatchRow(
                    x.Learner.Id,
                    x.Learner.EmployeeCode,
                    x.Learner.Name,
                    x.Department?.Name,
                    x.Team?.Name,
                    summary.CompletedCourseCount,
                    summary.MatchedCourseTitles.Count,
                    summary.MatchedSkills.Count,
                    summary.TotalScore,
                    summary.Tier,
                    string.Join(", ", summary.MatchedSkills),
                    string.Join(", ", summary.MissingSkills),
                    string.Join(" | ", summary.MatchedCourseTitles.Take(3)),
                    string.Join(", ", summary.ScoreBreakdown));
            })
            .OrderByDescending(r => r.MatchScore)
            .ThenByDescending(r => r.MatchedCourses)
            .ThenByDescending(r => r.CompletedCourses)
            .ThenBy(r => r.LearnerName)
            .Take(maxRows)
            .ToList();
    }

    private IQueryable<LearnerQueryRow> BuildLearnersQuery(Guid? departmentId, Guid? teamId)
    {
        var query =
            from learner in _db.Learners.AsNoTracking()
            join team in _db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
            from team in teamJoin.DefaultIfEmpty()
            join department in _db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
            from department in deptJoin.DefaultIfEmpty()
            orderby learner.Name
            select new LearnerQueryRow(learner, team, department);

        if (departmentId.HasValue)
        {
            query = query.Where(x => x.Department != null && x.Department.Id == departmentId.Value);
        }

        if (teamId.HasValue)
        {
            query = query.Where(x => x.Team != null && x.Team.Id == teamId.Value);
        }

        return query;
    }
}

public sealed record LearnerQueryRow(Learner Learner, Team? Team, Department? Department);
