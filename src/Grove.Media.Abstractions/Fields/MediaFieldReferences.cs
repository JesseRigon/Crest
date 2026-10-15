#nullable enable
using System.Text.Json.Nodes;
using Grove.Media.Library;

namespace Grove.Media.Fields;

/// <summary>Reads and writes a <see cref="MediaField"/>'s items as references: the placement id
/// when one is stored, the path otherwise (ruling F1).</summary>
public static class MediaFieldReferences
{
    /// <summary>The field's items in order. With ids stored, there is one item per id and the
    /// path at the same position is its fallback; without, one item per path.</summary>
    public static IReadOnlyList<MediaReference> GetReferences(this MediaField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var ids = field.PlacementIds;
        var paths = field.Paths ?? [];
        if (ids is null)
        {
            return [.. paths.Select(MediaReference.ForPath)];
        }

        var count = Math.Max(ids.Length, paths.Length);
        var references = new List<MediaReference>(count);
        for (var i = 0; i < count; i++)
        {
            var id = i < ids.Length && !string.IsNullOrEmpty(ids[i]) ? ids[i] : null;
            var path = i < paths.Length && !string.IsNullOrEmpty(paths[i]) ? paths[i] : null;
            references.Add(new MediaReference(id, path));
        }

        return references;
    }

    /// <summary>Stores <paramref name="references"/>: ids and paths side by side, an empty string
    /// where one is unknown. Every reference keeps its position, so media texts, anchors and
    /// attached file names stay aligned.</summary>
    public static void SetReferences(this MediaField field, IEnumerable<MediaReference> references)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(references);

        var items = references.ToArray();
        field.PlacementIds = [.. items.Select(reference => reference.PlacementId ?? string.Empty)];
        field.Paths = [.. items.Select(reference => reference.Path ?? string.Empty)];
    }

    /// <summary>The first item that names anything, or null.</summary>
    public static MediaReference? FirstReference(this MediaField field) =>
        field?.GetReferences().FirstOrDefault(reference => !reference.IsEmpty) is { IsEmpty: false } first ? first : null;

    /// <summary>A media field read from its stored JSON (a settings item, a template value).</summary>
    public static MediaField? FromJson(JsonObject? json)
    {
        if (json is null || (!json.ContainsKey(nameof(MediaField.Paths)) && !json.ContainsKey(nameof(MediaField.PlacementIds))))
        {
            return null;
        }

        return new MediaField
        {
            Paths = Strings(json[nameof(MediaField.Paths)]) ?? [],
            PlacementIds = Strings(json[nameof(MediaField.PlacementIds)]),
            MediaTexts = Strings(json[nameof(MediaField.MediaTexts)]) ?? [],
        };
    }

    private static string[]? Strings(JsonNode? node) =>
        node is JsonArray array ? [.. array.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty)] : null;

    /// <summary>Whether every item of the field has a placement id.</summary>
    public static bool IsMigrated(this MediaField field) =>
        field.GetReferences().All(reference => reference.HasPlacement);
}
