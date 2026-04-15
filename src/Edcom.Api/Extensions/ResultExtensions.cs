using Edcom.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Edcom.Api.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Maps a failed Result to an IResult ProblemDetails response.
    /// Validation → 400, NotFound → 404, Conflict → 409, Failure → 500.
    /// </summary>
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to problem details.");

        return Results.Problem(
            statusCode: result.Error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound   => StatusCodes.Status404NotFound,
                ErrorType.Conflict   => StatusCodes.Status409Conflict,
                _                    => StatusCodes.Status500InternalServerError,
            },
            title: result.Error.Type.ToString(),
            detail: result.Error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = result.Error.Code,
            });
    }
}
