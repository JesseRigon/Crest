using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using Grove.Environment.Cache;
using Grove.Security.Permissions;
using Grove.Templates.Models;
using Microsoft.Extensions.Caching.Memory;
using YesSql;

namespace Grove.Templates.Services;

/// <summary>A designed page as the route table, the site and the API read it.</summary>
public sealed record DesignedPageRecord(
    string ContentItemId,
    string ContentItemVersionId,
    string Name,
    string Route,
    string Bucket,
    string? Title,
    string? Description,
    bool AllowAnonymous,
    string Source,
    bool Published,
    DateTime? ModifiedUtc)
{
    /// <summary>The shape name its tree binds by.</summary>
    public string ShapeName => DesignedPagesConstants.ShapeType + "__" + Name;
}

/// <summary>
/// Reads and writes designed pages (decision 9). The published set is what the route table and
/// the site read on every request, so it is cached and re-read when the publish signal fires;
/// a draft is reached through preview only. Writes go through the content manager: a save makes
/// a draft, publish is its own act.
/// </summary>
public sealed class DesignedPageStore(IContentManager contentManager, ISession session, IMemoryCache memoryCache, ISignal signal)
{
    private const string CacheKey = "Grove.Templates.DesignedPages.PublishedSet";

    public async Task<IReadOnlyList<DesignedPageRecord>> ListPublishedAsync()
    {
        if (memoryCache.TryGetValue<IReadOnlyList<DesignedPageRecord>>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == DesignedPagesConstants.ContentType && index.Published).ListAsync();
        var records = items.Select(ToRecord).OfType<DesignedPageRecord>().ToArray();
        memoryCache.Set(CacheKey, (IReadOnlyList<DesignedPageRecord>)records, signal.GetToken(DesignedPagesConstants.PublishedSignal));
        return records;
    }

    /// <summary>Every page's latest version (draft or published), for the editor and the permissions.</summary>
    public async Task<IReadOnlyList<DesignedPageRecord>> ListLatestAsync()
    {
        var items = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == DesignedPagesConstants.ContentType && index.Latest).ListAsync();
        return items.Select(ToRecord).OfType<DesignedPageRecord>().OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<DesignedPageRecord?> GetAsync(string contentItemId, VersionOptions? version = null) =>
        ToRecord(await contentManager.GetAsync(contentItemId, version ?? VersionOptions.Latest));

    /// <summary>The published page of <paramref name="bucket"/> whose route matches <paramref name="path"/>, with the route's values.</summary>
    public async Task<(DesignedPageRecord Page, IReadOnlyDictionary<string, string> Values)?> MatchAsync(string bucket, string path)
    {
        foreach (var page in (await ListPublishedAsync()).Where(page => page.Bucket == bucket))
        {
            if (DesignedPageRoutes.TryMatch(page.Route, path, out var values))
            {
                return (page, values);
            }
        }

        return null;
    }

    public async Task<DesignedPageRecord> SaveDraftAsync(string? contentItemId, DesignedPagePart values)
    {
        var item = contentItemId is null ? null : await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);
        if (item is null)
        {
            item = await contentManager.NewAsync(DesignedPagesConstants.ContentType);
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

    private static void Apply(ContentItem item, DesignedPagePart values)
    {
        item.DisplayText = string.IsNullOrWhiteSpace(values.Title) ? values.Name : values.Title;
        item.Alter<DesignedPagePart>(part =>
        {
            part.Name = values.Name.Trim();
            part.Route = DesignedPageRoutes.Normalize(values.Route);
            part.Bucket = values.Bucket.Trim().ToLowerInvariant();
            part.Title = string.IsNullOrWhiteSpace(values.Title) ? null : values.Title.Trim();
            part.Description = string.IsNullOrWhiteSpace(values.Description) ? null : values.Description.Trim();
            part.AllowAnonymous = values.AllowAnonymous;
            part.Source = values.Source ?? string.Empty;
        });
    }

    private static DesignedPageRecord? ToRecord(ContentItem? item) =>
        item?.As<DesignedPagePart>() is { } part && !string.IsNullOrEmpty(part.Name)
            ? new(item.ContentItemId, item.ContentItemVersionId, part.Name, part.Route, part.Bucket, part.Title, part.Description, part.AllowAnonymous, part.Source, item.Published, item.ModifiedUtc)
            : null;
}

/// <summary>
/// Route patterns of designed pages: literal segments (case-insensitive), <c>{name}</c>,
/// <c>{name?}</c>, <c>{name:constraint}</c> (the constraint is not checked here) and a final
/// <c>{*name}</c>. Values are strings; the page's tree reads them as <c>Model.Route</c>.
/// </summary>
public static class DesignedPageRoutes
{
    public static string Normalize(string? route) => "/" + (route ?? string.Empty).Trim().Trim('/');

