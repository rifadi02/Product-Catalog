namespace Catalog.Api.Filters;

public sealed class FluentValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            if (result.IsValid) continue;

            foreach (var group in result.Errors.GroupBy(e => Camel(e.PropertyName)))
            {
                var messages = group.Select(e => e.ErrorMessage).ToArray();

                errors[group.Key] = errors.TryGetValue(group.Key, out var existing)
                    ? [.. existing, .. messages]
                    : messages;
            }
        }

        if (errors.Count > 0)
        {
            context.Result = Catalog.Domain.Common.Error
                .Validation("validation_failed", "One or more validation errors occurred.", errors)
                .ToProblem(context.HttpContext);
            return;
        }

        await next();
    }

    private static string Camel(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
