using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Teams;

public sealed class TeamsModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (int? page, int? pageSize, HttpResponse response, LmsDbContext db, CancellationToken ct) =>
        {
            var query = db.Teams
                .AsNoTracking()
                .Join(
                    db.Departments.AsNoTracking(),
                    t => t.DepartmentId,
                    d => d.Id,
                    (team, department) => new { team, department })
                .OrderBy(x => x.department.Name)
                .ThenBy(x => x.team.Name)
                .Select(x => new TeamView(
                    x.team.Id,
                    x.team.Name,
                    x.department.Id,
                    x.department.Name,
                    x.team.ManagerName,
                    x.team.ManagerEmail));

            var teams = await PagingHelper.ApplyOptionalPagingAsync(query, page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<TeamView>>.Ok(teams).ToHttpResult();
        });

        group.MapPost("", async (CreateTeamRequest request, LmsDbContext db, TeamManagementService service) =>
        {
            var result = await service.CreateAsync(request);
            if (!result.Success)
            {
                return ApiResult<TeamView>.ValidationFailed(result.Errors).ToHttpResult();
            }

            var team = result.Team!;
            var departmentName = await db.Departments.AsNoTracking()
                .Where(d => d.Id == team.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();

            return ApiResult<TeamView>.Ok(
                new TeamView(team.Id, team.Name, team.DepartmentId, departmentName ?? string.Empty, team.ManagerName, team.ManagerEmail),
                "Team created successfully.").ToHttpResult();
        });
    }
}
