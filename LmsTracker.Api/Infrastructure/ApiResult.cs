namespace LmsTracker.Api.Infrastructure;

/// <summary>
/// Standardized API response wrapper for consistent error and success handling.
/// </summary>
public sealed record ApiResult<T>(
    bool Success,
    T? Data,
    string? Message,
    string? Code,
    IReadOnlyList<string> Errors)
{
    public static ApiResult<T> Ok(T data, string? message = null) =>
        new(true, data, message, null, Array.Empty<string>());

    public static ApiResult<T> Fail(string message, string? code = null, IReadOnlyList<string>? errors = null) =>
        new(false, default, message, code, errors ?? Array.Empty<string>());

    public static ApiResult<T> Fail(IReadOnlyList<string> errors, string code = "VALIDATION_FAILED") =>
        new(false, default, null, code, errors);

    public static ApiResult<T> ValidationFailed(IReadOnlyList<string> errors) =>
        new(false, default, "Request validation failed.", "VALIDATION_FAILED", errors);

    public static ApiResult<T> NotFound(string resource) =>
        new(false, default, $"{resource} not found.", "NOT_FOUND", Array.Empty<string>());

    public static ApiResult<T> Error(string message, string code = "INTERNAL_ERROR") =>
        new(false, default, message, code, Array.Empty<string>());
}

/// <summary>
/// Non-generic version for endpoints that don't return data.
/// </summary>
public sealed record ApiResult(
    bool Success,
    string? Message,
    string? Code,
    IReadOnlyList<string> Errors)
{
    public static ApiResult Ok(string? message = null) =>
        new(true, message, null, Array.Empty<string>());

    public static ApiResult Fail(string message, string? code = null, IReadOnlyList<string>? errors = null) =>
        new(false, message, code, errors ?? Array.Empty<string>());

    public static ApiResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(false, "Request validation failed.", "VALIDATION_FAILED", errors);

    public static ApiResult NotFound(string resource) =>
        new(false, $"{resource} not found.", "NOT_FOUND", Array.Empty<string>());

    public static ApiResult Error(string message, string code = "INTERNAL_ERROR") =>
        new(false, message, code, Array.Empty<string>());
}
