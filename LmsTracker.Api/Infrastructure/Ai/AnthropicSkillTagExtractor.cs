using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LmsTracker.Api.Infrastructure.Ai;

/// <summary>
/// Calls the Claude Messages API (model: <see cref="AnthropicSkillTagExtractorOptions.Model"/>,
/// default claude-haiku-4-5) to extract normalized skill tags from course titles at sync time.
///
/// Deliberately a bounded-classification task, not an agent: one request, structured output
/// (<c>output_config.format</c> with a JSON schema) guarantees a parseable
/// <c>{"courses":[{"id","tags"}]}</c> shape back, so there is no free-text parsing or retry-on-
/// bad-JSON loop. All courses touched by one sync are batched into a single request rather than
/// one call per course, bounding both latency and cost.
///
/// This is enrichment, not a dependency: disabled by default (no API key is configured in this
/// repo), and any failure - disabled, missing key, HTTP error, timeout, malformed response -
/// degrades to an empty result rather than throwing, so a course sync can never fail because the
/// AI call did. See <see cref="ISkillTagExtractor"/> for the contract this enforces.
///
/// The model also self-rates its confidence per course ("high"/"medium"/"low"); only "high" is
/// auto-applied (see NormalizeResult). This is a real but limited safeguard - self-reported LLM
/// confidence is a heuristic, not a calibrated probability - and there is still no human review
/// step before a "high"-confidence tag affects matching. A manager can clear a course's tags via
/// CourseCatalogService.ClearSkillTagsAsync if one turns out wrong; the next sync that touches
/// that course will attempt extraction again.
/// </summary>
public sealed class AnthropicSkillTagExtractor : ISkillTagExtractor
{
    private const string ApiVersion = "2023-06-01";
    private const int MaxTagsPerCourse = 6;
    private const int MaxTokens = 4096;

    private const string SystemPrompt =
        "You extract normalized skill and technology tags from online course titles for a " +
        "skill-matching search index used by recruiters. For each course, return 2 to 6 short " +
        "tags (lowercase, kebab-case, e.g. \"kubernetes\", \"data-visualization\", \"aws\") that " +
        "a recruiter might search for when looking for someone with this skill. Prefer specific " +
        "technologies, tools, and skills over generic words like \"course\", \"basics\", or " +
        "\"training\". Do not invent skills the title does not support - if a title is too vague " +
        "to tag confidently, return fewer tags rather than guessing.\n\n" +
        "Also rate your own confidence in the tags for each course as \"high\", \"medium\", or " +
        "\"low\": \"high\" means the title names the technology/skill explicitly or unambiguously " +
        "implies it (e.g. \"Advanced Kubernetes Operations\"); \"medium\" means you inferred the " +
        "skill from context or an abbreviation (e.g. \"K8s\", \"ML for Beginners\"); \"low\" means " +
        "the title is vague, generic, or you are largely guessing. Be conservative - only use " +
        "\"high\" when a human reading just the title would tag it the same way without additional context.";

    private static readonly JsonSerializerOptions RequestJsonOptions = new();

    private static readonly object ResponseSchema = new
    {
        type = "object",
        properties = new
        {
            courses = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        tags = new { type = "array", items = new { type = "string" } },
                        confidence = new { type = "string", @enum = new[] { "high", "medium", "low" } },
                    },
                    required = new[] { "id", "tags", "confidence" },
                    additionalProperties = false,
                },
            },
        },
        required = new[] { "courses" },
        additionalProperties = false,
    };

    private readonly HttpClient _httpClient;
    private readonly AnthropicSkillTagExtractorOptions _options;
    private readonly ILogger<AnthropicSkillTagExtractor> _logger;

    public AnthropicSkillTagExtractor(
        HttpClient httpClient,
        IOptions<AnthropicSkillTagExtractorOptions> options,
        ILogger<AnthropicSkillTagExtractor> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ExtractSkillTagsAsync(
        IReadOnlyList<CourseTagExtractionInput> courses,
        CancellationToken cancellationToken)
    {
        var empty = new Dictionary<string, IReadOnlyList<string>>();
        if (!IsEnabled || courses.Count == 0)
        {
            return empty;
        }

        var batch = courses.Take(_options.MaxCoursesPerRequest).ToList();

        try
        {
            using var request = BuildRequest(batch);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Skill-tag extraction request failed with status {StatusCode}; falling back to title-only matching for this sync.",
                    (int)response.StatusCode);
                return empty;
            }

            var payload = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>(RequestJsonOptions, cancellationToken);
            var text = payload?.Content?.FirstOrDefault(block => block.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Skill-tag extraction returned no text content; falling back to title-only matching for this sync.");
                return empty;
            }

            var parsed = JsonSerializer.Deserialize<ExtractionResponseBody>(text, RequestJsonOptions);
            if (parsed?.Courses is null)
            {
                return empty;
            }

            return NormalizeResult(parsed.Courses);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Includes timeouts (the typed HttpClient has a bounded timeout - see Program.cs)
            // and malformed/unparseable responses. Enrichment failing must never surface as a
            // course-sync failure, so this degrades exactly like "feature disabled".
            _logger.LogWarning(ex, "Skill-tag extraction failed; falling back to title-only matching for this sync.");
            return empty;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeResult(IReadOnlyList<ExtractedCourseTags> courses)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var entry in courses)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            // Self-reported LLM confidence is a soft heuristic, not a calibrated probability -
            // but it's a real, cheap signal, and gating on it is strictly better than trusting
            // every tag equally. Only "high" is auto-applied; "medium"/"low" are discarded
            // rather than silently trusted, matching CourseCatalogService only ever
            // re-attempting extraction for courses with zero tags (see its untagged-only
            // filter) - a discarded low-confidence result is retried next sync, not stuck.
            if (!string.Equals(entry.Confidence, "high", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The JSON schema guarantees the *shape* of the response (an array of strings per
            // course), not the *quality* - the model can still return blank strings, duplicates,
            // or inconsistent casing. Normalize before this ever reaches the database.
            var normalized = (entry.Tags ?? [])
                .Select(tag => tag?.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag!)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxTagsPerCourse)
                .ToArray();

            if (normalized.Length > 0)
            {
                result[entry.Id] = normalized;
            }
        }

        return result;
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<CourseTagExtractionInput> batch)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.BaseUrl), "/v1/messages"));
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var coursesJson = JsonSerializer.Serialize(
            batch.Select(c => new { id = c.Key, title = c.Title }),
            RequestJsonOptions);

        var body = new AnthropicMessageRequest(
            _options.Model,
            MaxTokens,
            SystemPrompt,
            [new AnthropicMessage("user", $"Extract skill tags for these courses:\n{coursesJson}")],
            new OutputConfig(new JsonOutputFormat(ResponseSchema)));

        var json = JsonSerializer.Serialize(body, RequestJsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    private sealed record AnthropicMessageRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
        [property: JsonPropertyName("output_config")] OutputConfig OutputConfig);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OutputConfig(
        [property: JsonPropertyName("format")] JsonOutputFormat Format);

    private sealed record JsonOutputFormat(
        [property: JsonPropertyName("schema")] object Schema)
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "json_schema";
    }

    private sealed record AnthropicMessageResponse(
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock>? Content);

    private sealed record AnthropicContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record ExtractionResponseBody(
        [property: JsonPropertyName("courses")] IReadOnlyList<ExtractedCourseTags>? Courses);

    private sealed record ExtractedCourseTags(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
        [property: JsonPropertyName("confidence")] string? Confidence);
}
