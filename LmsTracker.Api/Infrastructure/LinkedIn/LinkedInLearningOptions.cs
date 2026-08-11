namespace LmsTracker.Api.Infrastructure.LinkedIn;

public sealed class LinkedInLearningOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.linkedin.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public bool UseBearerToken { get; set; } = true;

    // Example: /v2/learningAssets?q=criteria&keywords={query}
    public string CatalogSearchPathTemplate { get; set; } = string.Empty;

    // Example: /v2/learningActivityReports?q=criteria&learnerEmail={userEmail}&asset={courseExternalId}
    public string ProgressPathTemplate { get; set; } = string.Empty;
}
