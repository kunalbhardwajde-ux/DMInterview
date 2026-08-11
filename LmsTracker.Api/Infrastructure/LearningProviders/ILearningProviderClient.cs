namespace LmsTracker.Api.Infrastructure.LearningProviders;

public sealed record LearningCourseItem(string ExternalCourseId, string Title, string LaunchUrl);

/// <summary>
/// Common shape for external learning-provider integrations (Udemy Business, LinkedIn Learning, ...).
/// New providers only need to implement this once to work with <see cref="Modules.Courses.CourseCatalogService"/>
/// and the integration sync endpoints.
/// </summary>
public interface ILearningProviderClient
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default);

    Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default);
}
