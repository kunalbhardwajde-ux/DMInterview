using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LmsTracker.Api.Infrastructure.LearningProviders;
using Microsoft.Extensions.Options;
using static LmsTracker.Api.Infrastructure.LearningProviders.ProviderJsonHelpers;

namespace LmsTracker.Api.Infrastructure.Udemy;

public interface IUdemyBusinessClient : ILearningProviderClient;

public sealed class UdemyBusinessClient(HttpClient httpClient, IOptions<UdemyBusinessOptions> options)
    : LearningProviderClientBase(httpClient), IUdemyBusinessClient
{
    private readonly UdemyBusinessOptions _options = options.Value;

    protected override string BaseUrl => _options.BaseUrl;

    protected override void ApplyAuthorization(HttpRequestMessage request)
    {
        if (_options.UseBasicAuth)
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
        }
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.BaseUrl)
        && !string.IsNullOrWhiteSpace(_options.CatalogSearchPathTemplate)
        && (_options.UseBasicAuth
            ? !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret)
            : !string.IsNullOrWhiteSpace(_options.BearerToken));

    public async Task<IReadOnlyList<LearningCourseItem>> SearchCoursesAsync(string query, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return [];
        }

        var path = _options.CatalogSearchPathTemplate.Replace("{query}", Uri.EscapeDataString(query ?? string.Empty));
        var response = await SendAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var result = new List<LearningCourseItem>();
        var rows = GetArray(json.RootElement, "results") ?? GetArray(json.RootElement, "data") ?? [];

        foreach (var row in rows)
        {
            var externalId =
                GetString(row, "id")
                ?? GetString(row, "course_id")
                ?? GetString(row, "_class")
                ?? string.Empty;

            var title =
                GetString(row, "title")
                ?? GetString(row, "name")
                ?? string.Empty;

            var launchUrl =
                GetString(row, "url")
                ?? GetString(row, "web_url")
                ?? GetString(row, "landing_page")
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            result.Add(new LearningCourseItem(externalId, title, launchUrl));
        }

        return result;
    }

    public async Task<int?> GetProgressPercentAsync(string userEmail, string courseExternalId, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(_options.ProgressPathTemplate))
        {
            return null;
        }

        var path = _options.ProgressPathTemplate
            .Replace("{userEmail}", Uri.EscapeDataString(userEmail))
            .Replace("{courseExternalId}", Uri.EscapeDataString(courseExternalId));

        var response = await SendAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var candidate =
            GetDouble(json.RootElement, "progress_percent")
            ?? GetDouble(json.RootElement, "percent_complete")
            ?? GetDouble(json.RootElement, "completion_ratio") * 100
            ?? GetDouble(json.RootElement, "progress") * 100;

        if (candidate is null)
        {
            var nested = GetObject(json.RootElement, "results") ?? GetObject(json.RootElement, "data");
            candidate = nested is null
                ? null
                : GetDouble(nested.Value, "progress_percent")
                    ?? GetDouble(nested.Value, "percent_complete")
                    ?? GetDouble(nested.Value, "completion_ratio") * 100
                    ?? GetDouble(nested.Value, "progress") * 100;
        }

        if (candidate is null)
        {
            return null;
        }

        return Math.Clamp((int)Math.Round(candidate.Value, MidpointRounding.AwayFromZero), 0, 100);
    }
}
