using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure.Ai;
using LmsTracker.Api.Infrastructure.LearningProviders;
using LmsTracker.Api.Infrastructure.LinkedIn;
using LmsTracker.Api.Infrastructure.Udemy;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Courses;

public sealed class CourseCatalogService
{
    private readonly LmsDbContext _db;
    private readonly ISkillTagExtractor _skillTagExtractor;
    private readonly IReadOnlyList<(string ProviderName, ILearningProviderClient Client)> _providers;

    public CourseCatalogService(
        LmsDbContext db,
        IUdemyBusinessClient udemy,
        ILinkedInLearningClient linkedIn,
        ISkillTagExtractor skillTagExtractor)
    {
        _db = db;
        _skillTagExtractor = skillTagExtractor;
        _providers = [("Udemy", udemy), ("LinkedIn", linkedIn)];
    }

    /// <summary>The filtered, ordered-but-unmaterialized local catalog query. Exposed separately from SearchLocalAsync so the endpoint can apply optional server-side paging (PagingHelper needs an IQueryable to Count/Skip/Take against) without this service depending on HttpResponse.</summary>
    public IQueryable<Course> SearchLocalQueryable(string? query, string? provider)
    {
        var coursesQuery = _db.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            coursesQuery = coursesQuery.Where(c => c.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            coursesQuery = coursesQuery.Where(c => c.Title.Contains(query));
        }

        return coursesQuery.OrderBy(c => c.Title);
    }

    /// <summary>Read-only local catalog search, fully materialized. Safe to call from a GET - never calls out to a provider or writes to the DB.</summary>
    public async Task<IReadOnlyList<Course>> SearchLocalAsync(string? query, string? provider, CancellationToken cancellationToken) =>
        await SearchLocalQueryable(query, provider).ToListAsync(cancellationToken);

