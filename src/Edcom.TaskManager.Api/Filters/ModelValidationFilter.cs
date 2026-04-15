using Microsoft.AspNetCore.Mvc.Filters;

namespace Edcom.TaskManager.Api.Filters;

public class ModelValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            context.Result = new BadRequestObjectResult(new { errors });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
