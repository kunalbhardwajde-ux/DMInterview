using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.Udemy;

namespace LmsTracker.Api.Modules.Integrations;

public sealed class UdemyIntegrationModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/udemy").WithTags("Udemy Integration").RequireAuthorization("ManagerOnly");

        group.MapGet("/status", (IUdemyBusinessClient client) =>
            ApiResult<object>.Ok(new { enabled = client.IsEnabled }).ToHttpResult());

        group.MapPost("/sync-progress", async (IUdemyBusinessClient udemy, ProviderSyncService sync, CancellationToken ct) =>
        {
            if (!udemy.IsEnabled)
            {
                return ApiResult<object>.Fail("Udemy Business integration is not configured.").ToHttpResult();
            }

            var result = await sync.SyncProgressAsync("Udemy", udemy, ct);
            return ApiResult<object>.Ok(new { assignmentsChecked = result.AssignmentsChecked, updated = result.Updated, conflicted = result.Conflicted }).ToHttpResult();
        });
    }
}
