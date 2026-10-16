using System.Text.Json.Nodes;

namespace Grove.Display.Components.Models;

public sealed record GroveContentItemPage(GroveContentItem[] Items, int Total, int Page, int PageSize);

/// <summary>
/// The content-item wrapper the model-driven components (the editors, Parties, host modules) consume.
/// Not a display tree: the tree's node is <c>Grove.Display.Management.Components.DisplayNode</c>
/// (docs/blazor-display.md, ruling 2026-10-12), which replaced the shape metadata, zones, sources
/// and diagnostics this record used to carry.
/// </summary>
public sealed record GroveModel(
    GroveContentItem? ContentItem,
    JsonNode? Content)
{
    /// <summary>Wraps a content item returned by the api/grove/content-items API
    /// into the shape the model-driven components consume.</summary>
    public static GroveModel? FromContentItem(GroveContentItem? item) =>
        item is null ? null : new(item, item.Content);

    public string? GetText(string fieldSetName, string fieldName)
    {
        return ContentItem?.Content?[fieldSetName]?[fieldName]?["Text"]?.GetValue<string>();
    }

    public decimal? GetNumber(string fieldSetName, string fieldName)
    {
        var node = ContentItem?.Content?[fieldSetName]?[fieldName]?["Value"];
        return node is null ? null : node.GetValue<decimal>();
    }

    public string GetPickerIds(string fieldSetName, string fieldName)
    {
        var ids = ContentItem?.Content?[fieldSetName]?[fieldName]?["SelectedIds"]?.AsArray();
        return ids is null
            ? string.Empty
            : string.Join(", ", ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)));
    }
}

public sealed record GroveContentItem(
    string? ContentItemId,
    string? ContentItemVersionId,
    string? ContentType,
    string? DisplayText,
    bool Latest,
    bool Published,
    DateTime? CreatedUtc,
    DateTime? ModifiedUtc,
    DateTime? PublishedUtc,
    string? Owner,
    string? Author,
    JsonNode? Content,
    string? EditUrl,
    GroveContentItemMetadata? Metadata);

public sealed record GroveContentItemMetadata(
    string ContentType,
    string DisplayName,
    GroveContentFieldSetMetadata[] FieldSets);

public sealed record GroveContentFieldSetMetadata(
    string Name,
    string DisplayName,
    GroveContentFieldMetadata[] Fields,
    JsonNode? Settings);

public sealed record GroveContentFieldMetadata(
    string Name,
    string FieldType,
    string Label,
    bool Required,
    string Editor,
    string? Position,
    JsonNode? Settings);
