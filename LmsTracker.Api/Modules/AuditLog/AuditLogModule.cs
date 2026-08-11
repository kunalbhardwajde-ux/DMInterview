using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.AuditLog;

// Read-only view over the rows LmsDbContext.SaveChangesAsync writes automatically - see
// AuditLogEntry's doc comment for how entries get here in the first place.
public sealed class AuditLogModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-log").WithTags("Audit Log").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (int? page, int? pageSize, HttpResponse response, LmsDbContext db, CancellationToken ct) =>
        {
            var query = db.AuditLogEntries
                .AsNoTracking()
                .OrderByDescending(x => x.TimestampUtc)
                .Select(x => new AuditLogEntryView(x.Id, x.TimestampUtc, x.Actor, x.Action, x.EntityType, x.EntityId));

            var rows = await PagingHelper.ApplyOptionalPagingAsync(query, page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<AuditLogEntryView>>.Ok(rows).ToHttpResult();
        });
    }
}
