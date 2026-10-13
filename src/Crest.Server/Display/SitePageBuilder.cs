using Crest.Access;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.ContentManagement.Routing;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Layout;
using Crest.DisplayManagement.ModelBinding;
using Crest.Settings;
using Crest.Templates.Services;
using Microsoft.Extensions.Options;

namespace Crest.Display;

/// <summary>The site settings of the display system (docs/blazor-display.md decision 10).</summary>
public sealed class SiteDisplaySettings
{
    /// <summary>
    /// Whether a published, routable item whose type has no template gets a page built from its
    /// drivers and placement (the default <c>Content</c> rendering). Off by default: the API is on
    /// for every type, a page exists only when a template is assigned (decision 10).
    /// </summary>
    public bool RenderUnassignedContent { get; set; }
}

/// <summary>A site page: the status it answers with, its title and its tree.</summary>
public sealed record SitePage(int Status, string? Title, DisplayTree? Tree);

/// <summary>
/// Builds a public page (rework step 3, § 6 and § 6a): the path resolves to a content item
/// through Autoroute (the home route for "/"); the viewer must be allowed to view it; its type
/// needs an assigned template unless the tenant renders unassigned types; then the layout is
/// filled (the item into <c>Content</c>, the main menu into <c>Navigation</c>, the layer rules'
/// components through <see cref="ILayoutTreeContributor"/>s) and mapped to one tree, which the
/// node filter cuts to what the viewer may see. A miss or a refusal is still a page: the layout
/// with the error shape (<c>NotFound</c>, <c>Forbidden</c>), the template chain's last fallback.
/// </summary>
public sealed class SitePageBuilder(
    ISiteService siteService,
    IOptions<AutorouteOptions> autorouteOptions,
    IAutorouteEntries autorouteEntries,
    IContentManager contentManager,
    IContentHandleManager handles,
    IContentItemDisplayManager displayManager,
    IUpdateModelAccessor updaters,
    ILayoutAccessor layouts,
    IShapeFactory shapeFactory,
    IDisplayTreeBuilder trees,
    IDisplayTreeFilter filter,
    ICallerContextAccessor callers,
    IAccessDecision decision,
    TemplateResolver templates,
    IEnumerable<ILayoutTreeContributor> contributors)
{
    public const string MainMenuAlias = "alias:main-menu";

    private const string DetailDisplayType = "Detail";

    public async Task<SitePage> BuildAsync(string? path, CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var caller = callers.Current;
        var contentItemId = await ResolveAsync(path, site);
        var item = contentItemId is null ? null : await contentManager.GetAsync(contentItemId, VersionOptions.Published);

        // No caller means no gate ran for this request: nothing is shown.
        if (item is null || caller is null)
        {
            return await ErrorPageAsync(404, "NotFound", "Not found", site, caller, cancellationToken);
        }

        var verdict = await decision.DecideAsync(caller, Contents.CommonPermissions.ViewContent.Name, item, cancellationToken);
        if (!verdict.IsAllowed)
        {
            return verdict.Verdict == AccessVerdict.NotFound
                ? await ErrorPageAsync(404, "NotFound", "Not found", site, caller, cancellationToken)
                : await ErrorPageAsync(403, "Forbidden", "Forbidden", site, caller, cancellationToken);
        }

        var shape = await displayManager.BuildDisplayAsync(item, updaters.ModelUpdater, DetailDisplayType);

        // Decision 10: no page is generated for a type without a template. The template is asked
        // for by the names the item's shape binds by (Content__{Type}, or its stereotype's).
        if (!site.As<SiteDisplaySettings>().RenderUnassignedContent
            && await templates.ResolveAsync(TemplateResolver.NamesOf(shape.Metadata.Type, [.. shape.Metadata.Alternates]), DetailDisplayType) is null)
        {
            return await ErrorPageAsync(404, "NotFound", "Not found", site, caller, cancellationToken);
        }

        var title = Title(item.DisplayText, site);
        return new SitePage(200, title, await BuildLayoutAsync(shape, item, item.DisplayText, site, caller, cancellationToken));
    }

    private async Task<string?> ResolveAsync(string? path, ISite site)
    {
        var normalized = "/" + (path ?? string.Empty).Trim().Trim('/');
        if (normalized == "/")
        {
            return site.HomeRoute is { } home && home.TryGetValue(autorouteOptions.Value.ContentItemIdKey, out var id) ? id as string : null;
        }

        const string itemPrefix = "/Contents/ContentItems/";
        if (normalized.StartsWith(itemPrefix, StringComparison.OrdinalIgnoreCase) && normalized.Length > itemPrefix.Length && !normalized[itemPrefix.Length..].Contains('/'))
        {
            return normalized[itemPrefix.Length..];
        }

        var (found, entry) = await autorouteEntries.TryGetEntryByPathAsync(normalized);
        return found ? entry.ContentItemId : null;
    }

    private async Task<SitePage> ErrorPageAsync(int status, string shapeType, string title, ISite site, CallerContext? caller, CancellationToken cancellationToken)
    {
        var shape = await shapeFactory.CreateAsync(shapeType, Arguments.From(new { Status = status }));
        return new SitePage(status, Title(title, site), await BuildLayoutAsync(shape, item: null, title, site, caller, cancellationToken));
    }

    private async Task<DisplayTree> BuildLayoutAsync(IShape content, ContentItem? item, string? pageTitle, ISite site, CallerContext? caller, CancellationToken cancellationToken)
    {
        var layout = await layouts.GetLayoutAsync();
        layout.Properties["SiteName"] = site.SiteName;
        layout.Properties["Title"] = pageTitle;
        await layout.Zones["Content"].AddAsync(content);

        if (!string.IsNullOrEmpty(await handles.GetContentItemIdAsync(MainMenuAlias)))
        {
            var menu = await shapeFactory.CreateAsync("Menu", Arguments.From(new { Alias = MainMenuAlias }));
            await layout.Zones["Navigation"].AddAsync(menu);
        }

        var context = new LayoutTreeContext(layout, item, caller);
        foreach (var contributor in contributors.OrderBy(contributor => contributor.Order))
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        var tree = await trees.BuildAsync(layout, cancellationToken: cancellationToken);
        var root = await filter.FilterAsync(tree.Root, caller, cancellationToken) ?? DisplayNode.Markup(tree.Root.Id, string.Empty);
        return new DisplayTree { Root = root, DisplayType = DetailDisplayType, ScopeSignature = tree.ScopeSignature };
    }

    private static string Title(string? title, ISite site) =>
        string.IsNullOrWhiteSpace(title) ? site.SiteName ?? string.Empty
        : string.IsNullOrWhiteSpace(site.SiteName) ? title
        : $"{title} - {site.SiteName}";
}
