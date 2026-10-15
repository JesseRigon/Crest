using Grove.ContentManagement;

namespace Grove.Templates.Models;

/// <summary>
/// A template (docs/blazor-display.md § 5): Liquid whose tags emit display-tree nodes, bound to a
/// shape name with optional qualifiers, or a composite other templates place by name. Stored as
/// the <see cref="TemplatesConstants.ContentType"/> content item, so it has drafts, publish and
/// history like any content (decision 7: versioning from the publish model).
/// </summary>
public sealed class TemplatePart : ContentPart
{
    /// <summary>The shape name it binds (<c>Content__BlogPost</c>, <c>Layout</c>) or the composite's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The theme (and so the shell) it applies to; every theme when empty.</summary>
    public string? Theme { get; set; }

    /// <summary>The display type it applies to (<c>Detail</c>, <c>Summary</c>); every one when empty.</summary>
    public string? DisplayType { get; set; }

    /// <summary>The Liquid source.</summary>
    public string Source { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// A composite is a tenant-made component (rulings 2026-10-12, custom UI at runtime): placed by
    /// name, its inputs typed and checked, its outputs re-published from inner nodes, listed in the
    /// palette with the coded components.
    /// </summary>
    public bool IsComposite { get; set; }

    public string? DisplayName { get; set; }

    public string? Category { get; set; }

    public string? Icon { get; set; }

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
    public const string PublishedSignal = "Grove.Templates.Published";
}