    public static bool IsValid(string? route, out string? problem)
    {
        problem = null;
        var segments = Normalize(route).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (!IsParameter(segment))
            {
                if (segment.IndexOfAny(['{', '}', '?', '#']) >= 0)
                {
                    problem = $"'{segment}' is not a valid route segment.";
                    return false;
                }

                continue;
            }

            var (name, catchAll, _) = Parse(segment);
            if (name.Length == 0 || !names.Add(name))
            {
                problem = $"'{segment}' needs a parameter name of its own.";
                return false;
            }

            if (catchAll && index != segments.Length - 1)
            {
                problem = "A catch-all parameter must be the last segment.";
                return false;
            }
        }

        return true;
    }

    public static bool TryMatch(string pattern, string path, out IReadOnlyDictionary<string, string> values)
    {
        var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        values = captured;
        var templateSegments = Normalize(pattern).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = Normalize(path.Split('?', '#')[0]).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (!IsParameter(segment))
            {
                if (index >= pathSegments.Length || !string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            var (name, catchAll, optional) = Parse(segment);
            if (catchAll)
            {
                captured[name] = string.Join('/', pathSegments.Skip(index).Select(Uri.UnescapeDataString));
                return true;
            }

            if (index >= pathSegments.Length)
            {
                if (optional)
                {
                    continue;
                }

                return false;
            }

            captured[name] = Uri.UnescapeDataString(pathSegments[index]);
        }

        return pathSegments.Length <= templateSegments.Length;
    }

    private static bool IsParameter(string segment) => segment.StartsWith('{') && segment.EndsWith('}');

    private static (string Name, bool CatchAll, bool Optional) Parse(string segment)
    {
        var inner = segment[1..^1];
        var catchAll = inner.StartsWith('*');
        inner = inner.TrimStart('*');
        var optional = inner.EndsWith('?');
        inner = inner.TrimEnd('?');
        var colon = inner.IndexOf(':');
        return ((colon < 0 ? inner : inner[..colon]).Trim(), catchAll, optional);
    }
}

/// <summary>
/// A published designed page's tree as a shipped template named <c>DesignedPage__{Name}</c>
/// (decision 9: its stored tree), so the page renders through the same evaluation, limits and
/// node filter as every template. A draft under preview binds through <see cref="TemplatePreview"/>.
/// </summary>
public sealed class DesignedPageTemplates(DesignedPageStore pages) : IShippedTemplateProvider
{
    public async Task<IReadOnlyList<TemplateRecord>> GetTemplatesAsync() =>
        [.. (await pages.ListPublishedAsync()).Select(page => new TemplateRecord(
            page.ContentItemId, page.ContentItemVersionId, page.ShapeName, Theme: null, DisplayType: null, page.Source,
            page.Title, Published: true, Latest: true, page.ModifiedUtc))];
}

/// <summary>
/// The permission sets of § 10, assigned per page: <b>Design</b> (edit its tree), <b>Schema</b>
/// (see the types, fields, queries and components its design may use, never data), <b>Data</b>
/// (view it with real data, at run time) and <b>Publish</b> (make its draft live). Shared
/// component editing stays tenant-wide (<see cref="Permissions.ManageSharedComponents"/>).
/// </summary>
public sealed class DesignedPagePermissions(DesignedPageStore pages) : IPermissionProvider
{
    /// <summary>Creates pages and holds every page's sets (the shell-level assignment).</summary>
    public static readonly Permission ManageDesignedPages = new("ManageDesignedPages", "Manage designed pages", isSecurityCritical: true);

    public static Permission Design(string page) => new($"DesignPage_{page}", $"Design the page '{page}'", [ManageDesignedPages], isSecurityCritical: true);

    public static Permission Schema(string page) => new($"ViewPageSchema_{page}", $"See the schema for designing the page '{page}'", [ManageDesignedPages, Design(page)]);

    public static Permission Data(string page) => new($"ViewPage_{page}", $"View the page '{page}' with its data", [ManageDesignedPages]);

    public static Permission Publish(string page) => new($"PublishPage_{page}", $"Publish the page '{page}'", [ManageDesignedPages], isSecurityCritical: true);

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission> { ManageDesignedPages };
        foreach (var page in await pages.ListLatestAsync())
        {
            permissions.AddRange([Design(page.Name), Schema(page.Name), Data(page.Name), Publish(page.Name)]);
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype { Name = PlatformConstants.Roles.Administrator, Permissions = [ManageDesignedPages] },
    ];
}
