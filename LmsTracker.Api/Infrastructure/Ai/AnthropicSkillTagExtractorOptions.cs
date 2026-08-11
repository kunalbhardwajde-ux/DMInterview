namespace LmsTracker.Api.Infrastructure.Ai;

public sealed class AnthropicSkillTagExtractorOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    // Safety bound on how many courses go into a single extraction request/prompt.
    public int MaxCoursesPerRequest { get; set; } = 40;
}
