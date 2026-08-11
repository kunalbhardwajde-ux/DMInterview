using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LmsTracker.Api.Infrastructure.Auth;

public static class JwtTokenIssuer
{
    // HMAC-SHA256 requires a key of at least 256 bits; anything shorter throws at signing time
    // with a confusing error. Fail fast, at startup, with a message that says exactly what's wrong.
    public const int MinimumSigningKeyBytes = 32;

    public static (string Token, DateTime ExpiresAtUtc) IssueManagerToken(AuthOptions options) =>
        IssueToken(options,
        [
            new Claim(ClaimTypes.Role, "Manager"),
            new Claim(JwtRegisteredClaimNames.Sub, "manager"),
        ]);

    // Read-only, scoped identity: sub/learner_id both carry the learner's own id so every
    // Learner-only endpoint can filter strictly by "the id on this token," never a client-
    // supplied parameter. employee_code is included for display only (e.g. UI headers), not
    // authorization - LearnersModule/AssignmentsModule's /me and /mine both key off learner_id.
    public static (string Token, DateTime ExpiresAtUtc) IssueLearnerToken(AuthOptions options, Guid learnerId, string employeeCode) =>
        IssueToken(options,
        [
            new Claim(ClaimTypes.Role, "Learner"),
            new Claim(JwtRegisteredClaimNames.Sub, learnerId.ToString()),
            new Claim(LmsClaimTypes.LearnerId, learnerId.ToString()),
            new Claim("employee_code", employeeCode),
        ]);

    private static (string Token, DateTime ExpiresAtUtc) IssueToken(AuthOptions options, IReadOnlyList<Claim> claims)
    {
        ValidateSigningKey(options.SigningKey);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(options.TokenLifetimeMinutes);
        var allClaims = claims.Append(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: allClaims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public static void ValidateSigningKey(string signingKey)
    {
        if (Encoding.UTF8.GetByteCount(signingKey ?? string.Empty) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Auth:SigningKey must be at least {MinimumSigningKeyBytes} bytes (256 bits) for HMAC-SHA256. " +
                "Configure a real value via environment variable or user-secrets - see README's Authentication section.");
        }
    }
}
