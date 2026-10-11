#nullable enable
using System.Text.Json.Nodes;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;
using Crest.Media.Fields;
using Crest.Media.Files;
using Microsoft.Extensions.Logging;
using YesSql;

namespace Crest.Media.Library;

/// <summary>
/// Gives every media field a placement id for each path (ruling F1). Walks the latest and
/// published version of every content item, in batches, including fields nested in other items
/// (bag and flow parts) and SEO's images. A path that resolves to no placement keeps an empty id
/// and is still read by its path. Paths are kept beside the ids. Older versions are history and
/// keep their paths, which still resolve.
/// </summary>
public interface IMediaFieldRewriter
{
    /// <summary>Every content item's latest and published version.</summary>
    /// <returns>How many content item versions were rewritten.</returns>
    Task<int> RewriteAsync(CancellationToken cancellationToken = default);

    /// <summary>One content item, in place.</summary>
    Task<bool> RewriteAsync(ContentItem contentItem, CancellationToken cancellationToken = default);
}

public sealed class MediaFieldRewriter(
    IStore yesSqlStore,
    IContentDefinitionManager definitions,
    IMediaLibrary library,
    IMediaObjectStore objects,
    ILogger<MediaFieldRewriter> logger) : IMediaFieldRewriter
{
    private const int BatchSize = 50;

    /// <summary>Media fields that are part properties rather than defined fields (SEO's images).</summary>
    public static readonly IReadOnlyList<string> KnownPropertyFields = ["DefaultSocialImage", "OpenGraphImage", "TwitterImage"];

    /// <returns>How many content item versions were rewritten.</returns>
    public async Task<int> RewriteAsync(CancellationToken cancellationToken = default)
    {
        var fieldNames = await FieldNamesAsync(definitions);
        var rewritten = 0;
        var documentId = 0L;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = yesSqlStore.CreateSession();
            var items = await session.Query<ContentItem, ContentItemIndex>(index => index.DocumentId > documentId && (index.Latest || index.Published))
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            var batch = items.ToList();
            if (batch.Count == 0)
            {
                break;
            }

            documentId = batch.Max(item => item.Id);
            var changed = false;
            foreach (var item in batch)
            {
                try
                {
                    if (await RewriteAsync((JsonObject)item.Content, fieldNames, path => library.ResolvePathAsync(path, cancellationToken), ExistsAsync))
                    {
                        await session.SaveAsync(item, cancellationToken: cancellationToken);
                        rewritten++;
                        changed = true;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Could not rewrite the media fields of content item '{ContentItemId}'.", item.ContentItemId);
                }
            }

            if (changed)
            {
                await session.SaveChangesAsync(cancellationToken);
            }
        }

        return rewritten;
    }

    /// <summary>Rewrites the media fields of one content item: for content arriving by recipe or
    /// deployment, whose ids (if any) belong to another tenant.</summary>
    public async Task<bool> RewriteAsync(ContentItem contentItem, CancellationToken cancellationToken = default) =>
        await RewriteAsync((JsonObject)contentItem.Content, await FieldNamesAsync(definitions), path => library.ResolvePathAsync(path, cancellationToken), ExistsAsync);

    private async Task<bool> ExistsAsync(string placementId) => await objects.GetPlacementAsync(placementId) is not null;

    /// <summary>
    /// Rewrites every media field found under <paramref name="node"/>: an object under one of
    /// <paramref name="fieldNames"/> with a <c>Paths</c> array gets a <c>PlacementIds</c> array
    /// aligned with it. An id already stored is kept when <paramref name="exists"/> finds it
    /// (one from another tenant is not), else the path is resolved again.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public static async Task<bool> RewriteAsync(JsonNode? node, IReadOnlySet<string> fieldNames, Func<string, Task<string?>> resolve, Func<string, Task<bool>> exists)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (value is JsonObject field && fieldNames.Contains(key) && field[nameof(MediaField.Paths)] is JsonArray)
                    {
                        changed |= await RewriteFieldAsync(field, resolve, exists);
                    }
                    else if (value is JsonObject or JsonArray)
                    {
                        changed |= await RewriteAsync(value, fieldNames, resolve, exists);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array.ToList())
                {
                    changed |= await RewriteAsync(item, fieldNames, resolve, exists);
                }

                break;
        }

        return changed;
    }

    private static async Task<bool> RewriteFieldAsync(JsonObject field, Func<string, Task<string?>> resolve, Func<string, Task<bool>> exists)
    {
        var paths = (JsonArray)field[nameof(MediaField.Paths)]!;
        var existing = field[nameof(MediaField.PlacementIds)] as JsonArray;
        var ids = new string[paths.Count];
        var changed = existing is null || existing.Count != paths.Count;
        for (var i = 0; i < paths.Count; i++)
        {
            var current = existing is not null && i < existing.Count ? existing[i]?.GetValue<string>() : null;
            if (!string.IsNullOrEmpty(current) && await exists(current))
            {
                ids[i] = current;
                continue;
            }

            var path = paths[i]?.GetValue<string>();
            ids[i] = string.IsNullOrEmpty(path) ? string.Empty : await resolve(path) ?? string.Empty;
            changed |= ids[i] != (current ?? string.Empty);
        }

        if (changed)
        {
            field[nameof(MediaField.PlacementIds)] = new JsonArray([.. ids.Select(id => (JsonNode)JsonValue.Create(id)!)]);
        }

        return changed;
    }

    /// <summary>The names media fields are stored under: every defined media field, and SEO's.</summary>
    public static async Task<IReadOnlySet<string>> FieldNamesAsync(IContentDefinitionManager definitions)
    {
        var names = new HashSet<string>(KnownPropertyFields, StringComparer.Ordinal);
        foreach (var part in await definitions.ListPartDefinitionsAsync())
        {
            foreach (var field in part.Fields.Where(field => field.FieldDefinition?.Name == nameof(MediaField)))
            {
                names.Add(field.Name);
            }
        }

        return names;
    }
}
