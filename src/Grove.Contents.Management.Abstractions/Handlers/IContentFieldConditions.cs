namespace Grove.Contents.Management.Handlers;

/// <summary>
/// A type's conditions on its fields (docs/content-engine.md › Validation and conditions run on
/// the one expression engine), applied by the content manager's validation, which every write
/// path runs: the fields their visibility hides are cleared before the handlers validate (a hidden
/// field keeps no value and is exempt from its own checks), and the type's field rules are
/// evaluated after them, their refusals keyed by field path.
/// </summary>
public interface IContentFieldConditions
{
    /// <summary>Removes the values of the fields whose visibility condition is false.</summary>
    Task ClearHiddenFieldsAsync(ContentItem contentItem, CancellationToken cancellationToken = default);

    /// <summary>Evaluates the type's field rules, failing <paramref name="result"/> for each refusal.</summary>
    Task ValidateRulesAsync(ContentItem contentItem, ContentValidateResult result, CancellationToken cancellationToken = default);
}
