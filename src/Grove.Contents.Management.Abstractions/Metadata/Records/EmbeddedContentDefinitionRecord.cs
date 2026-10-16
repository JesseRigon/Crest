using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Metadata.Records;

/// <summary>
/// An embedding declared on a type (docs/content-engine.md › Embedded content items): a named slot
/// holding embedded content items, stored beside the type's field-set attachments.
/// </summary>
public class EmbeddedContentDefinitionRecord
{
    /// <summary>The embedding's name: its key in an item's content and its group in the editor.</summary>
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    /// <summary>The embedding's position among the type's field-set attachments in the editor.</summary>
    public string Position { get; set; }

    /// <summary>The content types the embedding allows, stereotypes as <c>@Stereotype</c>, and <c>*</c> for any type (the parent then names the type, as a taxonomy names its term type).</summary>
    public string[] AllowedTypes { get; set; } = [];

    public int? MinCount { get; set; }

    public int? MaxCount { get; set; }

    /// <summary>Whether the items' order is meaningful (the editor reorders them).</summary>
    public bool Ordered { get; set; } = true;

    /// <summary>The display type the embedded content items render with (Summary when blank).</summary>
    public string DisplayType { get; set; }

    /// <summary>Whether the items nest into a tree: each item holds its child items under the embedding's name, at any depth.</summary>
    public bool Nested { get; set; }

    /// <summary>Whether the editor shows the items collapsed.</summary>
    public bool Collapsed { get; set; }

    /// <summary>Whether the items' ids are exposed to option sources and routing.</summary>
    public bool Referenceable { get; set; }

    /// <summary>See <see cref="Models.EmbeddingModes"/>.</summary>
    public string Mode { get; set; } = Models.EmbeddingModes.Live;

    /// <summary>For a snapshot embedding: the referenced values (option row columns) an option list field reference copies beside its ids.</summary>
    public string[] CopiedValues { get; set; } = [];

    /// <summary>Further settings sections (a definition lock).</summary>
    public JsonObject Settings { get; set; }
}
