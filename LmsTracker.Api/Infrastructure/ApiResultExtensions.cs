namespace LmsTracker.Api.Infrastructure;

public static class ApiResultExtensions
{
    public static IResult ToHttpResult<T>(this ApiResult<T> result) =>
        result.Success
            ? Results.Ok(result)
            : result.Code switch
            {
                "NOT_FOUND" => Results.NotFound(result),
                "VALIDATION_FAILED" => Results.BadRequest(result),
                "CONFLICT" => Results.Conflict(result),
                "UNAUTHORIZED" => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
                _ => Results.BadRequest(result),
            };

    public static IResult ToHttpResult(this ApiResult result) =>
        result.Success
            ? Results.Ok(result)
            : result.Code switch
            {
                "NOT_FOUND" => Results.NotFound(result),
                "VALIDATION_FAILED" => Results.BadRequest(result),
                "CONFLICT" => Results.Conflict(result),
                "UNAUTHORIZED" => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
                _ => Results.BadRequest(result),
            };
}
