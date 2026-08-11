using LmsTracker.Api.Domain;
using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;

namespace LmsTracker.Api.Modules.Courses;

public sealed class CoursesModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses").WithTags("Courses").RequireAuthorization("ManagerOnly");

        group.MapGet("", async (
            string? query,
            string? provider,
            int? page,
            int? pageSize,
            HttpResponse response,
            CourseCatalogService catalogService,
            CancellationToken ct) =>
        {
            var courses = await PagingHelper.ApplyOptionalPagingAsync(
                catalogService.SearchLocalQueryable(query, provider), page, pageSize, response, ct);
            return ApiResult<IReadOnlyList<Course>>.Ok(courses).ToHttpResult();
        });

        // POST, not GET, because syncing pulls from external providers and writes to the DB -
        // a GET must stay safe (no side effects) per REST semantics.
        group.MapPost("/sync", async (
            string? query,
            CourseCatalogService catalogService,
            CancellationToken ct) =>
        {
            await catalogService.SyncFromProvidersAsync(query ?? string.Empty, ct);
            return ApiResult<object>.Ok(new { synced = true }).ToHttpResult();
        });

        group.MapPatch("/{courseId:guid}/mandatory", async (
            Guid courseId,
            UpdateMandatoryCourseRequest request,
            CourseCatalogService catalogService,
            CancellationToken ct) =>
        {
            var course = await catalogService.SetMandatoryAsync(courseId, request.IsMandatory, ct);
            if (course is null)
            {
                return ApiResult<object>.NotFound("Course").ToHttpResult();
            }

            return ApiResult<object>.Ok(new { course.Id, course.Title, course.IsMandatory }).ToHttpResult();
        });

        // Manager override for a bad AI-extracted tag - see CourseCatalogService.ClearSkillTagsAsync.
        group.MapDelete("/{courseId:guid}/skill-tags", async (
            Guid courseId,
            CourseCatalogService catalogService,
            CancellationToken ct) =>
        {
            var course = await catalogService.ClearSkillTagsAsync(courseId, ct);
            if (course is null)
            {
                return ApiResult<object>.NotFound("Course").ToHttpResult();
            }

            return ApiResult<object>.Ok(new { course.Id, course.Title, course.SkillTags }).ToHttpResult();
        });
    }
}
