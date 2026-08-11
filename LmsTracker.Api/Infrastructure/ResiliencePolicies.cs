using Polly;
using Polly.Extensions.Http;

namespace LmsTracker.Api.Infrastructure;

/// <summary>
/// Shared Polly resilience policies for the three external HTTP dependencies (Udemy, LinkedIn,
/// Anthropic). Each client already has a bounded HttpClient.Timeout (see Program.cs) so a slow
/// dependency can't hang a request forever - that guarantees a *definite* failure. This adds the
/// piece a bare timeout can't: retrying a genuinely transient blip (a dropped connection, one bad
/// 503) instead of surfacing it to the caller immediately, and stopping short of hammering a
/// dependency that's actually down instead of retrying every single request against it.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// 2 retries with exponential backoff (~200ms, ~400ms) on transient failures - 5xx, 408, and
    /// network-level exceptions (HttpPolicyExtensions.HandleTransientHttpError covers both).
    /// Deliberately not more: this call already sits inside a bounded HttpClient.Timeout, and
    /// each retry consumes part of that budget - too many retries just delays the definite
    /// failure the timeout exists to guarantee.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> Retry() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(2, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));

    /// <summary>
    /// After 5 consecutive transient failures, stop calling the dependency for 30 seconds and
    /// fail every request immediately instead of queuing up slow retries against something
    /// that's clearly down. Each call site (Udemy/LinkedIn/Anthropic) must get its own instance -
    /// circuit state is per-policy-instance, and sharing one across independent dependencies
    /// would trip all three together.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CircuitBreaker() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 5, durationOfBreak: TimeSpan.FromSeconds(30));

    /// <summary>Retry (outer) wrapping circuit breaker (inner) - the standard combination, so each individual retry attempt still respects the breaker's open state instead of only checking it once per call.</summary>
    public static IAsyncPolicy<HttpResponseMessage> RetryWithCircuitBreaker() =>
        Retry().WrapAsync(CircuitBreaker());
}
