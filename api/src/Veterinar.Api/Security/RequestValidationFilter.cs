using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Veterinar.Application.Common;

namespace Veterinar.Api.Security;

public sealed class RequestValidationFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
            if (argument?.GetType().Namespace?.StartsWith("Veterinar.Application.", StringComparison.Ordinal) == true)
                Validate(argument);
        return next(context);
    }
    private static void Validate(object value)
    {
        foreach (var property in value.GetType().GetProperties())
        {
            var data = property.GetValue(value);
            foreach (var rule in property.GetCustomAttributes<ValidationAttribute>())
                if (!rule.IsValid(data)) throw new RequestException("Zkontrolujte povinná pole, délky textů a platnost zadaných hodnot.");
            if (data is IEnumerable list && data is not string)
            {
                var count = 0;
                foreach (var item in list)
                {
                    if (++count > 7 || item is null) throw new RequestException("Provozní doba může mít nejvýše sedm pravidel.");
                    Validate(item);
                }
            }
        }
    }
}
