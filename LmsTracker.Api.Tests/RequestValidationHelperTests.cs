using LmsTracker.Api.Contracts;
using LmsTracker.Api.Infrastructure;

namespace LmsTracker.Api.Tests;

public sealed class RequestValidationHelperTests
{
    [Fact]
    public void Validate_WithNullModel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RequestValidationHelper.Validate(null!));
    }

    [Fact]
    public void Validate_WithValidDepartmentRequest_ReturnsEmpty()
    {
        var request = new CreateDepartmentRequest("Engineering", "ENG");
        var errors = RequestValidationHelper.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithInvalidDepartmentRequest_ReturnsErrors()
    {
        var request = new CreateDepartmentRequest("E", "");
        var errors = RequestValidationHelper.Validate(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Name"));
    }

    [Fact]
    public void Validate_WithValidTeamRequest_ReturnsEmpty()
    {
        var request = new CreateTeamRequest(
            "Engineering",
            Guid.NewGuid(),
            "John Doe",
            "john@example.com");
        var errors = RequestValidationHelper.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithInvalidEmailInTeamRequest_ReturnsErrors()
    {
        var request = new CreateTeamRequest(
            "Engineering",
            Guid.NewGuid(),
            "John Doe",
            "not-an-email");
        var errors = RequestValidationHelper.Validate(request);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_WithValidLearnerRequest_ReturnsEmpty()
    {
        var request = new CreateLearnerRequest(
            "EMP001",
            "John Learner",
            "john@example.com",
            "Engineer",
            null);
        var errors = RequestValidationHelper.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMissingRequiredFieldsInLearnerRequest_ReturnsErrors()
    {
        var request = new CreateLearnerRequest(
            "",
            "",
            "invalid-email",
            "",
            null);
        var errors = RequestValidationHelper.Validate(request);
        Assert.NotEmpty(errors);
        Assert.True(errors.Count >= 3);
    }
}
