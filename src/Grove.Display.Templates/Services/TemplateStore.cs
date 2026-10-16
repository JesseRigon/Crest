using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Environment.Cache;
using Grove.Display.Templates.Models;
using Microsoft.Extensions.Caching.Memory;
using YesSql;

namespace Grove.Display.Templates.Services;

/// <summary>A published template as the resolver and the binding use it.</summary>
public sealed record TemplateRecord(
    string ContentItemId,
    string ContentItemVersionId,
    string Name,
    string? Theme,
    string? DisplayType,
    string Source,
    string? Description,
    bool Published,
    bool Latest,
    DateTime? ModifiedUtc)
{
    public bool IsComposite { get; init; }

    public string? DisplayName { get; init; }

    public string? Category { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyList<TemplateInput> Inputs { get; init; } = [];

    public IReadOnlyList<TemplateOutput> Outputs { get; init; } = [];
}

/// <summary>The published templates the resolver chooses from.</summary>
public interface ITemplateCatalog
{
    Task<IReadOnlyList<TemplateRecord>> ListPublishedAsync();
}

/// <summary>
/// Reads and writes templates. The published set is small and read on every display build, so
/// it is cached and re-read when the publish signal fires (a template published, unpublished or
/// removed). Writes go through the content manager: a save makes a draft, publish is its own act.
/// </summary>
public sealed class TemplateStore(
    IContentManager contentManager,
    ISession session,
    IMemoryCache memoryCache,
    ISignal signal) : ITemplateCatalog
{
    private const string CacheKey = "Grove.Display.Templates.PublishedSet";

    public async Task<IReadOnlyList<TemplateRecord>> ListPublishedAsync()
    {
        if (memoryCache.TryGetValue<IReadOnlyList<TemplateRecord>>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == TemplatesConstants.ContentType && index.Published).ListAsync();
        var records = items.Select(ToRecord).OfType<TemplateRecord>().ToArray();
        memoryCache.Set(CacheKey, (IReadOnlyList<TemplateRecord>)records, signal.GetToken(TemplatesConstants.PublishedSignal));
        return records;
    }

    /// <summary>Every template's latest version (draft or published), for the editor.</summary>
    public async Task<IReadOnlyList<TemplateRecord>> ListLatestAsync()
    {
        var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == TemplatesConstants.ContentType && index.Latest).ListAsync();
        return items.Select(ToRecord).OfType<TemplateRecord>().OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<TemplateRecord?> GetAsync(string contentItemId, VersionOptions? version = null) =>
        ToRecord(await contentManager.GetAsync(contentItemId, version ?? VersionOptions.Latest));

    /// <summary>Saves a draft of the template (a new one when <paramref name="contentItemId"/> is null).</summary>
    public async Task<TemplateRecord> SaveDraftAsync(string? contentItemId, TemplateFieldSet values)
    {
        var item = contentItemId is null ? null : await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
        if (item is null)
        {
            item = await contentManager.NewAsync(TemplatesConstants.ContentType);
            Apply(item, values);
            await contentManager.CreateAsync(item, VersionOptions.Draft);
        }
        else
        {
            Apply(item, values);
            await contentManager.UpdateAsync(item);
        }

        return ToRecord(item)!;
    }

    public async Task<bool> PublishAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return false;
        }

        await contentManager.PublishAsync(item);
        return true;
    }

    public async Task<bool> RemoveAsync(string contentItemId)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null)
        {
            return false;
        }

        await contentManager.RemoveAsync(item);
        return true;
    }

    private static void Apply(ContentItem item, TemplateFieldSet values)
    {
        item.DisplayText = values.Name;
        item.Alter<TemplateFieldSet>(part =>
        {
            part.Name = values.Name.Trim();
            part.Theme = string.IsNullOrWhiteSpace(values.Theme) ? null : values.Theme.Trim();
            part.DisplayType = string.IsNullOrWhiteSpace(values.DisplayType) ? null : values.DisplayType.Trim();
            part.Source = values.Source ?? string.Empty;
            part.Description = values.Description;
            part.IsComposite = values.IsComposite;
            part.DisplayName = string.IsNullOrWhiteSpace(values.DisplayName) ? null : values.DisplayName.Trim();
            part.Category = string.IsNullOrWhiteSpace(values.Category) ? null : values.Category.Trim();
            part.Icon = string.IsNullOrWhiteSpace(values.Icon) ? null : values.Icon.Trim();
            part.Inputs = values.Inputs ?? [];
            part.Outputs = values.Outputs ?? [];
        });
    }

    private static TemplateRecord? ToRecord(ContentItem? item) =>
        item?.As<TemplateFieldSet>() is { } part
            ? new(item.ContentItemId, item.ContentItemVersionId, part.Name, part.Theme, part.DisplayType, part.Source, part.Description, item.Published, item.Latest, item.ModifiedUtc)
            {
                IsComposite = part.IsComposite,
                DisplayName = part.DisplayName,
                Category = part.Category,
                Icon = part.Icon,
                Inputs = part.Inputs ?? [],
                Outputs = part.Outputs ?? [],
            }
            : null;
}
