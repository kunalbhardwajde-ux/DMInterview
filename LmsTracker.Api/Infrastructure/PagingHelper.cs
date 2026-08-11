using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Infrastructure;

/// <summary>
/// Optional, backward-compatible server-side paging: when the caller omits both
/// <paramref name="page"/> and <paramref name="pageSize"/>, behavior is unchanged (the full
/// result set is returned, as every list endpoint always has). When either is supplied, the
/// query is paged server-side and the pre-paging total is reported via the "X-Total-Count"
/// response header rather than changing the response body shape, so existing callers that never
/// send paging params keep working exactly as before.
/// </summary>
public static class PagingHelper
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 200;

    public static async Task<IReadOnlyList<T>> ApplyOptionalPagingAsync<T>(
        IQueryable<T> query,
        int? page,
        int? pageSize,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!page.HasValue && !pageSize.HasValue)
        {
            return await query.ToListAsync(cancellationToken);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        response.Headers["X-Total-Count"] = totalCount.ToString();

        var effectivePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var effectivePage = Math.Max(1, page ?? 1);
        var skip = (effectivePage - 1) * effectivePageSize;

        return await query.Skip(skip).Take(effectivePageSize).ToListAsync(cancellationToken);
    }
}
