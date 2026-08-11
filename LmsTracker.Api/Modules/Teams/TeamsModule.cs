using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Teams;

public sealed class TeamsModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (int? page, int? pageSize, string? sortBy, string? sortDir, HttpResponse response, LmsDbContext db, CancellationToken ct) =>
        {
            var joined = db.Teams
                .AsNoTracking()
                .Join(
                    db.Departments.AsNoTracking(),
                    t => t.DepartmentId,
                    d => d.Id,
                    (team, department) => new { team, department });

            // Ordering must happen on the raw joined entities, before the Select below - SQL
            // Server's EF provider can't translate an OrderBy over a property of an
            // already-projected record (TeamView), even though the equivalent projection inside
            // Select works fine; ordering post-projection only "works" against the EF InMemory
            // provider used by the integration tests, not against real SQL Server. Defaults to
            // department name then team name (the original, always-on ordering) when sortBy is
            // absent or unrecognized, so existing callers that never send sort params see no change.
            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            var ordered = sortBy?.ToLowerInvariant() switch
            {
                "name" => descending ? joined.OrderByDescending(x => x.team.Name) : joined.OrderBy(x => x.team.Name),
                "managername" => descending ? joined.OrderByDescending(x => x.team.ManagerName) : joined.OrderBy(x => x.team.ManagerName),
                "manageremail" => descending ? joined.OrderByDescending(x => x.team.ManagerEmail) : joined.OrderBy(x => x.team.ManagerEmail),
                "department" or "departmentname" => descending
                    ? joined.OrderByDescending(x => x.department.Name).ThenByDescending(x => x.team.Name)
                    : joined.OrderBy(x => x.department.Name).ThenBy(x => x.team.Name),
                _ => joined.OrderBy(x => x.department.Name).ThenBy(x => x.team.Name),
            };

            var query = ordered.Select(x => new TeamView(
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
