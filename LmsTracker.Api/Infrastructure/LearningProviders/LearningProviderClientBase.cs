using System.Net.Http.Headers;

namespace LmsTracker.Api.Infrastructure.LearningProviders;

/// <summary>
/// Shared HTTP plumbing for learning-provider clients (request building, base-URL joining,
/// Accept header). Providers legitimately differ in auth scheme and response JSON shape -
/// both stay in the concrete client so this base class only owns what's actually identical.
/// </summary>
public abstract class LearningProviderClientBase(HttpClient httpClient)
{
    protected abstract string BaseUrl { get; }

    protected abstract void ApplyAuthorization(HttpRequestMessage request);

    protected async Task<HttpResponseMessage> SendAsync(string relativePath, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(relativePath));
        ApplyAuthorization(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await httpClient.SendAsync(request, ct);
    }

    private Uri BuildUri(string path)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var p = path.StartsWith('/') ? path : "/" + path;
        return new Uri(baseUrl + p);
    }
}
