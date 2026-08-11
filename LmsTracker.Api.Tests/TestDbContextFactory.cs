using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Tests;

internal static class TestDbContextFactory
{
    public static LmsDbContext CreateNew(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new LmsDbContext(options);
    }
}
