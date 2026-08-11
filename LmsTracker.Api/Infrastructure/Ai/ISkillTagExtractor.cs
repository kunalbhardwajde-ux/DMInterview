namespace LmsTracker.Api.Infrastructure.Ai;

/// <summary>One course to extract skill tags for. Key is caller-defined (e.g. Course.Id) and is echoed back unchanged in the result.</summary>
public sealed record CourseTagExtractionInput(string Key, string Title);

/// <summary>
/// Enriches course titles with normalized skill/technology tags via an LLM, so skill-match
/// search can find a course whose title doesn't literally contain the requested keyword.
/// This is enrichment only - implementations must never throw for API/network failures and
/// must return an empty result instead, since callers use this to populate optional metadata,
/// not to gate course sync. The actual skill-match ranking never depends on this interface -
/// see <see cref="LmsTracker.Api.Modules.Reports.SkillMatchScoringService"/>.
/// </summary>
public interface ISkillTagExtractor
{
    /// <summary>False when the feature is turned off or missing required config (e.g. no API key) - mirrors the IsEnabled pattern on ILearningProviderClient.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns a map of input Key to normalized tags. Keys with no confident tags are omitted
    /// rather than mapped to an empty list. Returns an empty dictionary (never throws) if
    /// disabled, given no input, or the extraction call fails for any reason.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ExtractSkillTagsAsync(
        IReadOnlyList<CourseTagExtractionInput> courses,
        CancellationToken cancellationToken);
}
