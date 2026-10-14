using System.Collections.Concurrent;
using System.Reflection;
using Crest.Admin;
using Crest.Components.Modules;
using Crest.DisplayManagement.Components;
using Crest.Environment.Shell;
using Crest.Security.Permissions;
using Crest.Themes.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Routing;

/// <summary>
/// Shell-lifetime cache of the declared page set, keyed by the active (admin theme, site theme)
/// pair, since that decides whether the admin shell's pages are served. A feature or theme change
/// rebuilds the tenant shell, which drops this with it; no further invalidation is needed.
/// </summary>
public sealed class PageRegistryCache
{
    internal ConcurrentDictionary<(string? AdminThemeId, string? SiteThemeId), Task<PageSet>> Declared { get; } = new();
}

/// <summary>
/// The page registry (docs/admin-menu.md › The page registry). The declared layer is the routed
/// components of every shell's client assemblies with their <see cref="PageEntryAttribute"/>
/// declarations, plus every <see cref="IPageSource"/>; the dynamic entries come from the
/// <see cref="IDynamicPageSource"/>s per request.
/// </summary>
/// <remarks>
/// Admin pages are served only while the Blazor admin theme is active; a module client library
/// whose module is disabled (or not installed) is absent; a <c>DefaultTenantOnly</c> page exists
/// only on the Default tenant (a source decides that for its own entries). Site and member pages are always registered: a tenant's admin,
/// site and member themes are independent, simultaneously active settings.
/// </remarks>
public sealed class PageRegistry(
    IAdminThemeService adminThemeService,
    ISiteThemeService siteThemeService,
    IBlazorAdminThemeDetector blazorAdminThemeDetector,
    IEnabledModuleAssemblies enabledModules,
    IEnumerable<IPageSource> sources,
    IEnumerable<IDynamicPageSource> dynamicSources,
    IPermissionService permissions,
    IOptions<ShellRegistryOptions> shellRegistry,
    ShellSettings shellSettings,
    PageRegistryCache cache,
    ILogger<PageRegistry> logger) : IPageRegistry
{
    /// <summary>The admin shell's default landing page.</summary>
    public const string AdminLanding = "/Dashboard";

    /// <summary>The site shell's default landing page (the content page).</summary>
    public const string SiteLanding = "/";

    private PageSet? _all;

    public async Task<PageSet> GetDeclaredAsync()
    {
        var key = (await adminThemeService.GetAdminThemeNameAsync(), await siteThemeService.GetSiteThemeNameAsync());
        return await cache.Declared.GetOrAdd(key, _ => BuildAsync());
    }

    public async Task<PageSet> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_all is not null)
        {
            return _all;
        }

        var declared = await GetDeclaredAsync();
        var dynamic = new List<PageEntry>();
        foreach (var source in dynamicSources)
        {
            dynamic.AddRange((await source.GetPagesAsync(declared, cancellationToken)).Select(entry => Stamp(entry, source)));
        }

        return _all = dynamic.Count == 0 ? declared : declared.With(dynamic);
    }

    private async Task<PageSet> BuildAsync()
    {
        var isBlazorAdminActive = await blazorAdminThemeDetector.IsBlazorAdminThemeActiveAsync();
        var enabled = await enabledModules.GetEnabledAsync();

        var entries = new List<PageEntry>();
        foreach (var shell in new[] { ShellId.Admin, ShellId.Site, ShellId.Member })
        {
            if (shell == ShellId.Admin && !isBlazorAdminActive)
            {
                continue;
            }

            foreach (var assembly in ShellAssemblies.InShell(shell))
            {
                var name = assembly.GetName().Name;
                if (ModuleClientAssemblies.IsModuleClient(name) && !enabled.Contains(name!))
                {
                    continue;
                }

                entries.AddRange(Scan(assembly, shell));
            }
        }

        foreach (var source in sources)
        {
            entries.AddRange(source.GetPages().Select(entry => Stamp(entry, source)));
        }

        await ReportAsync(entries);
        return new PageSet(entries);
    }

    private static PageEntry Stamp(PageEntry entry, object source) =>
        entry.DeclaredBy is null ? entry with { DeclaredBy = source.GetType().FullName } : entry;

    // One entry per declaration of a component: the routes it covers, its permissions and its
    // menu placement. Routes no declaration covers form one undeclared entry, which the route
    // table serves and the gate denies.
    private IEnumerable<PageEntry> Scan(Assembly assembly, ShellId shell)
    {
        var landing = shell == ShellId.Admin ? AdminLanding : shell == ShellId.Site ? SiteLanding : null;
        foreach (var component in ComponentCatalogs.ForAssembly(assembly).DescribePages().GroupBy(page => page.Type))
        {
            var declarations = component.GroupBy(page => page.Entry).ToArray();
            foreach (var declaration in declarations)
            {
                var attribute = declaration.Key;
                if (attribute?.DefaultTenantOnly == true && !shellSettings.IsDefaultShell())
                {
                    continue;
                }

                var routes = declaration.Select(page => page.Template).ToArray();
                var first = declaration.First();
                var id = attribute?.Id
                    ?? (declarations.Length == 1 ? component.Key.FullName! : $"{component.Key.FullName}:{routes[0]}");

                yield return new PageEntry
                {
                    Id = id,
                    Shell = shell,
                    Routes = routes,
                    Component = component.Key,
                    Permissions = attribute?.Permissions ?? [],
                    AllowsAnonymous = first.AllowsAnonymous,
                    IsOrganizationFree = component.Key.IsDefined(typeof(OrganizationFreePageAttribute), inherit: false),
                    IsDefaultLanding = landing is not null && routes.Contains(landing, StringComparer.OrdinalIgnoreCase),
                    ClientOnly = attribute?.ClientOnly ?? false,
                    DeclaredBySource = attribute?.DeclaredBySource ?? false,
                    DeclaredBy = component.Key.FullName,
                    Menu = attribute?.Caption is { Length: > 0 } caption
                        ? new PageMenu(caption, attribute.Parent, attribute.Position, attribute.Icon)
                        {
                            Url = routes.FirstOrDefault(route => !route.Contains('{')),
                        }
                        : null,
                };
            }
        }
    }

    // Declaration mistakes are reported, never fatal: a duplicate id keeps its first declaration,
    // an undeclared admin page and an unknown permission deny.
    private async Task ReportAsync(List<PageEntry> entries)
    {
        foreach (var duplicate in entries.GroupBy(entry => (entry.Shell, entry.Id)).Where(group => group.Count() > 1))
        {
            logger.LogWarning(
                "Page registry: the {Shell} entry id '{Id}' is declared more than once ({DeclaredBy}); the first declaration is kept.",
                duplicate.Key.Shell,
                duplicate.Key.Id,
                string.Join(", ", duplicate.Select(entry => entry.DeclaredBy)));
        }

        var fixedPages = shellRegistry.Value.Shells
            .Where(descriptor => descriptor.Shell == ShellId.Admin && descriptor.FixedPage is not null)
            .Select(descriptor => descriptor.FixedPage!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => entry.Shell == ShellId.Admin
            && entry.Component is not null
            && entry.Permissions.Count == 0
            && !entry.AllowsAnonymous
            && !entry.DeclaredBySource
            && !entry.Routes.All(fixedPages.Contains)))
        {
            logger.LogWarning(
                "Page registry: the admin page {Component} ({Routes}) declares no permission, so it is denied to everyone. Declare one with [PageEntry(...)].",
                entry.Component!.FullName,
                string.Join(", ", entry.Routes));
        }

        var known = (await permissions.GetPermissionsAsync()).Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in entries.Where(entry => entry.Component is not null))
        {
            foreach (var unknown in entry.Permissions.Where(permission => !known.Contains(permission)))
            {
                logger.LogWarning(
                    "Page registry: the page {Component} declares the permission '{Permission}', which no enabled feature registers; nobody but a super user holds it.",
                    entry.Component!.FullName,
                    unknown);
            }
        }
    }
}
