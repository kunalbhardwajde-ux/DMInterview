using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Departments;

public sealed class DepartmentsModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/departments").WithTags("Departments").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (LmsDbContext db) =>
        {
            var departments = await db.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DepartmentView(d.Id, d.Name, d.Code))
                .ToListAsync();

            return ApiResult<IReadOnlyList<DepartmentView>>.Ok(departments).ToHttpResult();
        });

        group.MapPost("", async (CreateDepartmentRequest request, DepartmentManagementService service) =>
        {
            var result = await service.CreateAsync(request);
            if (!result.Success)
            {
                return ApiResult<DepartmentView>.ValidationFailed(result.Errors).ToHttpResult();
            }

            var department = result.Department!;
            return ApiResult<DepartmentView>.Ok(
                new DepartmentView(department.Id, department.Name, department.Code),
                "Department created successfully.").ToHttpResult();
        });
    }
}
