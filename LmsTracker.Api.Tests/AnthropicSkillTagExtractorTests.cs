using System.Net;
using System.Text;
using LmsTracker.Api.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LmsTracker.Api.Tests;

/// <summary>
/// Exercises AnthropicSkillTagExtractor entirely against a fake HttpMessageHandler - no real
/// network call is ever made. No API key is configured in this environment, so these tests
/// prove the request shape (auth headers, structured-output schema) and the fail-open behavior
/// (disabled / HTTP error / malformed response all degrade to an empty result) rather than a
/// live round trip against the real Anthropic API.
/// </summary>
public sealed class AnthropicSkillTagExtractorTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        // Captured eagerly because AnthropicSkillTagExtractor disposes the request (and its
        // content) as soon as SendAsync returns - reading LastRequest.Content after the fact
        // would hit a disposed StringContent.
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static AnthropicSkillTagExtractor CreateExtractor(
        StubHttpMessageHandler handler,
        AnthropicSkillTagExtractorOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        var effectiveOptions = options ?? new AnthropicSkillTagExtractorOptions { Enabled = true, ApiKey = "test-key" };
        return new AnthropicSkillTagExtractor(httpClient, Options.Create(effectiveOptions), NullLogger<AnthropicSkillTagExtractor>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmptyWithoutCallingHttp_WhenDisabled()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Must not call HTTP when disabled."));
        var extractor = CreateExtractor(handler, new AnthropicSkillTagExtractorOptions { Enabled = false, ApiKey = "test-key" });

        var result = await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        Assert.Empty(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmptyWithoutCallingHttp_WhenApiKeyMissing()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Must not call HTTP without an API key."));
        var extractor = CreateExtractor(handler, new AnthropicSkillTagExtractorOptions { Enabled = true, ApiKey = "" });

        var result = await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        Assert.Empty(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmptyWithoutCallingHttp_WhenNoCoursesGiven()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Must not call HTTP with zero courses."));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync([], CancellationToken.None);

        Assert.Empty(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ParsesStructuredOutputAndNormalizesTags()
    {
        const string responseBody = """
        {
          "content": [
            { "type": "text", "text": "{\"courses\":[{\"id\":\"id-1\",\"tags\":[\"Kubernetes\",\" kubernetes \",\"Container-Orchestration\",\"\"],\"confidence\":\"high\"}]}" }
          ]
        }
        """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync(
            [new CourseTagExtractionInput("id-1", "Container Orchestration with K8s")], CancellationToken.None);

        var tags = Assert.Single(result).Value;
        // Duplicate ("kubernetes" appears twice with different whitespace) and blank entries are
        // removed, and everything is lowercased - the JSON schema guarantees shape, not quality.
        Assert.Equal(["kubernetes", "container-orchestration"], tags);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_OmitsCourse_WhenModelReturnsNoUsableTags()
    {
        const string responseBody = """
        { "content": [ { "type": "text", "text": "{\"courses\":[{\"id\":\"id-1\",\"tags\":[],\"confidence\":\"high\"}]}" } ] }
        """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync(
            [new CourseTagExtractionInput("id-1", "Vague Title")], CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("low")]
    [InlineData(null)]
    public async Task ExtractSkillTagsAsync_DropsTags_WhenConfidenceIsNotHigh(string? confidence)
    {
        var confidenceJson = confidence is null ? "null" : $"\"{confidence}\"";
        var responseBody = $$"""
        { "content": [ { "type": "text", "text": "{\"courses\":[{\"id\":\"id-1\",\"tags\":[\"kubernetes\"],\"confidence\":{{confidenceJson}}}]}" } ] }
        """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync(
            [new CourseTagExtractionInput("id-1", "K8s Fundamentals")], CancellationToken.None);

        // Self-reported confidence below "high" means the tags are not trusted enough to
        // auto-apply - they're discarded (retried on the next sync), not stored anywhere.
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_KeepsHighConfidenceCourse_AndDropsLowConfidenceCourse_InTheSameBatch()
    {
        const string responseBody = """
        { "content": [ { "type": "text", "text": "{\"courses\":[{\"id\":\"id-1\",\"tags\":[\"kubernetes\"],\"confidence\":\"high\"},{\"id\":\"id-2\",\"tags\":[\"guess\"],\"confidence\":\"low\"}]}" } ] }
        """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync(
            [
                new CourseTagExtractionInput("id-1", "Advanced Kubernetes Operations"),
                new CourseTagExtractionInput("id-2", "Vague Title"),
            ],
            CancellationToken.None);

        var kept = Assert.Single(result);
        Assert.Equal("id-1", kept.Key);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmpty_WhenHttpCallFails()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmpty_WhenResponseBodyIsMalformed()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "not json"));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_ReturnsEmpty_WhenModelTextIsNotValidJson()
    {
        const string responseBody = """
        { "content": [ { "type": "text", "text": "sorry, I can't help with that" } ] }
        """;
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler);

        var result = await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_SendsAuthHeadersAndStructuredOutputSchema()
    {
        const string responseBody = """{ "content": [ { "type": "text", "text": "{\"courses\":[]}" } ] }""";
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler, new AnthropicSkillTagExtractorOptions
        {
            Enabled = true,
            ApiKey = "sk-test-key",
            Model = "claude-haiku-4-5",
            BaseUrl = "https://api.anthropic.com",
        });

        await extractor.ExtractSkillTagsAsync([new CourseTagExtractionInput("id-1", "Kubernetes Basics")], CancellationToken.None);

        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal("sk-test-key", request!.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
        Assert.Equal(new Uri("https://api.anthropic.com/v1/messages"), request.RequestUri);

        var requestBody = handler.LastRequestBody;
        Assert.NotNull(requestBody);
        Assert.Contains("\"model\":\"claude-haiku-4-5\"", requestBody);
        Assert.Contains("\"type\":\"json_schema\"", requestBody);
        Assert.Contains("\"additionalProperties\":false", requestBody);
        Assert.Contains("\"confidence\"", requestBody);
    }

    [Fact]
    public async Task ExtractSkillTagsAsync_CapsBatchSize_ToMaxCoursesPerRequest()
    {
        const string responseBody = """{ "content": [ { "type": "text", "text": "{\"courses\":[]}" } ] }""";
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, responseBody));
        var extractor = CreateExtractor(handler, new AnthropicSkillTagExtractorOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            MaxCoursesPerRequest = 2,
        });

        var courses = Enumerable.Range(1, 5)
            .Select(i => new CourseTagExtractionInput($"id-{i}", $"Course {i}"))
            .ToList();

        await extractor.ExtractSkillTagsAsync(courses, CancellationToken.None);

        var requestBody = handler.LastRequestBody;
        Assert.NotNull(requestBody);
        Assert.Contains("id-1", requestBody);
        Assert.Contains("id-2", requestBody);
        Assert.DoesNotContain("id-3", requestBody);
    }
}
