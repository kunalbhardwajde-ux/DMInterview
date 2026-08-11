using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;

namespace LmsTracker.Api.Tests;

/// <summary>
/// Covers the cross-field validation rules on request DTOs directly, via the single validation
/// entry point (RequestValidationHelper.Validate) every request goes through - CreateTeamRequest
/// and CreateAssignmentRequest implement IValidatableObject for exactly these rules (trim-aware
/// length checks, "at least one of two fields", conditional-required, enum-parse) since plain
/// DataAnnotations attributes can't express them. See Contracts/Dtos.cs.
/// </summary>
public sealed class RequestValidationTests
{
    [Fact]
    public void CreateAssignmentRequest_ReturnsErrors_WhenIncomplete()
    {
        var request = new CreateAssignmentRequest(Guid.Empty, null, null, "Invalid", null);

        var errors = RequestValidationHelper.Validate(request);

        Assert.Contains(errors, error => error.Contains("course", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("learner or a team", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Access type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateAssignmentRequest_IsValid_ForTemporaryAccessWithADueDate()
    {
        var request = new CreateAssignmentRequest(Guid.NewGuid(), Guid.NewGuid(), null, "Temporary", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));

        var errors = RequestValidationHelper.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateAssignmentRequest_RequiresDueDate_ForTemporaryAccess()
    {
        var request = new CreateAssignmentRequest(Guid.NewGuid(), Guid.NewGuid(), null, "Temporary", null);

        var errors = RequestValidationHelper.Validate(request);

        Assert.Contains(errors, error => error.Contains("due date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateTeamRequest_ReturnsErrors_WhenDetailsAreIncomplete()
    {
        var request = new CreateTeamRequest("A", Guid.NewGuid(), " ", "not-an-email");

        var errors = RequestValidationHelper.Validate(request);

        Assert.Contains(errors, error => error.Contains("team name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("manager name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateTeamRequest_IsValid_WithWellFormedInput()
    {
        var request = new CreateTeamRequest("Platform", Guid.NewGuid(), "Sam", "sam@example.com");

        var errors = RequestValidationHelper.Validate(request);

        Assert.Empty(errors);
    }
}
