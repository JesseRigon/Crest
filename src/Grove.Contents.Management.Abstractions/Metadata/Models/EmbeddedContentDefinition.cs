using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Management.Metadata.Models;

/// <summary>
/// An embedding (docs/content-engine.md › Embedded content items): a named slot on a parent type
/// holding embedded content items of the types it allows. Not a field set: its items are content
/// items of their own types, stored inside the parent's document under the embedding's name
/// (<see cref="EmbeddedContentItems"/>). It takes a position among the type's field-set
/// attachments in the editor.
/// </summary>
public class EmbeddedContentDefinition : ContentDefinition
{
    public EmbeddedContentDefinition(EmbeddedContentDefinitionRecord record)
    {
        Name = record.Name;
        DisplayName = record.DisplayName;
        Description = record.Description;
        Position = record.Position;
        AllowedTypes = [.. record.AllowedTypes ?? []];
        MinCount = record.MinCount;
        MaxCount = record.MaxCount;
        Ordered = record.Ordered;
        DisplayType = record.DisplayType;
        Nested = record.Nested;
        Collapsed = record.Collapsed;
        Referenceable = record.Referenceable;
        Mode = EmbeddingModes.Normalize(record.Mode);
        CopiedValues = [.. record.CopiedValues ?? []];
        Settings = record.Settings?.Clone() ?? [];
    }

    public string DisplayName { get; }

    public string Description { get; }

    public string Position { get; }

    /// <summary>The allowed content types; a <c>@Stereotype</c> entry allows every type of that stereotype.</summary>
    public IReadOnlyList<string> AllowedTypes { get; }

    public int? MinCount { get; }

    public int? MaxCount { get; }

    public bool Ordered { get; }

    public string DisplayType { get; }

    /// <summary>The items nest into a tree: each holds its child items under the embedding's name.</summary>
    public bool Nested { get; }

    public bool Collapsed { get; }

    public bool Referenceable { get; }

    public string Mode { get; }

    public IReadOnlyList<string> CopiedValues { get; }

    public bool IsSnapshot => Mode == EmbeddingModes.Snapshot;

    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>The <see cref="AllowedTypes"/> entry allowing any type: the parent names the type its items have.</summary>
    public const string AnyType = "*";

    /// <summary>The allowed content types named directly (not by stereotype).</summary>
    public IEnumerable<string> AllowedTypeNames => AllowedTypes.Where(type => !type.StartsWith('@') && type != AnyType);

    /// <summary>The items of <paramref name="item"/>'s embedding, and of a nested embedding every descendant, depth first.</summary>
    public IEnumerable<ContentItem> AllItems(ContentItem item)
    {
        foreach (var child in item.Embedded(Name))
        {
            yield return child;
            if (Nested)
            {
                foreach (var descendant in AllItems(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>The allowed stereotypes, without their <c>@</c>.</summary>
    public IEnumerable<string> AllowedStereotypes => AllowedTypes.Where(type => type.StartsWith('@')).Select(type => type[1..]);

    /// <summary>Whether the embedding allows items of <paramref name="type"/>, by name or by stereotype.</summary>
    public bool Allows(ContentTypeDefinition type) =>
        type is not null
        && (AllowedTypes.Contains(AnyType)
            || AllowedTypeNames.Contains(type.Name, StringComparer.OrdinalIgnoreCase)
            || (type.TryGetStereotype(out var stereotype) && AllowedStereotypes.Contains(stereotype, StringComparer.OrdinalIgnoreCase)));

    public string DisplayNameOrName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

    public EmbeddedContentDefinitionRecord ToRecord() => new()
    {
        Name = Name,
        DisplayName = DisplayName,
        Description = Description,
        Position = Position,
        AllowedTypes = [.. AllowedTypes],
        MinCount = MinCount,
        MaxCount = MaxCount,
        Ordered = Ordered,
        DisplayType = DisplayType,
        Nested = Nested,
        Collapsed = Collapsed,
        Referenceable = Referenceable,
        Mode = Mode,
        CopiedValues = [.. CopiedValues],
        Settings = Settings?.Clone() ?? [],
    };
}

/// <summary>How an embedding's items read (docs/content-engine.md › Live and snapshot embeddings).</summary>
public static class EmbeddingModes
{
    /// <summary>The items read through their type's current definition.</summary>
    public const string Live = "live";

    /// <summary>The items freeze when the parent is published, with the schema they were written in.</summary>
    public const string Snapshot = "snapshot";

    public static readonly string[] All = [Live, Snapshot];

    public static string Normalize(string mode) =>
        string.Equals(mode, Snapshot, StringComparison.OrdinalIgnoreCase) ? Snapshot : Live;
}

/// <summary>How deep embedding goes (docs/content-engine.md › Embedded content items): the type editor refuses deeper declarations.</summary>
public static class EmbeddingDepth
{
    public const int Max = 4;
}
