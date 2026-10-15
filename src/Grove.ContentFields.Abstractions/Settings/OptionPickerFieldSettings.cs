using Grove.ContentManagement.Metadata.Settings;

namespace Grove.Settings;

/// <summary>
/// Per-ATTACHMENT configuration for an <see cref="Grove.Fields.OptionPickerField"/>. The field's
/// stored shape lives with the other field types; the Grove.OptionPicker module owns its
/// registration, sources, API and editor.
/// The projection lives here rather than on the field or the target type, because the
/// same source is displayed differently in different dropdowns.
/// </summary>
public class OptionPickerFieldSettings : FieldSettings
{
    /// <summary>Which source to draw from, e.g. "contentpartlist:pricing.modifier-kind".</summary>
    public string SourceKey { get; set; } = string.Empty;

    public bool Multiple { get; set; }

    // Required is FieldSettings.Required: whether a value must be chosen for the document
    // to be complete. It is also what a DEPENDENT field derives its own requiredness from:
    // if the parent is required, the child is too, since a document cannot be complete with
    // the pair half-filled. (A redeclaration here once hid the base property, so code
    // reading the settings as FieldSettings always saw false.)

    public string Placeholder { get; set; } = string.Empty;

    /// <summary>The columns this dropdown displays, in order. Empty means the
    /// provider's default display column.</summary>
    public OptionColumn[] Columns { get; set; } = [];

    /// <summary>Optional composition of the selected value, e.g. "{Name} ({Badge})".
    /// Null or empty joins the visible columns instead.</summary>
    public string? DisplayTemplate { get; set; }

    /// <summary>A Liquid pattern for each row's label, for sources that render one (content
    /// items: <c>{{ Model.ContentItem | display_text }}</c>). Null or empty keeps the source's
    /// own label.</summary>
    public string? TitlePattern { get; set; }

    /// <summary>Column paths the type-ahead filters on. Empty means the provider
    /// decides.</summary>
    public string[] SearchColumns { get; set; } = [];

    /// <summary>Column paths to sort by. Empty means the provider's natural order
    /// (for Content Part Lists: position, then display text).</summary>
    public string[] SortColumns { get; set; } = [];

    /// <summary>Restrictions applied to what the dropdown offers. Evaluated by the
    /// provider, which knows how to filter its own source.</summary>
    public OptionFilter[] Filters { get; set; } = [];
}
