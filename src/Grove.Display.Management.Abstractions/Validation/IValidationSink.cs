#nullable enable

namespace Grove.Validation;

/// <summary>
/// The request's validation errors raised below the API layer: the content manager's cancelled
/// publish, unpublish, create and delete, and the part and field handlers' <c>ValidatingAsync</c>
/// results a save reports. Scoped to the request (one per tenant scope); the API answers a
/// non-empty sink with <c>ApiProblems.Validation(sink.Errors)</c>, the error contract's
/// <c>errors</c> map (docs/MVC/api.md › The error contract). Keys are field paths
/// (<c>FieldSet.Field</c>), setting names, or <c>""</c> for the object. No model binding.
/// </summary>
public interface IValidationSink
{
    /// <summary>Adds a message under <paramref name="key"/> (<c>""</c> for the object).</summary>
    void AddError(string key, string message);

    /// <summary>The errors raised so far in this scope.</summary>
    ValidationErrors Errors { get; }

    /// <summary>True when no error was raised.</summary>
    bool IsValid { get; }
}

/// <summary>The request-scoped <see cref="IValidationSink"/>.</summary>
public sealed class ValidationSink : IValidationSink
{
    public ValidationErrors Errors { get; } = new();

    public bool IsValid => Errors.IsEmpty;

    public void AddError(string key, string message) => Errors.Add(key ?? string.Empty, message);
}
