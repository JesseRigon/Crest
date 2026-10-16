using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Contents.Management.Routing;
using Grove.Display.Management;
using Grove.Display.Management.Components;
using Grove.Display.Management.Layout;
using Grove.Infrastructure.Settings;
using Grove.Display.Templates.Services;
using Microsoft.Extensions.Options;

namespace Grove.Display;

/// <summary>The site settings of the display system (docs/blazor-display.md decision 10).</summary>
public sealed class SiteDisplaySettings
{
    /// <summary>
    /// Whether the default <c>Content</c> template (<see cref="DefaultContentTemplate"/>) runs for a
    /// published, routable item whose type has no template of its own: it lays out the fields the
    /// viewer may see. Off by default: the API is on for every type, a page exists only when a
    /// template is assigned (decision 10).
    /// </summary>
    public bool RenderUnassignedContent { get; set; }
}

/// <summary>
/// The default <c>Content</c> template (decision 10): the item's title and every zone its drivers
/// filled, so what it lists is what the drivers built for the viewer and the node filter let
/// through. A shipped template, the last level of the chain below the tenant's own, and present
/// only while the tenant's <see cref="SiteDisplaySettings.RenderUnassignedContent"/> is on, for the
/// Detail display type; a tenant template named <c>Content</c> replaces it.
/// </summary>
public sealed class DefaultContentTemplate(ISiteService siteService) : IShippedTemplateProvider
{
    public const string Name = "Content";

    public const string Source = """
        {% compose "Content" %}
        {% zone "Header" %}{% component "GroveHeading", text: Model.ContentItem.DisplayText, size: "H1" %}{% children "Header" %}{% endzone %}
        {% zone "Meta" %}{% children "Meta" %}{% endzone %}
        {% zone "Content" %}{% children "Content" %}{% endzone %}
        {% zone "Tags" %}{% children "Tags" %}{% endzone %}
        {% zone "Footer" %}{% children "Footer" %}{% endzone %}
        {% endcompose %}
        """;

    private static readonly TemplateRecord s_template = new(
        "shipped:" + Name, "shipped:" + Name, Name, Theme: null, DisplayType: "Detail", Source,
        "The default content page: the item's title and the fields the viewer may see (decision 10).",
        Published: true, Latest: true, ModifiedUtc: null);

    public async Task<IReadOnlyList<TemplateRecord>> GetTemplatesAsync() =>
        (await siteService.GetSiteSettingsAsync()).As<SiteDisplaySettings>().RenderUnassignedContent ? [s_template] : [];
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
    ILayoutAccessor layouts,
    IShapeFactory shapeFactory,
    IDisplayTreeBuilder trees,
    IDisplayTreeFilter filter,
    ICallerContextAccessor callers,
    IAccessDecision decision,
    TemplateResolver templates,
    DesignedPageStore designedPages,
    IEnumerable<ILayoutTreeContributor> contributors)
{
    public const string MainMenuAlias = "alias:main-menu";

    /// <summary>The shape of the site's default home page, shown at "/" until the tenant sets a home route.</summary>
    public const string DefaultHomeShape = "Home";

    private const string DetailDisplayType = "Detail";

    public async Task<SitePage> BuildAsync(string? path, CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var caller = callers.Current;

        // A designed page of the site bucket owns its route ahead of Autoroute (decision 9).
        if (await designedPages.MatchAsync(Grove.Display.Templates.Models.DesignedPagesConstants.SiteBucket, Normalize(path)) is { } designed)
        {
            return await BuildDesignedAsync(designed.Page, designed.Values, cancellationToken);
        }

        // The site's home before the tenant chose one (no home route): the shipped default home
        // page, the Home shape, which a tenant template named Home replaces.
        if (Normalize(path) == "/" && HomeContentItemId(site) is null)
        {
            return await DefaultHomeAsync(site, caller, cancellationToken);
        }

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

        var shape = await displayManager.BuildDisplayAsync(item, DetailDisplayType);

        // Decision 10: no page is generated for a type without a template. The template is asked
        // for by the names the item's shape binds by (Content__{Type}, or its stereotype's); the
        // default Content template answers only where the tenant turned it on.
        if (await templates.ResolveAsync(TemplateResolver.NamesOf(shape.Metadata.Type, [.. shape.Metadata.Alternates]), DetailDisplayType) is null)
        {
            return await ErrorPageAsync(404, "NotFound", "Not found", site, caller, cancellationToken);
        }

        var title = Title(item.DisplayText, site);
        return new SitePage(200, title, await BuildLayoutAsync(shape, item, item.DisplayText, site, caller, cancellationToken));
    }

    /// <summary>
    /// A designed page (decision 9, § 6a): admitted by its Data permission unless it allows
    /// anonymous callers, its tree bound as <c>DesignedPage__{Name}</c> with the route's values as
    /// <c>Model.Route</c>, rendered inside the site layout like any page. A draft is built here
    /// only under preview (<see cref="TemplatePreview"/> carries its source).
    /// </summary>
    public async Task<SitePage> BuildDesignedAsync(DesignedPageRecord page, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var caller = callers.Current;
        if (caller is null)
        {
            return await ErrorPageAsync(404, "NotFound", "Not found", site, caller, cancellationToken);
        }

        if (!page.AllowAnonymous
            && !(await decision.DecideAsync(caller, DesignedPagePermissions.Data(page.Name).Name, cancellationToken: cancellationToken)).IsAllowed)
        {
            return await ErrorPageAsync(caller.UserId is null ? 404 : 403, caller.UserId is null ? "NotFound" : "Forbidden", caller.UserId is null ? "Not found" : "Forbidden", site, caller, cancellationToken);
        }

        var shape = await shapeFactory.CreateAsync(Grove.Display.Templates.Models.DesignedPagesConstants.ShapeType, Arguments.From(new
        {
            Route = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            PageName = page.Name,
            page.Title,
            page.Description,
        }));
        shape.Metadata.Alternates.Add(page.ShapeName);

        var title = page.Title ?? page.Name;
        return new SitePage(200, Title(title, site), await BuildLayoutAsync(shape, item: null, title, site, caller, cancellationToken));
    }

    /// <summary>The path as pages are keyed: one leading slash, no trailing one.</summary>
    public static string Normalize(string? path) => "/" + (path ?? string.Empty).Trim().Trim('/');

    private async Task<string?> ResolveAsync(string? path, ISite site)
    {
        var normalized = Normalize(path);
        if (normalized == "/")
        {
            return HomeContentItemId(site);
        }

        const string itemPrefix = "/Contents/ContentItems/";
        if (normalized.StartsWith(itemPrefix, StringComparison.OrdinalIgnoreCase) && normalized.Length > itemPrefix.Length && !normalized[itemPrefix.Length..].Contains('/'))
        {
            return normalized[itemPrefix.Length..];
        }

        var (found, entry) = await autorouteEntries.TryGetEntryByPathAsync(normalized);
        return found ? entry.ContentItemId : null;
    }

    private string? HomeContentItemId(ISite site) =>
        site.HomeRoute is { } home && home.TryGetValue(autorouteOptions.Value.ContentItemIdKey, out var id) ? id as string : null;

    private async Task<SitePage> DefaultHomeAsync(ISite site, CallerContext? caller, CancellationToken cancellationToken)
    {
        var shape = await shapeFactory.CreateAsync(DefaultHomeShape, Arguments.From(new { site.SiteName }));
        return new SitePage(200, Title(null, site), await BuildLayoutAsync(shape, item: null, pageTitle: null, site, caller, cancellationToken));
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
