#nullable enable

namespace Grove.Infrastructure.Settings;

/// <summary>
/// The settings form's metadata for one property of a settings model, beyond what the standard
/// attributes say: the label, help text, section and order come from <c>[Display]</c>, the input
/// type from <c>[DataType]</c>, the rules from DataAnnotations and <c>Grove.Validation</c>, a hidden
/// property from <c>[ScaffoldColumn(false)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SettingAttribute : Attribute
{
    /// <summary>
    /// The editor, when the property type does not say it: <c>text</c>, <c>textarea</c>,
    /// <c>number</c>, <c>boolean</c>, <c>select</c>, <c>multiselect</c>, <c>list</c> (lines of
    /// text), <c>color</c>, <c>code</c>, <c>json</c>, <c>object</c>, <c>objects</c>, or
    /// <c>hidden</c>: not shown, carried through the form as is (the id of an item in a list).
    /// </summary>
    public string? Editor { get; set; }

    /// <summary>
    /// Write-only: the value is never sent to the browser; the form shows whether one is set, and
    /// an empty submission keeps the stored value.
    /// </summary>
    public bool Secret { get; set; }

    /// <summary>
    /// The data protection purpose a secret is stored under (protected on save, never
    /// unprotected for the form); stored as is when absent.
    /// </summary>
    public string? ProtectedBy { get; set; }

    /// <summary>The <see cref="ISettingOptions"/> that lists the allowed values (a select, a multi-select).</summary>
    public Type? Options { get; set; }

    /// <summary>The language of a <c>code</c> editor (<c>html</c>, <c>liquid</c>, <c>javascript</c>, <c>css</c>, <c>json</c>).</summary>
    public string? Language { get; set; }

    /// <summary>
    /// Shown only when another field has a value: <c>"Field"</c> (truthy), <c>"Field=Value"</c>,
    /// <c>"Field=A|B"</c>; the other field's wire or property name.
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>Shown, never edited.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>The placeholder of an empty input.</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// For a list of strings stored as one string: the separator the stored value joins the
    /// items with (a space, a comma).
    /// </summary>
    public string? Separator { get; set; }
}

/// <summary>
/// The allowed values of a settings field (<c>[Setting(Options = typeof(T))]</c>), resolved from
/// the request's services when the schema is served: a short list of what the tenant has
/// (content types, deployment plans, cultures).
/// </summary>
public interface ISettingOptions
{
    Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>One allowed value: what is stored and what is shown.</summary>
public sealed record SettingOption(string Value, string Text);
