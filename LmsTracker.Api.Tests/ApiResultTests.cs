using LmsTracker.Api.Infrastructure;

namespace LmsTracker.Api.Tests;

public sealed class ApiResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessResultWithData()
    {
        var data = new { id = 1, name = "Test" };
        var result = ApiResult<object>.Ok(data);

        Assert.True(result.Success);
        Assert.Equal(data, result.Data);
        Assert.Null(result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Fail_ReturnsFailedResult()
    {
        var result = ApiResult<string>.Fail("Something went wrong");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("Something went wrong", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationFailed_ReturnsFailedResultWithErrors()
    {
        var errors = new[] { "Field1 is required", "Field2 must be valid email" };
        var result = ApiResult<string>.ValidationFailed(errors);

        Assert.False(result.Success);
        Assert.Equal("VALIDATION_FAILED", result.Code);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void NotFound_ReturnsNotFoundResult()
    {
        var result = ApiResult<string>.NotFound("User");

        Assert.False(result.Success);
        Assert.Equal("NOT_FOUND", result.Code);
        Assert.Contains("User", result.Message);
    }

    [Fact]
    public void Non_Generic_Ok_ReturnsSuccessResult()
    {
        var result = ApiResult.Ok("Operation completed");

        Assert.True(result.Success);
        Assert.Equal("Operation completed", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Non_Generic_ValidationFailed_ReturnsFailedResult()
    {
        var errors = new[] { "Error1", "Error2" };
        var result = ApiResult.ValidationFailed(errors);

        Assert.False(result.Success);
        Assert.Equal("VALIDATION_FAILED", result.Code);
        Assert.Equal(errors, result.Errors);
    }
}
