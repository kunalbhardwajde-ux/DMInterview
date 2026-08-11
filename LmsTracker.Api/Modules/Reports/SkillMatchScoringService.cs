namespace LmsTracker.Api.Modules.Reports;

public sealed record SkillMatchCourseSummary(string Title, bool IsMandatory, IReadOnlyList<string>? SkillTags = null);

public sealed record SkillMatchEvaluation(
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> MatchedCourseTitles,
    int CompletedCourseCount,
    int MatchedSkillScore,
    int MatchedCourseScore,
    int MandatoryCourseScore,
    int CompletedCourseScore,
    int TotalScore,
    string Tier,
    IReadOnlyList<string> ScoreBreakdown);

/// <summary>
/// Ranks a learner's readiness for a client interview against a requested skill set.
///
/// Scores four independent signals, each capped at <see cref="MaxPerCategory"/> so no
/// single signal can dominate the total, then sums them into a 0-20 score:
///   - Matched skills:    how many requested skills the learner's completed courses cover.
///   - Matched courses:   how many distinct completed courses back those matched skills
///                        (a learner with 1 course covering 3 skills should rank behind
///                        one with 3 courses covering 3 skills - breadth of evidence matters).
///   - Mandatory courses: rewards baseline compliance, independent of the requested skills.
///   - Completion depth:  a general "how much have they finished" signal so a learner with
///                        zero exact matches but a strong track record still ranks above
///                        someone with no history at all (see the "weak matches still rank"
///                        hint shown in the UI when there are no rows yet).
/// Tiers (Low &lt; 10 &lt;= Medium &lt; 15 &lt;= High) are calibrated against the 0-20 range, not
/// against the number of requested skills, so results stay comparable across searches.
///
/// <see cref="MatchedSkills"/>/<see cref="MissingSkills"/>/<see cref="MatchedCourseTitles"/>/
/// <see cref="CompletedCourseCount"/> are the raw (uncapped) evidence behind the score - callers
/// that need "why did this candidate rank here" (e.g. a recruiter-facing UI) should read these,
/// not the *Score fields, which are deliberately clamped for ranking and would under-report a
/// learner who matched more than <see cref="MaxPerCategory"/> skills/courses.
///
/// Matching starts from a case-insensitive substring match of each requested skill against
/// completed-course titles - the same simple rule as always, and still the *only* signal when
/// AI skill-tag extraction is disabled (the default in this repo - no API key is committed).
/// When extraction is enabled (see the AnthropicSkillTagging config section and
/// <see cref="LmsTracker.Api.Infrastructure.Ai.ISkillTagExtractor"/>), courses are also tagged
/// with LLM-extracted skill keywords at sync time, so a course titled "Container Orchestration
/// with K8s" tagged ["kubernetes", "container-orchestration"] now matches a request for
/// "kubernetes" even though the title itself never contains that word. Tag extraction only
/// enriches data - it is never consulted by this scoring/ranking logic itself, which stays
/// deterministic, has zero external dependencies, and is fully unit-tested with no tags present.
/// </summary>
public sealed class SkillMatchScoringService
{
    private const int MaxPerCategory = 5;

    private static bool CourseMatchesSkill(SkillMatchCourseSummary course, string skill) =>
        course.Title.Contains(skill, StringComparison.OrdinalIgnoreCase)
        || (course.SkillTags?.Any(tag => TagMatchesSkill(tag, skill)) ?? false);

    private static bool TagMatchesSkill(string? tag, string skill) =>
        !string.IsNullOrWhiteSpace(tag)
        && (tag.Contains(skill, StringComparison.OrdinalIgnoreCase) || skill.Contains(tag, StringComparison.OrdinalIgnoreCase));

    public SkillMatchEvaluation Evaluate(IEnumerable<SkillMatchCourseSummary> completedCourses, IEnumerable<string> requestedSkills)
    {
        var requested = requestedSkills
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
        {
            return new SkillMatchEvaluation(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                0,
                0,
                0,
                0,
                0,
                0,
                "Low",
                Array.Empty<string>());
        }

        var completedCourseList = completedCourses as IReadOnlyList<SkillMatchCourseSummary> ?? completedCourses.ToArray();

        var matchedCourseTitles = completedCourseList
            .Where(course => requested.Any(skill => CourseMatchesSkill(course, skill)))
            .Select(course => course.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedSkills = requested
            .Where(skill => completedCourseList.Any(course => CourseMatchesSkill(course, skill)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missingSkills = requested
            .Except(matchedSkills, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var completedCourseCount = completedCourseList
            .Select(c => c.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var mandatoryCourseCount = completedCourseList.Count(c => c.IsMandatory);

        var matchedSkillScore = Math.Min(matchedSkills.Length, MaxPerCategory);
        var matchedCourseScore = Math.Min(matchedCourseTitles.Length, MaxPerCategory);
        var mandatoryCourseScore = Math.Min(mandatoryCourseCount, MaxPerCategory);
        var completedCourseScore = Math.Min(completedCourseCount, MaxPerCategory);
        var totalScore = matchedSkillScore + matchedCourseScore + mandatoryCourseScore + completedCourseScore;

        var tier = totalScore switch
        {
            >= 15 => "High",
            >= 10 => "Medium",
            _ => "Low"
        };

        var breakdown = new[]
        {
            $"Skills {matchedSkillScore}/{MaxPerCategory}",
            $"Courses {matchedCourseScore}/{MaxPerCategory}",
            $"Mandatory {mandatoryCourseScore}/{MaxPerCategory}",
            $"Depth {completedCourseScore}/{MaxPerCategory}"
        };

        return new SkillMatchEvaluation(
            matchedSkills,
            missingSkills,
            matchedCourseTitles,
            completedCourseCount,
            matchedSkillScore,
            matchedCourseScore,
            mandatoryCourseScore,
            completedCourseScore,
            totalScore,
            tier,
            breakdown);
    }
}
