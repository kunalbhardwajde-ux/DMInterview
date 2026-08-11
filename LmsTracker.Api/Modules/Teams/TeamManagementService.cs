using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Teams;

public sealed class TeamManagementService(LmsDbContext dbContext)
{
    public async Task<TeamCreationResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = RequestValidationHelper.Validate(request);
        if (validationErrors.Count > 0)
        {
            return TeamCreationResult.Fail(validationErrors);
        }

        var normalizedName = request.Name.Trim();
        var normalizedManagerName = request.ManagerName.Trim();
        var normalizedManagerEmail = request.ManagerEmail.Trim();

        var department = await dbContext.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (department is null)
        {
            return TeamCreationResult.Fail(new[] { "Department not found." });
        }

        var duplicateExists = await dbContext.Teams.AnyAsync(
            t => t.DepartmentId == request.DepartmentId && t.Name.ToLower() == normalizedName.ToLower(),
            cancellationToken);
        if (duplicateExists)
        {
            return TeamCreationResult.Fail(new[] { "A team with the same name already exists in this department." });
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            DepartmentId = request.DepartmentId,
            Name = normalizedName,
            ManagerName = normalizedManagerName,
            ManagerEmail = normalizedManagerEmail,
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TeamCreationResult.Ok(team);
    }
}

public sealed record TeamCreationResult(bool Success, Team? Team, IReadOnlyList<string> Errors)
{
    public static TeamCreationResult Ok(Team team) => new(true, team, Array.Empty<string>());

    public static TeamCreationResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}
