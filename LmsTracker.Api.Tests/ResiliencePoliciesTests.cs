using System.Net;
using LmsTracker.Api.Infrastructure;
using Polly.CircuitBreaker;

namespace LmsTracker.Api.Tests;

/// <summary>
/// Proves the retry/circuit-breaker policies actually change request behavior, not just that
/// they compile and get attached in Program.cs. Executes the policy directly against a fake
/// delegate (no HttpClient/network involved) so these run fast and deterministically.
/// </summary>
public sealed class ResiliencePoliciesTests
{
    [Fact]
    public async Task Retry_RetriesTransientFailures_ThenSucceeds()
    {
        var attempts = 0;
        var policy = ResiliencePolicies.Retry();

        var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        Assert.Equal(3, attempts);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Retry_GivesUpAfterConfiguredAttempts_AndReturnsTheLastFailureResponse()
    {
        var attempts = 0;
        var policy = ResiliencePolicies.Retry();

        var response = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        // 1 initial attempt + 2 retries = 3 total, matching ResiliencePolicies.Retry()'s retryCount.
        Assert.Equal(3, attempts);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterConsecutiveFailures_AndFailsFastWithoutCallingTheDelegate()
    {
        var policy = ResiliencePolicies.CircuitBreaker();
        var calls = 0;
        Func<Task<HttpResponseMessage>> fail = () =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        };

        // handledEventsAllowedBeforeBreaking: 5 - the 5th failure trips the breaker open.
        for (var i = 0; i < 5; i++)
        {
            await policy.ExecuteAsync(fail);
        }

        Assert.Equal(5, calls);
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() => policy.ExecuteAsync(fail));
        // The breaker short-circuited - the delegate itself was never invoked a 6th time.
        Assert.Equal(5, calls);
    }

    [Fact]
    public async Task RetryWithCircuitBreaker_EachRetryAttemptRespectsAnAlreadyOpenBreaker()
    {
        // A fresh instance per client is required precisely because circuit state is shared
        // across every call through one policy instance - this proves that sharing.
        var wrapped = ResiliencePolicies.RetryWithCircuitBreaker();
        var calls = 0;
        Func<Task<HttpResponseMessage>> fail = () =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        };

        // Each outer call makes up to 3 real attempts (1 + 2 retries); after enough of those,
        // the shared circuit breaker (threshold 5) opens and later attempts fail fast.
        await ExecuteUntilBrokenAsync(wrapped, fail);

        var callsAfterBreakerOpened = calls;
        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() => wrapped.ExecuteAsync(fail));
        Assert.Equal(callsAfterBreakerOpened, calls); // no new underlying call once broken
    }

    private static async Task ExecuteUntilBrokenAsync(Polly.IAsyncPolicy<HttpResponseMessage> policy, Func<Task<HttpResponseMessage>> fail)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                await policy.ExecuteAsync(fail);
            }
            catch (BrokenCircuitException<HttpResponseMessage>)
            {
                return;
            }
        }
    }
}
