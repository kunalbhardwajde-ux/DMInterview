namespace LmsTracker.Api.Infrastructure.Auth;

public sealed class AuthOptions
{
    // HMAC-SHA256 signing key. Must be at least 32 bytes (256 bits) - JwtTokenIssuer validates
    // this at startup rather than letting token issuance fail unpredictably at request time.
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "LmsTracker";
    public string Audience { get; set; } = "LmsTracker.Clients";

    // A single shared credential, not a per-user identity store. This closes "any HTTP client
    // can call this API with zero credentials" - it does not add per-employee login. See the
    // README's Authentication section for the explicit scope boundary.
    public string ManagerAccessCode { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 480;
}
