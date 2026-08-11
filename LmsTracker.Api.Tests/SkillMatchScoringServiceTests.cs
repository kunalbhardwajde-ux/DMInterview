using LmsTracker.Api.Modules.Reports;

namespace LmsTracker.Api.Tests;

public sealed class SkillMatchScoringServiceTests
{
    private readonly SkillMatchScoringService _service = new();

    [Fact]
    public void Evaluate_CapsEachCategoryAtFiveAndHighlightsHighFit()
    {
        var completedCourses = new[]
        {
            new SkillMatchCourseSummary("Cloud Security Foundations", IsMandatory: true),
            new SkillMatchCourseSummary("Advanced Cloud Architecture", IsMandatory: true),
            new SkillMatchCourseSummary("AI for Product Teams", IsMandatory: false),
            new SkillMatchCourseSummary("Security Operations Basics", IsMandatory: false),
            new SkillMatchCourseSummary("Data Privacy Essentials", IsMandatory: false),
            new SkillMatchCourseSummary("Communication Skills", IsMandatory: false)
        };

        var analysis = _service.Evaluate(completedCourses, new[] { "cloud", "security", "ai" });

        Assert.Equal(3, analysis.MatchedSkills.Count);
        Assert.Empty(analysis.MissingSkills);
        Assert.Equal(4, analysis.MatchedCourseTitles.Count);
        Assert.Equal(6, analysis.CompletedCourseCount);
        Assert.Equal(3, analysis.MatchedSkillScore);
        Assert.Equal(4, analysis.MatchedCourseScore);
        Assert.Equal(2, analysis.MandatoryCourseScore);
        Assert.Equal(5, analysis.CompletedCourseScore);
        Assert.Equal(14, analysis.TotalScore);
        Assert.Equal("Medium", analysis.Tier);
        Assert.Contains("Skills 3/5", analysis.ScoreBreakdown);
    }

    [Fact]
    public void Evaluate_ReportsRawUncappedCounts_WhenTheyExceedTheCategoryCap()
    {
        // Six matched courses/skills is above MaxPerCategory (5) - the *Score fields must clamp
        // to 5, but MatchedCourseTitles/MatchedSkills (the "why" evidence) must not, otherwise a
        // recruiter-facing UI silently under-reports a strong candidate's real match count.
        var completedCourses = new[]
        {
            new SkillMatchCourseSummary("Cloud Foundations", IsMandatory: false),
            new SkillMatchCourseSummary("Cloud Security", IsMandatory: false),
            new SkillMatchCourseSummary("Cloud Architecture", IsMandatory: false),
            new SkillMatchCourseSummary("Cloud Networking", IsMandatory: false),
            new SkillMatchCourseSummary("Cloud Storage", IsMandatory: false),
            new SkillMatchCourseSummary("Cloud Migration", IsMandatory: false),
        };

        var analysis = _service.Evaluate(completedCourses, new[] { "cloud" });

        Assert.Equal(6, analysis.MatchedCourseTitles.Count);
        Assert.Equal(6, analysis.CompletedCourseCount);
        Assert.Equal(5, analysis.MatchedCourseScore);
        Assert.Equal(5, analysis.CompletedCourseScore);
    }

    [Fact]
    public void Evaluate_MatchesViaSkillTags_WhenTitleDoesNotContainTheRequestedSkill()
    {
        // The exact "Container Orchestration with K8s" / "kubernetes" gap called out in the
        // class doc comment as the reason AI skill-tag extraction exists. Title-only matching
        // (SkillTags: null/empty) must NOT match this - proving the tags are what closes the gap.
        var completedCourses = new[]
        {
            new SkillMatchCourseSummary("Container Orchestration with K8s", IsMandatory: false, SkillTags: ["kubernetes", "container-orchestration"]),
        };

        var withTags = _service.Evaluate(completedCourses, new[] { "kubernetes" });
        Assert.Contains("kubernetes", withTags.MatchedSkills);
        Assert.Single(withTags.MatchedCourseTitles);

        var titleOnly = completedCourses.Select(c => c with { SkillTags = null });
        var withoutTags = _service.Evaluate(titleOnly, new[] { "kubernetes" });
        Assert.Empty(withoutTags.MatchedSkills);
    }

    [Fact]
    public void Evaluate_ReturnsLowTierWhenNoRelevantCoursesAreFound()
    {
        var completedCourses = new[]
        {
            new SkillMatchCourseSummary("Leadership Essentials", IsMandatory: true),
            new SkillMatchCourseSummary("Project Planning", IsMandatory: false)
        };

        var analysis = _service.Evaluate(completedCourses, new[] { "cloud", "security", "ai" });

        Assert.Empty(analysis.MatchedSkills);
        Assert.Equal(3, analysis.TotalScore);
        Assert.Equal("Low", analysis.Tier);
    }
}
