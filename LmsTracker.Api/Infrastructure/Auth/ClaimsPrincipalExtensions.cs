using System.Security.Claims;

namespace LmsTracker.Api.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    // Learner-scoped endpoints (LearnersModule's /me, AssignmentsModule's /mine) must derive
    // identity from the token alone, never from a client-supplied route/query parameter - that's
    // what stops one Learner's token from reading another Learner's data.
    public static Guid? GetLearnerId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(LmsClaimTypes.LearnerId);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
