using System.Text.Json.Nodes;

namespace Crest.Components.Models;

public sealed record CrestContentItemPage(CrestContentItem[] Items, int Total, int Page, int PageSize);

/// <summary>
/// The content-item wrapper the model-driven components (the editors, Parties, Fruitful) consume.
/// Not a display tree: the tree's node is <c>Crest.DisplayManagement.Components.DisplayNode</c>
/// (docs/blazor-display.md, ruling 2026-10-12), which replaced the shape metadata, zones, sources
/// and diagnostics this record used to carry.
/// </summary>
public sealed record CrestModel(
    CrestContentItem? ContentItem,
    JsonNode? Content)
{
    /// <summary>Wraps a content item returned by the api/crest/content-items API
    /// into the shape the model-driven components consume.</summary>
    public static CrestModel? FromContentItem(CrestContentItem? item) =>
        item is null ? null : new(item, item.Content);

    public string? GetText(string partName, string fieldName)
    {
        return ContentItem?.Content?[partName]?[fieldName]?["Text"]?.GetValue<string>();
    }

    public decimal? GetNumber(string partName, string fieldName)
    {
        var node = ContentItem?.Content?[partName]?[fieldName]?["Value"];
        return node is null ? null : node.GetValue<decimal>();
    }

    public string GetPickerIds(string partName, string fieldName)
    {
        var ids = ContentItem?.Content?[partName]?[fieldName]?["ContentItemIds"]?.AsArray();
        return ids is null
            ? string.Empty
            : string.Join(", ", ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)));
    }
}

public sealed record CrestContentItem(
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
    CrestContentItemMetadata? Metadata);

public sealed record CrestContentItemMetadata(
    string ContentType,
    string DisplayName,
    CrestContentPartMetadata[] Parts);

public sealed record CrestContentPartMetadata(
    string Name,
    string DisplayName,
    CrestContentFieldMetadata[] Fields,
    JsonNode? Settings);

public sealed record CrestContentFieldMetadata(
    string Name,
    string FieldType,
    string Label,
    bool Required,
    string Editor,
    string? Position,
    JsonNode? Settings);
