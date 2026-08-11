using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.LinkedIn;

namespace LmsTracker.Api.Modules.Integrations;

public sealed class LinkedInIntegrationModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/linkedin").WithTags("LinkedIn Integration").RequireAuthorization("ManagerOnly");

        group.MapGet("/status", (ILinkedInLearningClient client) =>
            ApiResult<object>.Ok(new { enabled = client.IsEnabled }).ToHttpResult());

        group.MapPost("/sync-progress", async (ILinkedInLearningClient linkedIn, ProviderSyncService sync, CancellationToken ct) =>
        {
            if (!linkedIn.IsEnabled)
            {
                return ApiResult<object>.Fail("LinkedIn Learning integration is not configured.").ToHttpResult();
            }

            var result = await sync.SyncProgressAsync("LinkedIn", linkedIn, ct);
            return ApiResult<object>.Ok(new { assignmentsChecked = result.AssignmentsChecked, updated = result.Updated, conflicted = result.Conflicted }).ToHttpResult();
        });
    }
}