    /// <summary>Moved here from the PATCH endpoint lambda so this mutation goes through the service layer like every other write, instead of touching LmsDbContext directly from routing code.</summary>
    public async Task<Course?> SetMandatoryAsync(Guid courseId, bool isMandatory, CancellationToken cancellationToken)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null)
        {
            return null;
        }

        course.IsMandatory = isMandatory;
        await _db.SaveChangesAsync(cancellationToken);

        return course;
    }

    /// <summary>
    /// Manager override for a bad AI-extracted tag set: clears Course.SkillTags back to empty.
    /// This is the honest scope of "human review" here - there's no approval workflow before a
    /// high-confidence tag is applied, but a manager who spots a wrong one has a real lever to
    /// remove it. Matching falls back to title-only for this course immediately; the tags are
    /// re-attempted (not guaranteed re-applied - confidence gating still applies) the next time
    /// a sync touches this course, since ExtractAndPersistSkillTagsAsync only ever targets
    /// courses with zero tags.
    /// </summary>
    public async Task<Course?> ClearSkillTagsAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null)
        {
            return null;
        }

        if (course.SkillTags.Count > 0)
        {
            course.SkillTags = [];
            await _db.SaveChangesAsync(cancellationToken);
        }

        return course;
    }

    /// <summary>Pulls the latest catalog from enabled providers and upserts it locally. Has side effects - only call from a mutating endpoint.</summary>
    public async Task SyncFromProvidersAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var touchedCourses = new List<Course>();

        foreach (var (providerName, client) in _providers)
        {
            if (!client.IsEnabled)
            {
                continue;
            }

            var remoteCourses = await client.SearchCoursesAsync(query, cancellationToken);
            touchedCourses.AddRange(await SyncProviderCoursesAsync(providerName, remoteCourses, cancellationToken));
        }

        // Enrichment runs off the request path: flag the touched, still-untagged courses and
        // return, rather than making the caller of POST /api/courses/sync wait on an LLM round
        // trip on top of the provider calls it already waited on -
        // SkillTagExtractionPollingService picks up flagged courses on its own schedule. Setting
        // a durable DB column here (instead of enqueueing an in-memory delegate) is what makes
        // this survive a crash between "sync completed" and "extraction ran" - see
        // Course.SkillTagExtractionRequestedAtUtc's doc comment. No-op when extraction is
        // disabled, so nothing is flagged at all in the common (disabled) case.
        if (_skillTagExtractor.IsEnabled && touchedCourses.Count > 0)
        {
            var now = DateTime.UtcNow;
            var flagged = false;
            foreach (var course in touchedCourses.Where(c => c.SkillTags.Count == 0))
            {
                course.SkillTagExtractionRequestedAtUtc = now;
                flagged = true;
            }

            if (flagged)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<Course>> SyncProviderCoursesAsync(string provider, IReadOnlyList<LearningCourseItem> remoteCourses, CancellationToken cancellationToken)
    {
        if (remoteCourses.Count == 0)
        {
            return [];
        }

        var externalIds = remoteCourses.Select(c => c.ExternalCourseId).ToHashSet();
        var existingByExternalId = await _db.Courses
            .Where(c => c.Provider == provider && externalIds.Contains(c.ExternalCourseId))
            .ToDictionaryAsync(c => c.ExternalCourseId, cancellationToken);

        var touched = new List<Course>();

        foreach (var live in remoteCourses)
        {
            if (existingByExternalId.TryGetValue(live.ExternalCourseId, out var existing))
            {
                existing.Title = live.Title;
                existing.LaunchUrl = live.LaunchUrl;
                touched.Add(existing);
            }
            else
            {
                var created = new Course
                {
                    Id = Guid.NewGuid(),
                    ExternalCourseId = live.ExternalCourseId,
                    Title = live.Title,
                    Provider = provider,
                    LaunchUrl = live.LaunchUrl
                };
                _db.Courses.Add(created);
                touched.Add(created);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return touched;
    }

    /// <summary>
    /// Processes courses flagged via SkillTagExtractionRequestedAtUtc (set by
    /// SyncFromProvidersAsync above) - called by SkillTagExtractionPollingService on a fixed
    /// interval, each tick against a freshly-resolved CourseCatalogService/LmsDbContext from its
    /// own DI scope, never a stale request-scoped one. This durable-flag design (versus the old
    /// in-memory work queue) is what lets extraction survive a process crash between "sync
    /// completed" and "extraction ran": the flag lives in SQL Server, so the next poll after
    /// restart picks up exactly where it left off instead of the work silently vanishing.
    ///
    /// Sends every flagged, still-untagged course to the configured LLM in one batched request
    /// and persists the returned skill tags. If extraction fails for any reason, this is a no-op
    /// and skill matching keeps working exactly as it did before, via title-substring matching
    /// alone (see SkillMatchScoringService) - but the flag still clears, since
    /// ISkillTagExtractor.ExtractSkillTagsAsync fails open (never throws) and "attempted" is a
    /// terminal state here until the next sync re-flags the course.
    /// </summary>
    public async Task ProcessPendingSkillTagExtractionsAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!_skillTagExtractor.IsEnabled)
        {
            return;
        }

        var pending = await _db.Courses
            .Where(c => c.SkillTagExtractionRequestedAtUtc != null)
            .OrderBy(c => c.SkillTagExtractionRequestedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        // SkillTags is a value-converted property (JSON column) - filter in-memory rather than
        // in the query above, since EF Core cannot translate List<string>.Count into SQL against
        // a converted column.
        var untagged = pending.Where(c => c.SkillTags.Count == 0).ToList();
        foreach (var alreadyTagged in pending.Where(c => c.SkillTags.Count > 0))
        {
            // Reached durably-pending state but already has tags from some other path - nothing
            // left to do, just clear the flag.
            alreadyTagged.SkillTagExtractionRequestedAtUtc = null;
        }

        if (untagged.Count > 0)
        {
            var inputs = untagged
                .Select(c => new CourseTagExtractionInput(c.Id.ToString(), c.Title))
                .ToList();

            var tagsByCourseId = await _skillTagExtractor.ExtractSkillTagsAsync(inputs, cancellationToken);
            foreach (var course in untagged)
            {
                if (tagsByCourseId.TryGetValue(course.Id.ToString(), out var tags) && tags.Count > 0)
                {
                    course.SkillTags = tags.ToList();
                }

                course.SkillTagExtractionRequestedAtUtc = null;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
