using System.Security.Claims;
using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Learners;

public sealed class LearnersModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/learners").WithTags("Learners").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (int? page, int? pageSize, HttpResponse response, LmsDbContext db, CancellationToken ct) =>
        {
            var query =
                from learner in db.Learners.AsNoTracking()
                join team in db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                join department in db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
                from department in deptJoin.DefaultIfEmpty()
                orderby learner.Name
                select new LearnerView(
                    learner.Id,
                    learner.EmployeeCode,
                    learner.Name,
                    learner.Email,
                    learner.Designation,
                    learner.TeamId,
                    team != null ? team.Name : null,
                    department != null ? department.Id : null,
                    department != null ? department.Name : null);

            var learners = await PagingHelper.ApplyOptionalPagingAsync(query, page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<LearnerView>>.Ok(learners).ToHttpResult();
        });

        group.MapPost("", async (CreateLearnerRequest request, LearnerManagementService service, LmsDbContext db) =>
        {
            var result = await service.CreateAsync(request);
            if (!result.Success)
            {
                return ApiResult<LearnerView>.ValidationFailed(result.Errors).ToHttpResult();
            }

            var learner = result.Learner!;

            string? teamName = null;
            Guid? departmentId = null;
            string? departmentName = null;

            if (learner.TeamId.HasValue)
            {
                var teamInfo = await db.Teams.AsNoTracking()
                    .Where(t => t.Id == learner.TeamId.Value)
                    .Join(
                        db.Departments.AsNoTracking(),
                        t => t.DepartmentId,
                        d => d.Id,
                        (t, d) => new { t.Name, DepartmentId = d.Id, DepartmentName = d.Name })
                    .FirstOrDefaultAsync();

                teamName = teamInfo?.Name;
                departmentId = teamInfo?.DepartmentId;
                departmentName = teamInfo?.DepartmentName;
            }

            return ApiResult<LearnerView>.Ok(
                new LearnerView(
                    learner.Id,
                    learner.EmployeeCode,
                    learner.Name,
                    learner.Email,
                    learner.Designation,
                    learner.TeamId,
                    teamName,
                    departmentId,
                    departmentName),
                "Learner created successfully.").ToHttpResult();
        });

        // A different policy than the group above, so this is its own MapGroup rather than a
        // route inside "group" - RequireAuthorization calls accumulate (AND together) rather
        // than replace, so a ManagerOnly group with a LearnerOnly route nested inside it would
        // require a token to satisfy both roles at once, which no token ever can.
        var selfServiceGroup = app.MapGroup("/api/learners").WithTags("Learners").RequireAuthorization("LearnerOnly");

        selfServiceGroup.MapGet("/me", async (ClaimsPrincipal user, LmsDbContext db, CancellationToken ct) =>
        {
            var learnerId = user.GetLearnerId();
            if (learnerId is null)
            {
                return ApiResult<LearnerView>.Fail("Token is missing a learner identity.", "UNAUTHORIZED").ToHttpResult();
            }

            var view = await (
                from learner in db.Learners.AsNoTracking()
                where learner.Id == learnerId.Value
                join team in db.Teams.AsNoTracking() on learner.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                join department in db.Departments.AsNoTracking() on team.DepartmentId equals department.Id into deptJoin
                from department in deptJoin.DefaultIfEmpty()
                select new LearnerView(
                    learner.Id,
                    learner.EmployeeCode,
                    learner.Name,
                    learner.Email,
                    learner.Designation,
                    learner.TeamId,
                    team != null ? team.Name : null,
                    department != null ? department.Id : null,
                    department != null ? department.Name : null)
            ).FirstOrDefaultAsync(ct);

            return view is null
                ? ApiResult<LearnerView>.NotFound("Learner").ToHttpResult()
                : ApiResult<LearnerView>.Ok(view).ToHttpResult();
        });
    }
}
