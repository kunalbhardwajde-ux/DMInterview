namespace LmsTracker.Api.Infrastructure.Auth;

/// <summary>
/// Custom claim type names used on Learner tokens. Deliberately short, non-URI strings (unlike
/// System.Security.Claims.ClaimTypes' long URIs) - JwtSecurityTokenHandler's default
/// inbound/outbound claim-type map only rewrites those well-known long-form URIs, so a plain
/// string like this round-trips through issuance and validation unchanged, with no mapping
/// surprises to account for.
/// </summary>
public static class LmsClaimTypes
{
    public const string LearnerId = "learner_id";
}
