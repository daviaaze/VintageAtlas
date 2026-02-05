using System.Collections.Generic;

namespace VintageAtlas.Application.Validation;

/// <summary>
/// Interface for validating objects.
/// Follows the Validator pattern for input validation.
/// </summary>
/// <typeparam name="T">Type to validate</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validate an object and return validation errors
    /// </summary>
    /// <param name="instance">Object to validate</param>
    /// <returns>List of validation errors (empty if valid)</returns>
    ValidationResult Validate(T instance);
}

/// <summary>
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    private readonly List<string> _errors = [];

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    public void AddError(string error)
    {
        _errors.Add(error);
    }

    public static ValidationResult Success() => new();
    
    public static ValidationResult Failure(params string[] errors)
    {
        var result = new ValidationResult();
        foreach (var error in errors)
        {
            result.AddError(error);
        }
        return result;
    }
}

