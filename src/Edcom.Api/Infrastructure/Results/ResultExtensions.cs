using Microsoft.AspNetCore.Mvc;

namespace Edcom.Api.Infrastructure.Results;

/// <summary>
/// Extension methods for converting Result<T> to HTTP responses.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Convert Result<T> to IActionResult for controller responses.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, object? okResponseValue = null) =>
        result.Match(
            onSuccess: data =>
                (IActionResult)new OkObjectResult(okResponseValue ?? data),
            onFailure: failure =>
                (IActionResult)new BadRequestObjectResult(new { code = failure.Code, message = failure.Message }),
            onUnauthorized: unauth =>
                (IActionResult)new UnauthorizedObjectResult(new { message = unauth.Message }),
            onNotFound: notFound =>
                (IActionResult)new NotFoundObjectResult(new { entity = notFound.Entity, message = "Not found" }),
            onConflict: conflict =>
                (IActionResult)new ConflictObjectResult(new { message = conflict.Message }));

    /// <summary>Convert Result<T> to IActionResult, specifying custom OK response.</summary>
    public static IActionResult ToActionResult<T, TResponse>(
        this Result<T> result,
        Func<T, TResponse> mapSuccess) =>
        result.Match(
            onSuccess: data => (IActionResult)new OkObjectResult(mapSuccess(data)),
            onFailure: failure =>
                (IActionResult)new BadRequestObjectResult(new { code = failure.Code, message = failure.Message }),
            onUnauthorized: unauth =>
                (IActionResult)new UnauthorizedObjectResult(new { message = unauth.Message }),
            onNotFound: notFound =>
                (IActionResult)new NotFoundObjectResult(new { entity = notFound.Entity }),
            onConflict: conflict =>
                (IActionResult)new ConflictObjectResult(new { message = conflict.Message }));

    /// <summary>Map success value to different type.</summary>
    public static Result<TNew> Map<T, TNew>(this Result<T> result, Func<T, TNew> map) =>
        result.Match(
            onSuccess: data => Result<TNew>.Success(map(data)),
            onFailure: f => Result<TNew>.Failure(f.Code, f.Message, f.Data),
            onUnauthorized: u => Result<TNew>.Unauthorized(u.Message),
            onNotFound: n => Result<TNew>.NotFound(n.Entity),
            onConflict: c => Result<TNew>.Conflict(c.Message));

    /// <summary>Chain async operations: if first succeeds, run second.</summary>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Result<T> result,
        Func<T, Task<Result<TNew>>> next) =>
        await result.Match(
            onSuccess: async data => await next(data),
            onFailure: f => Task.FromResult(Result<TNew>.Failure(f.Code, f.Message, f.Data)),
            onUnauthorized: u => Task.FromResult(Result<TNew>.Unauthorized(u.Message)),
            onNotFound: n => Task.FromResult(Result<TNew>.NotFound(n.Entity)),
            onConflict: c => Task.FromResult(Result<TNew>.Conflict(c.Message)));
}
