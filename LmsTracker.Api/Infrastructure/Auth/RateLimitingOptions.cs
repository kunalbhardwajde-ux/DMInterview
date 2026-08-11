namespace LmsTracker.Api.Infrastructure.Auth;

public sealed class RateLimitingOptions
{
    // Per-client-IP fixed window on POST /api/auth/login - see Program.cs's "login" rate limit
    // policy. Configurable (rather than hardcoded) so the integration test factory can raise it
    // for its shared fixture, while a dedicated test spins up its own factory with a small limit
    // to actually exercise the 429 path.
    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
}
