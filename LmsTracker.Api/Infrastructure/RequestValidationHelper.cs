using System.ComponentModel.DataAnnotations;

namespace LmsTracker.Api.Infrastructure;

public static class RequestValidationHelper
{
    public static IReadOnlyList<string> Validate(object model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(model, context, results, validateAllProperties: true))
        {
            return results
                .Select(result => result.ErrorMessage ?? "Validation failed.")
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return Array.Empty<string>();
    }
}
