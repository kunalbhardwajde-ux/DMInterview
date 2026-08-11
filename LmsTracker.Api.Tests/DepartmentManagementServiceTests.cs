using LmsTracker.Api.Contracts;
using LmsTracker.Api.Domain;
using LmsTracker.Api.Modules.Departments;

namespace LmsTracker.Api.Tests;

public sealed class DepartmentManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesDepartment_WhenCodeIsUnique()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_CreatesDepartment_WhenCodeIsUnique));
        var service = new DepartmentManagementService(db);

        var result = await service.CreateAsync(new CreateDepartmentRequest("Engineering", "eng"));

        Assert.True(result.Success);
        Assert.Equal("ENG", result.Department!.Code);
        Assert.Single(db.Departments);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenCodeAlreadyExists()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenCodeAlreadyExists));
        db.Departments.Add(new Department { Id = Guid.NewGuid(), Name = "Engineering", Code = "ENG", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new DepartmentManagementService(db);
        var result = await service.CreateAsync(new CreateDepartmentRequest("Engineering Two", "eng"));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Single(db.Departments);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenNameTooShort()
    {
        var db = TestDbContextFactory.CreateNew(nameof(CreateAsync_Fails_WhenNameTooShort));
        var service = new DepartmentManagementService(db);

        var result = await service.CreateAsync(new CreateDepartmentRequest("A", "ENG"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
