namespace LmsTracker.Api.Infrastructure.Udemy;

public sealed class UdemyBusinessOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public bool UseBasicAuth { get; set; } = true;

    // Example: /api-2.0/organizations/me/courses/list/?search={query}
    public string CatalogSearchPathTemplate { get; set; } = string.Empty;

    // Example: /api-2.0/organizations/me/users/{userEmail}/courses/{courseExternalId}
    public string ProgressPathTemplate { get; set; } = string.Empty;
}
