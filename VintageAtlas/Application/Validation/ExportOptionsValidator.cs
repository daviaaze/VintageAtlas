using VintageAtlas.Application.DTOs;

namespace VintageAtlas.Application.Validation;

/// <summary>
/// Validates ExportOptions before executing export.
/// Ensures business rules are enforced.
/// </summary>
public class ExportOptionsValidator : IValidator<ExportOptions>
{
    public ValidationResult Validate(ExportOptions options)
    {
        var result = new ValidationResult();

        if (options == null)
        {
            result.AddError("Export options cannot be null");
            return result;
        }

        // Business rule: StopOnDone requires SaveMode
        if (options.StopOnDone && !options.SaveMode)
        {
            result.AddError("StopOnDone requires SaveMode to be enabled (cannot stop server while players are connected)");
        }

        return result;
    }
}

