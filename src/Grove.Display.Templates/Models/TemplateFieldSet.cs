using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Display.Templates.Models;

/// <summary>
/// A template (docs/blazor-display.md § 5): Liquid whose tags emit display-tree nodes, bound to a
/// shape name with optional qualifiers, or a composite other templates place by name. Stored as
/// the <see cref="TemplatesConstants.ContentType"/> content item, so it has drafts, publish and
/// history like any content (decision 7: versioning from the publish model). Its values are fields
/// (text fields, a boolean field <c>IsComposite</c>); this class is the typed accessor over them. The
/// inputs and outputs stay lists of entries on the field set (no field type holds a list of records).
/// </summary>
public sealed class TemplateFieldSet : ContentFieldSet
{
    /// <summary>The shape name it binds (<c>Content__BlogPost</c>, <c>Layout</c>) or the composite's name.</summary>
    [JsonIgnore]
    public string Name
    {
        get => this.Get<TextField>(nameof(Name))?.Text ?? string.Empty;
        set => this.Alter<TextField>(nameof(Name), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The theme (and so the shell) it applies to; every theme when empty.</summary>
    [JsonIgnore]
    public string? Theme
    {
        get => this.Get<TextField>(nameof(Theme))?.Text;
        set => this.Alter<TextField>(nameof(Theme), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The display type it applies to (<c>Detail</c>, <c>Summary</c>); every one when empty.</summary>
    [JsonIgnore]
    public string? DisplayType
    {
        get => this.Get<TextField>(nameof(DisplayType))?.Text;
        set => this.Alter<TextField>(nameof(DisplayType), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The Liquid source.</summary>
    [JsonIgnore]
    public string Source
    {
        get => this.Get<TextField>(nameof(Source))?.Text ?? string.Empty;
        set => this.Alter<TextField>(nameof(Source), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string? Description
    {
        get => this.Get<TextField>(nameof(Description))?.Text;
        set => this.Alter<TextField>(nameof(Description), fieldValue => fieldValue.Text = value);
    }

    /// <summary>
    /// A composite is a tenant-made component (rulings 2026-10-12, custom UI at runtime): placed by
    /// name, its inputs typed and checked, its outputs re-published from inner nodes, listed in the
    /// palette with the coded components.
    /// </summary>
    [JsonIgnore]
    public bool IsComposite
    {
        get => this.Get<BooleanField>(nameof(IsComposite))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(IsComposite), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public string? DisplayName
    {
        get => this.Get<TextField>(nameof(DisplayName))?.Text;
        set => this.Alter<TextField>(nameof(DisplayName), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string? Category
    {
        get => this.Get<TextField>(nameof(Category))?.Text;
        set => this.Alter<TextField>(nameof(Category), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string? Icon
    {
        get => this.Get<TextField>(nameof(Icon))?.Text;
        set => this.Alter<TextField>(nameof(Icon), fieldValue => fieldValue.Text = value);
    }

    public List<TemplateInput> Inputs { get; set; } = [];

    public List<TemplateOutput> Outputs { get; set; } = [];
}

/// <summary>A composite's input: a typed parameter (<c>string</c>, <c>number</c>, <c>boolean</c>, <c>choice</c>, <c>object</c>, <c>array</c>) the template reads as <c>Model.{Name}</c>.</summary>
public sealed class TemplateInput
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "string";

    public string? DisplayName { get; set; }

    public bool Required { get; set; }

    public System.Text.Json.Nodes.JsonNode? Default { get; set; }

    public string[]? Choices { get; set; }
}

/// <summary>A composite's output: an inner node's output (by its id in the template), re-published under <see cref="Name"/>.</summary>
public sealed class TemplateOutput
{
    public string Name { get; set; } = string.Empty;

    public string Node { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? DisplayName { get; set; }
}

public static class TemplatesConstants
{
    public const string ContentType = "Template";

    /// <summary>The signal a publish, unpublish or removal of a template raises: the published set is re-read.</summary>
    public const string PublishedSignal = "Grove.Display.Templates.Published";
}
