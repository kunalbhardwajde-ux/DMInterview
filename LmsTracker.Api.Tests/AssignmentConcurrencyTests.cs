using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure.LearningProviders;
using LmsTracker.Api.Infrastructure.Udemy;
using LmsTracker.Api.Modules.Integrations;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Tests;

public sealed class AssignmentConcurrencyTests
{
    private sealed class StubProgressClient(int progressPercent) : IUdemyBusinessClient
    {
        public bool IsEnabled => true;

        public Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LearningCourseItem>>([]);

        public Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default) =>
            Task.FromResult<int?>(progressPercent);
    }

    private static async Task<(Learner Learner, Course Course, Assignment Assignment)> SeedAsync(LmsDbContext db)
    {
        var learner = new Learner { Id = Guid.NewGuid(), EmployeeCode = "EMP1", Name = "Ada", Email = "ada@example.com", Designation = "Engineer", CreatedAtUtc = DateTime.UtcNow };
        var course = new Course { Id = Guid.NewGuid(), ExternalCourseId = "c-1", Title = "Cloud Fundamentals", Provider = "Udemy", LaunchUrl = "https://example.com" };
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            LearnerId = learner.Id,
            CourseId = course.Id,
            AccessType = AccessType.Permanent,
            ProgressPercent = 0,
            Status = AssignmentStatus.NotStarted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.Learners.Add(learner);
        db.Courses.Add(course);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return (learner, course, assignment);
    }

    [Fact]
    public async Task RowVersion_ThrowsConcurrencyException_WhenOriginalValueIsStale()
    {
        // EF Core's InMemory provider doesn't auto-increment `rowversion` bytes the way SQL
        // Server does on every UPDATE, so two contexts loading-then-saving in sequence won't
        // naturally collide here. What we can and should verify against any provider is the
        // actual guarantee: SaveChanges rejects a write whose original concurrency-token value
        // no longer matches what's stored, which is exactly what protects against the
        // PATCH-vs-provider-sync race this column exists for.
        var dbName = nameof(RowVersion_ThrowsConcurrencyException_WhenOriginalValueIsStale);
        var seedDb = TestDbContextFactory.CreateNew(dbName);
        var (_, _, seededAssignment) = await SeedAsync(seedDb);

        await using var context = TestDbContextFactory.CreateNew(dbName);
        var assignment = await context.Assignments.FirstAsync(a => a.Id == seededAssignment.Id);

        context.Entry(assignment).Property(a => a.RowVersion).OriginalValue = [9, 9, 9, 9, 9, 9, 9, 9];
        assignment.ProgressPercent = 50;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SyncProgressAsync_UpdatesAssignment_WhenNoConflict()
    {
        var dbName = nameof(SyncProgressAsync_UpdatesAssignment_WhenNoConflict);
        var seedDb = TestDbContextFactory.CreateNew(dbName);
        await SeedAsync(seedDb);

        await using var context = TestDbContextFactory.CreateNew(dbName);
        var sync = new ProviderSyncService(context);

        var result = await sync.SyncProgressAsync("Udemy", new StubProgressClient(60), CancellationToken.None);

        Assert.Equal(1, result.AssignmentsChecked);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Conflicted);

        var stored = await context.Assignments.FirstAsync();
        Assert.Equal(60, stored.ProgressPercent);
        Assert.Equal(AssignmentStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task SyncProgressAsync_SkipsConflictedRow_InsteadOfThrowing()
    {
        var dbName = nameof(SyncProgressAsync_SkipsConflictedRow_InsteadOfThrowing);
        var seedDb = TestDbContextFactory.CreateNew(dbName);
        var (_, _, seededAssignment) = await SeedAsync(seedDb);

        // Pre-load the assignment into the same context ProviderSyncService will use (so its
        // internal query resolves to this already-tracked instance via EF's identity map),
        // then force a stale original RowVersion to simulate another writer having updated the
        // row in between - exactly the PATCH-vs-provider-sync race this column guards against.
        await using var syncContext = TestDbContextFactory.CreateNew(dbName);
        var tracked = await syncContext.Assignments.FirstAsync(a => a.Id == seededAssignment.Id);
        syncContext.Entry(tracked).Property(a => a.RowVersion).OriginalValue = [9, 9, 9, 9, 9, 9, 9, 9];

        var sync = new ProviderSyncService(syncContext);
        var result = await sync.SyncProgressAsync("Udemy", new StubProgressClient(90), CancellationToken.None);

        Assert.Equal(1, result.AssignmentsChecked);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Conflicted);
    }
}
