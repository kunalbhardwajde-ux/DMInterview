using LmsTracker.Api.Domain;
using LmsTracker.Api.Infrastructure.LearningProviders;
using Microsoft.EntityFrameworkCore;

namespace LmsTracker.Api.Modules.Integrations;

public sealed record ProviderSyncResult(int AssignmentsChecked, int Updated, int Conflicted);

/// <summary>
/// Pulls live progress for every assignment against a given provider and applies it.
/// Shared by every provider's sync-progress endpoint so adding a new provider only
/// requires an <see cref="ILearningProviderClient"/> implementation, not a new sync loop.
/// </summary>
public sealed class ProviderSyncService(LmsDbContext db)
{
    public async Task<ProviderSyncResult> SyncProgressAsync(string providerName, ILearningProviderClient client, CancellationToken ct)
    {
        var items = await (
            from a in db.Assignments
            join l in db.Learners on a.LearnerId equals l.Id
            join c in db.Courses on a.CourseId equals c.Id
            where c.Provider == providerName
            select new { Assignment = a, LearnerEmail = l.Email, c.ExternalCourseId }
        ).ToListAsync(ct);

        var updated = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalCourseId))
            {
                continue;
            }

            var progress = await client.GetProgressPercentAsync(item.LearnerEmail, item.ExternalCourseId, ct);
            if (!progress.HasValue)
            {
                continue;
            }

            var value = Math.Clamp(progress.Value, 0, 100);
            item.Assignment.ProgressPercent = value;
            item.Assignment.Status = AssignmentStatusExtensions.FromProgress(value);
            item.Assignment.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
        }

        // Progress sync races with manual PATCH /assignments/{id}/progress on the same row.
        // Rather than aborting the whole batch on one stale row, drop just the conflicting
        // entries (they'll be picked up on the next sync) and keep saving the rest.
        var conflicted = 0;
        while (updated > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                    updated--;
                    conflicted++;
                }
            }
        }

        return new ProviderSyncResult(items.Count, updated, conflicted);
    }
}
