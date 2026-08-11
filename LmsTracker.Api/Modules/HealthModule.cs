using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules;

public sealed class HealthModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (LmsDbContext db, CancellationToken ct) =>
        {
            bool databaseReachable;
            try
            {
                databaseReachable = await db.Database.CanConnectAsync(ct);
            }
            catch
            {
                databaseReachable = false;
            }

            if (!databaseReachable)
            {
                return ApiResult<object>.Fail("Database is unreachable.", "DEPENDENCY_UNAVAILABLE").ToHttpResult();
            }

            return ApiResult<object>.Ok(new { status = "ok", database = "ok" }).ToHttpResult();
        }).WithTags("Health");
    }
}
