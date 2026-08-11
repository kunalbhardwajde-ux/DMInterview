using System.Security.Cryptography;
using System.Text;
using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;
using LmsTracker.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LmsTracker.Api.Modules.Auth;

/// <summary>
/// Issues short-lived bearer tokens for two identities: a single shared Manager access code
/// (broad access to every admin endpoint) and a per-employee-code Learner login (read-only,
/// scoped to that learner's own data via LearnersModule's /me and AssignmentsModule's /mine).
/// Employee-code login is an identity lookup, not a secret proof of identity - the same trust
/// model the old client-side-only "individual persona" flow always used, just now enforced
/// server-side instead of trusting whatever employee code the browser typed in. See the
/// README's Authentication section for the explicit scope boundary this still doesn't cross
/// (no password store, no audit-quality identity assurance for either role).
/// </summary>
public sealed class AuthModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Deliberately NOT behind RequireAuthorization() - this is how a client gets a token.
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, IOptions<AuthOptions> options, LmsDbContext db, CancellationToken ct) =>
        {
            var validationErrors = RequestValidationHelper.Validate(request);
            if (validationErrors.Count > 0)
            {
                return ApiResult<LoginResponse>.ValidationFailed(validationErrors).ToHttpResult();
            }

            var auth = options.Value;

            if (!string.IsNullOrWhiteSpace(request.AccessCode))
            {
                if (!IsValidAccessCode(request.AccessCode, auth.ManagerAccessCode))
                {
                    return ApiResult<LoginResponse>.Fail("Invalid access code.", "UNAUTHORIZED").ToHttpResult();
                }

                var (managerToken, managerExpiresAtUtc) = JwtTokenIssuer.IssueManagerToken(auth);
                return ApiResult<LoginResponse>.Ok(new LoginResponse(managerToken, managerExpiresAtUtc, "Manager")).ToHttpResult();
            }

            var normalizedCode = request.EmployeeCode!.Trim().ToUpperInvariant();
            var learner = await db.Learners.AsNoTracking().FirstOrDefaultAsync(l => l.EmployeeCode == normalizedCode, ct);
            if (learner is null)
            {
                return ApiResult<LoginResponse>.Fail("Employee code not found.", "UNAUTHORIZED").ToHttpResult();
            }

            var (learnerToken, learnerExpiresAtUtc) = JwtTokenIssuer.IssueLearnerToken(auth, learner.Id, learner.EmployeeCode);
            return ApiResult<LoginResponse>.Ok(new LoginResponse(learnerToken, learnerExpiresAtUtc, "Learner")).ToHttpResult();
        }).RequireRateLimiting("login");
    }

    // Constant-time comparison so response timing can't be used to brute-force the access code
    // character-by-character. Lengths differ almost always (real code vs guess), so compare a
    // fixed-size hash of each input instead of the raw bytes - avoids the exception
    // CryptographicOperations.FixedTimeEquals throws on mismatched-length inputs, without
    // reintroducing a length-based timing signal.
    private static bool IsValidAccessCode(string provided, string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
