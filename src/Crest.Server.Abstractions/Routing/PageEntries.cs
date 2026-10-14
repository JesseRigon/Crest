using Crest.Routing;

namespace Crest.Routing
{
    /// <summary>Shorthands for the entries a <see cref="IPageSource"/> declares.</summary>
    public static class PageEntries
    {
        /// <summary>A menu group: expands, opens nothing, visible while any child is.</summary>
        public static PageEntry Group(string id, string caption, string? parent = null, string? position = null, string? icon = null) =>
            new() { Id = id, Menu = new PageMenu(caption, parent, position, icon) };

        /// <summary>
        /// A legacy MVC admin page, in the menu until it is ported: its route values are resolved
        /// to its URL by the tenant's link generator; MVC authorizes the page itself.
        /// </summary>
        public static PageEntry Legacy(
            string id,
            string caption,
            string? parent,
            string area,
            string controller,
            string action,
            string[] permissions,
            string? position = null,
            string? icon = null,
            IReadOnlyDictionary<string, object?>? values = null)
        {
            var route = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["area"] = area,
                ["controller"] = controller,
                ["action"] = action,
            };

            foreach (var (key, value) in values ?? new Dictionary<string, object?>())
            {
                route[key] = value;
            }

            return new()
            {
                Id = id,
                Permissions = permissions,
                Menu = new PageMenu(caption, parent, position, icon) { LegacyRoute = route },
            };
        }

        /// <summary>A legacy site settings group (<c>Crest.Settings</c>' admin editor for <paramref name="groupId"/>).</summary>
        public static PageEntry LegacySettings(string id, string caption, string? parent, string groupId, string[] permissions, string? position = null, string? icon = null) =>
            Legacy(id, caption, parent, "Crest.Settings", "Admin", "Index", permissions, position, icon, new Dictionary<string, object?> { ["groupId"] = groupId });

        /// <summary>A menu item for a native page whose route is not its own component's (a page shared by several items, a query-string view).</summary>
        public static PageEntry Link(string id, string caption, string? parent, string url, string[] permissions, string? position = null, string? icon = null) =>
            new()
            {
                Id = id,
                Permissions = permissions,
                Menu = new PageMenu(caption, parent, position, icon) { Url = url },
            };
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class PageSourceServiceCollectionExtensions
    {
        /// <summary>Registers a declared page source (read once when the shell builds).</summary>
        public static IServiceCollection AddPageSource<TSource>(this IServiceCollection services)
            where TSource : class, IPageSource
        {
            services.AddScoped<IPageSource, TSource>();
            return services;
        }

        /// <summary>Registers a dynamic page source (read per request).</summary>
        public static IServiceCollection AddDynamicPageSource<TSource>(this IServiceCollection services)
            where TSource : class, IDynamicPageSource
        {
            services.AddScoped<IDynamicPageSource, TSource>();
            return services;
        }
    }
}
