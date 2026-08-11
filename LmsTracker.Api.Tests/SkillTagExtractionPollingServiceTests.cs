using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure.Ai;
using LmsTracker.Api.Infrastructure.BackgroundTasks;
using LmsTracker.Api.Infrastructure.LearningProviders;
using LmsTracker.Api.Infrastructure.LinkedIn;
using LmsTracker.Api.Infrastructure.Udemy;
using LmsTracker.Api.Modules.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LmsTracker.Api.Tests;

/// <summary>
/// Exercises the real SkillTagExtractionPollingService (not a test double) against a real DI
/// container and an InMemory LmsDbContext, proving the actual production wiring: a course flagged
/// via SkillTagExtractionRequestedAtUtc gets picked up and processed with no explicit trigger
/// from the test - just like it would after a real process restart finds the same flag.
/// </summary>
public sealed class SkillTagExtractionPollingServiceTests
{
    private sealed class NoOpUdemyClient : IUdemyBusinessClient
    {
        public bool IsEnabled => false;

        public Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningCourseItem>>([]);

        public Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
    }

    private sealed class NoOpLinkedInClient : ILinkedInLearningClient
    {
        public bool IsEnabled => false;

        public Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningCourseItem>>([]);

        public Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
    }

    private sealed class StubSkillTagExtractor : ISkillTagExtractor
    {
        public bool IsEnabled => true;

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ExtractSkillTagsAsync(
            IReadOnlyList<CourseTagExtractionInput> courses, CancellationToken cancellationToken)
        {
            var result = courses.ToDictionary(c => c.Key, c => (IReadOnlyList<string>)["auto-tagged"]);
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PicksUpAFlaggedCourse_WithoutAnyExplicitTrigger()
    {
        var databaseName = nameof(ExecuteAsync_PicksUpAFlaggedCourse_WithoutAnyExplicitTrigger);

        var services = new ServiceCollection();
        services.AddDbContext<LmsDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IUdemyBusinessClient, NoOpUdemyClient>();
        services.AddSingleton<ILinkedInLearningClient, NoOpLinkedInClient>();
        services.AddSingleton<ISkillTagExtractor, StubSkillTagExtractor>();
        services.AddScoped<CourseCatalogService>();
        await using var provider = services.BuildServiceProvider();

        var courseId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            db.Courses.Add(new Course
            {
                Id = courseId,
                ExternalCourseId = "ext-1",
                Title = "Kubernetes Basics",
                Provider = "Udemy",
                LaunchUrl = "https://example.com",
                SkillTagExtractionRequestedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var hostedService = new SkillTagExtractionPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SkillTagExtractionPollingService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            Course? stored = null;
            for (var attempt = 0; attempt < 50 && (stored is null || stored.SkillTagExtractionRequestedAtUtc is not null); attempt++)
            {
                await Task.Delay(100);
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
                stored = await db.Courses.SingleAsync(c => c.Id == courseId);
            }

            Assert.NotNull(stored);
            Assert.Null(stored!.SkillTagExtractionRequestedAtUtc);
            Assert.Equal(["auto-tagged"], stored.SkillTags);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }
}
