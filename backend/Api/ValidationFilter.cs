using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MinhaEscala.Infrastructure;

public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values.Where(x => x is not null))
        {
            var type = typeof(IValidator<>).MakeGenericType(arg!.GetType());
            if (services.GetService(type) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            if (!result.IsValid) {
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(result.Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray())) { Title = "Revise os campos informados." }); return;
            }
        }
        await next();
    }
}
