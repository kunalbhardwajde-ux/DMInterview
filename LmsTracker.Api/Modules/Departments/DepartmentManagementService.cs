using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Departments;

public sealed class DepartmentManagementService(LmsDbContext dbContext)
{
    public async Task<DepartmentCreationResult> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = RequestValidationHelper.Validate(request);
        if (validationErrors.Count > 0)
        {
            return DepartmentCreationResult.Fail(validationErrors);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicateExists = await dbContext.Departments.AnyAsync(d => d.Code == normalizedCode, cancellationToken);
        if (duplicateExists)
        {
            return DepartmentCreationResult.Fail(new[] { "Department code already exists." });
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = normalizedCode,
            CreatedAtUtc = DateTime.UtcNow,
        };

        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);

        return DepartmentCreationResult.Ok(department);
    }
}

public sealed record DepartmentCreationResult(bool Success, Department? Department, IReadOnlyList<string> Errors)
{
    public static DepartmentCreationResult Ok(Department department) => new(true, department, Array.Empty<string>());

    public static DepartmentCreationResult Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}
