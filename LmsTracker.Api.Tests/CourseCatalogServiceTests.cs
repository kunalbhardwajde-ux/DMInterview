using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure.Ai;
using LmsTracker.Api.Infrastructure.LearningProviders;
using LmsTracker.Api.Infrastructure.LinkedIn;
using LmsTracker.Api.Infrastructure.Udemy;
using LmsTracker.Api.Modules.Courses;

namespace LmsTracker.Api.Tests;

public sealed class CourseCatalogServiceTests
{
    private static CourseCatalogService CreateService(
        LmsDbContext db,
        IUdemyBusinessClient udemy,
        ILinkedInLearningClient linkedIn,
        ISkillTagExtractor extractor) =>
        new(db, udemy, linkedIn, extractor);

    private sealed class StubUdemyClient(IReadOnlyList<LearningCourseItem> results) : IUdemyBusinessClient
    {
        public int CallCount { get; private set; }

        public bool IsEnabled => true;

        public Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(results);
        }

        public Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
    }

    private sealed class DisabledLinkedInClient : ILinkedInLearningClient
    {
        public bool IsEnabled => false;

        public Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningCourseItem>>([]);

        public Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
    }

    /// <summary>Test double standing in for the real LLM call - never touches the network. Matches by Title so callers don't need to predict generated Course.Id values.</summary>
    private sealed class StubSkillTagExtractor(bool isEnabled, IReadOnlyDictionary<string, IReadOnlyList<string>>? tagsByTitle = null) : ISkillTagExtractor
    {
        public int CallCount { get; private set; }

        public bool IsEnabled => isEnabled;

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ExtractSkillTagsAsync(
            IReadOnlyList<CourseTagExtractionInput> courses, CancellationToken cancellationToken)
        {
            CallCount++;

            var result = new Dictionary<string, IReadOnlyList<string>>();
            if (tagsByTitle is not null)
            {
                foreach (var course in courses)
                {
                    if (tagsByTitle.TryGetValue(course.Title, out var tags))
                    {
                        result[course.Key] = tags;
                    }
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(result);
        }
    }

    [Fact]
    public async Task SyncFromProvidersAsync_AddsNewCourse_WhenNotYetInCatalog()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_AddsNewCourse_WhenNotYetInCatalog));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Kubernetes Basics", "https://example.com/k8s")]);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);

        Assert.Single(db.Courses, c => c.ExternalCourseId == "ext-1" && c.Provider == "Udemy");
    }

    [Fact]
    public async Task SyncFromProvidersAsync_UpdatesExistingCourse_InsteadOfDuplicating()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_UpdatesExistingCourse_InsteadOfDuplicating));
        db.Courses.Add(new Course { Id = Guid.NewGuid(), ExternalCourseId = "ext-1", Title = "Old Title", Provider = "Udemy", LaunchUrl = "https://old.example.com" });
        await db.SaveChangesAsync();

        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "New Title", "https://new.example.com")]);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        await service.SyncFromProvidersAsync("kubernetes", CancellationToken.None);

        var stored = Assert.Single(db.Courses);
        Assert.Equal("New Title", stored.Title);
        Assert.Equal("https://new.example.com", stored.LaunchUrl);
    }

    [Fact]
    public async Task SyncFromProvidersAsync_DoesNotCallProviders_WhenQueryIsEmpty()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_DoesNotCallProviders_WhenQueryIsEmpty));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Should Not Sync", "https://example.com")]);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        await service.SyncFromProvidersAsync(string.Empty, CancellationToken.None);

        Assert.Equal(0, udemy.CallCount);
        Assert.Empty(db.Courses);
    }

    [Fact]
    public async Task SearchLocalAsync_FiltersByTitle_WithoutCallingProviders()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SearchLocalAsync_FiltersByTitle_WithoutCallingProviders));
        db.Courses.Add(new Course { Id = Guid.NewGuid(), ExternalCourseId = "ext-1", Title = "Kubernetes Basics", Provider = "Udemy", LaunchUrl = "https://example.com" });
        db.Courses.Add(new Course { Id = Guid.NewGuid(), ExternalCourseId = "ext-2", Title = "Excel Basics", Provider = "Udemy", LaunchUrl = "https://example.com" });
        await db.SaveChangesAsync();

        var udemy = new StubUdemyClient([new LearningCourseItem("ext-3", "Should Not Appear", "https://example.com")]);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        var results = await service.SearchLocalAsync("Kubernetes", null, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Kubernetes Basics", results[0].Title);
        Assert.Equal(0, udemy.CallCount);
        Assert.Equal(2, db.Courses.Count());
    }

    [Fact]
    public async Task SyncFromProvidersAsync_DoesNotFlagForExtraction_WhenExtractorDisabled()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_DoesNotFlagForExtraction_WhenExtractorDisabled));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Kubernetes Basics", "https://example.com/k8s")]);
        var extractor = new StubSkillTagExtractor(isEnabled: false);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);

        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);

        Assert.Equal(0, extractor.CallCount);
        var stored = Assert.Single(db.Courses);
        Assert.Empty(stored.SkillTags);
        Assert.Null(stored.SkillTagExtractionRequestedAtUtc);
    }

    [Fact]
    public async Task SyncFromProvidersAsync_FlagsTouchedUntaggedCourses_ButDoesNotCallTheExtractorItself()
    {
        // Sync only sets the durable flag - extraction runs later, off the request path, via
        // SkillTagExtractionPollingService calling ProcessPendingSkillTagExtractionsAsync.
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_FlagsTouchedUntaggedCourses_ButDoesNotCallTheExtractorItself));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Container Orchestration with K8s", "https://example.com/k8s")]);
        var extractor = new StubSkillTagExtractor(isEnabled: true);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);

        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);

        Assert.Equal(0, extractor.CallCount);
        var stored = Assert.Single(db.Courses);
        Assert.NotNull(stored.SkillTagExtractionRequestedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingSkillTagExtractionsAsync_ExtractsAndPersistsTags_ForFlaggedCourses()
    {
        var db = TestDbContextFactory.CreateNew(nameof(ProcessPendingSkillTagExtractionsAsync_ExtractsAndPersistsTags_ForFlaggedCourses));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Container Orchestration with K8s", "https://example.com/k8s")]);
        var extractor = new StubSkillTagExtractor(
            isEnabled: true,
            tagsByTitle: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Container Orchestration with K8s"] = ["kubernetes", "container-orchestration"],
            });
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);
        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);
        Assert.NotNull(db.Courses.Single().SkillTagExtractionRequestedAtUtc);

        await service.ProcessPendingSkillTagExtractionsAsync(batchSize: 50, CancellationToken.None);

        Assert.Equal(1, extractor.CallCount);
        var stored = Assert.Single(db.Courses);
        Assert.Equal(["kubernetes", "container-orchestration"], stored.SkillTags);
        Assert.Null(stored.SkillTagExtractionRequestedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingSkillTagExtractionsAsync_ClearsFlag_EvenWhenExtractorReturnsNoTags()
    {
        // ExtractSkillTagsAsync fails open and can legitimately return nothing (low/medium
        // confidence, or a transient failure already swallowed inside the extractor) - the flag
        // must still clear, or this course would be re-sent to the LLM on every future poll tick
        // forever instead of waiting for its next real sync, per the documented "attempted is
        // terminal" contract.
        var db = TestDbContextFactory.CreateNew(nameof(ProcessPendingSkillTagExtractionsAsync_ClearsFlag_EvenWhenExtractorReturnsNoTags));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Kubernetes Basics", "https://example.com/k8s")]);
        var extractor = new StubSkillTagExtractor(isEnabled: true, tagsByTitle: null);
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);
        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);

        await service.ProcessPendingSkillTagExtractionsAsync(batchSize: 50, CancellationToken.None);

        Assert.Equal(1, extractor.CallCount);
        var stored = Assert.Single(db.Courses);
        Assert.Empty(stored.SkillTags);
        Assert.Null(stored.SkillTagExtractionRequestedAtUtc);
    }

    [Fact]
    public async Task ProcessPendingSkillTagExtractionsAsync_DoesNothing_WhenNoCoursesAreFlagged()
    {
        var db = TestDbContextFactory.CreateNew(nameof(ProcessPendingSkillTagExtractionsAsync_DoesNothing_WhenNoCoursesAreFlagged));
        var extractor = new StubSkillTagExtractor(isEnabled: true);
        var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), extractor);

        await service.ProcessPendingSkillTagExtractionsAsync(batchSize: 50, CancellationToken.None);

        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public async Task ProcessPendingSkillTagExtractionsAsync_SurvivesBeingCalledIndependently_ProvingDurabilityAcrossASimulatedCrash()
    {
        // The whole point of the durable-flag redesign: flagging (sync) and processing (the
        // poller) are two independent calls against the same persisted state, not one in-memory
        // operation - so "the process crashed in between" is naturally representable as simply
        // not calling ProcessPendingSkillTagExtractionsAsync right away. A fresh DbContext
        // instance against the same database name (as if this were a new process after restart)
        // still sees the flag and can finish the work.
        var databaseName = nameof(ProcessPendingSkillTagExtractionsAsync_SurvivesBeingCalledIndependently_ProvingDurabilityAcrossASimulatedCrash);
        var extractor = new StubSkillTagExtractor(
            isEnabled: true,
            tagsByTitle: new Dictionary<string, IReadOnlyList<string>> { ["Kubernetes Basics"] = ["kubernetes"] });

        using (var db = TestDbContextFactory.CreateNew(databaseName))
        {
            var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Kubernetes Basics", "https://example.com/k8s")]);
            var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);
            await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);
            // Simulated crash: the process exits here, before any extraction has run.
        }

        // "Restart": a brand new DbContext against the same (InMemory-named) database, standing
        // in for what SkillTagExtractionPollingService's next tick would see after the real
        // process restarts.
        using (var db = TestDbContextFactory.CreateNew(databaseName))
        {
            var flaggedBeforeProcessing = db.Courses.Single().SkillTagExtractionRequestedAtUtc;
            Assert.NotNull(flaggedBeforeProcessing);

            var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), extractor);
            await service.ProcessPendingSkillTagExtractionsAsync(batchSize: 50, CancellationToken.None);

            var stored = Assert.Single(db.Courses);
            Assert.Equal(["kubernetes"], stored.SkillTags);
            Assert.Null(stored.SkillTagExtractionRequestedAtUtc);
        }
    }

    [Fact]
    public async Task SyncFromProvidersAsync_DoesNotReFlagForExtraction_ForACourseAlreadyTagged()
    {
        // Re-syncing an already-tagged catalog is the common case (most syncs re-touch courses
        // seen before) - re-flagging (and so re-extracting) every time would silently multiply
        // LLM cost for no benefit, since tags don't change unless the title does.
        var db = TestDbContextFactory.CreateNew(nameof(SyncFromProvidersAsync_DoesNotReFlagForExtraction_ForACourseAlreadyTagged));
        var udemy = new StubUdemyClient([new LearningCourseItem("ext-1", "Kubernetes Basics", "https://example.com/k8s")]);
        var extractor = new StubSkillTagExtractor(
            isEnabled: true,
            tagsByTitle: new Dictionary<string, IReadOnlyList<string>> { ["Kubernetes Basics"] = ["kubernetes"] });
        var service = CreateService(db, udemy, new DisabledLinkedInClient(), extractor);

        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);
        await service.ProcessPendingSkillTagExtractionsAsync(batchSize: 50, CancellationToken.None);
        await service.SyncFromProvidersAsync("Kubernetes", CancellationToken.None);

        Assert.Equal(1, extractor.CallCount);
        var stored = Assert.Single(db.Courses);
        Assert.Equal(["kubernetes"], stored.SkillTags);
        Assert.Null(stored.SkillTagExtractionRequestedAtUtc);
    }

    [Fact]
    public async Task SetMandatoryAsync_UpdatesFlag_ForExistingCourse()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SetMandatoryAsync_UpdatesFlag_ForExistingCourse));
        var course = new Course { Id = Guid.NewGuid(), ExternalCourseId = "ext-1", Title = "Security Basics", Provider = "Udemy", LaunchUrl = "https://example.com" };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        var updated = await service.SetMandatoryAsync(course.Id, true, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated!.IsMandatory);
        Assert.True(db.Courses.Single().IsMandatory);
    }

    [Fact]
    public async Task SetMandatoryAsync_ReturnsNull_ForMissingCourse()
    {
        var db = TestDbContextFactory.CreateNew(nameof(SetMandatoryAsync_ReturnsNull_ForMissingCourse));
        var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        var updated = await service.SetMandatoryAsync(Guid.NewGuid(), true, CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task ClearSkillTagsAsync_ResetsTagsToEmpty_SoTheCourseIsReExtractedOnNextSync()
    {
        var db = TestDbContextFactory.CreateNew(nameof(ClearSkillTagsAsync_ResetsTagsToEmpty_SoTheCourseIsReExtractedOnNextSync));
        var course = new Course
        {
            Id = Guid.NewGuid(),
            ExternalCourseId = "ext-1",
            Title = "Container Orchestration with K8s",
            Provider = "Udemy",
            LaunchUrl = "https://example.com",
            SkillTags = ["wrong-tag"],
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        var updated = await service.ClearSkillTagsAsync(course.Id, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Empty(updated!.SkillTags);
        Assert.Empty(db.Courses.Single().SkillTags);
    }

    [Fact]
    public async Task ClearSkillTagsAsync_ReturnsNull_ForMissingCourse()
    {
        var db = TestDbContextFactory.CreateNew(nameof(ClearSkillTagsAsync_ReturnsNull_ForMissingCourse));
        var service = CreateService(db, new StubUdemyClient([]), new DisabledLinkedInClient(), new StubSkillTagExtractor(isEnabled: false));

        var updated = await service.ClearSkillTagsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(updated);
    }
}
