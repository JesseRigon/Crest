using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents;
using Grove.Display.Management;
using Grove.Display.Management.Descriptors;
using Grove.Display.Management.Shapes;
using Grove.Contents.SiteMenu.Models;
using Grove.Utilities;
using Grove.Security.Permissions;

namespace Grove.Contents.SiteMenu;

public class MenuShapes : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("Menu")
            .OnProcessing(async context =>
            {
                var menu = context.Shape;

                // Menu population is executed when processing the shape so that its value
                // can be cached. IShapeDisplayEvents is called before the ShapeDescriptor
                // events and thus this code can be cached.

                var shapeFactory = context.ServiceProvider.GetRequiredService<IShapeFactory>();
                var contentManager = context.ServiceProvider.GetRequiredService<IContentManager>();
                var handleManager = context.ServiceProvider.GetRequiredService<IContentHandleManager>();

                var contentItemId = menu.TryGetProperty("Alias", out object alias) && alias != null
                    ? await handleManager.GetContentItemIdAsync(alias.ToString())
                    : menu.TryGetProperty("ContentItemId", out object id)
                        ? id.ToString()
                        : null;

                if (contentItemId == null)
                {
                    return;
                }

                menu.Classes.Add("menu");

                var menuContentItem = await contentManager.GetAsync(contentItemId);

                if (menuContentItem == null)
                {
                    return;
                }

                menu.Properties["ContentItem"] = menuContentItem;

                menu.Properties["MenuName"] = menuContentItem.DisplayText;

                var menuItems = MenuItemsEmbedding.Items(menuContentItem);

                var differentiator = FormatName(menu.GetProperty<string>("MenuName"));

                if (!string.IsNullOrEmpty(differentiator))
                {
                    // Get cached alternate and add it efficiently
                    var cachedAlternates = MenuAlternatesFactory.GetMenuAlternates(differentiator);
                    menu.Metadata.Alternates.AddRange(cachedAlternates);
                    menu.Metadata.Differentiator = differentiator;
                    menu.Classes.Add(("menu-" + differentiator).HtmlClassify());
                }

                // The first level of menu item shapes is created.
                // Each other level is created when the menu item is displayed.

                var permissionService = context.ServiceProvider.GetRequiredService<IPermissionService>();
                var httpContextAccessor = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                var authorizationService = context.ServiceProvider.GetRequiredService<IAuthorizationService>();

                foreach (var contentItem in menuItems)
                {
                    if (!await ShouldCreateAsync(contentItem, contentManager, permissionService, authorizationService, httpContextAccessor.HttpContext?.User))
                    {
                        continue;
                    }

                    var shape = await shapeFactory.CreateAsync("MenuItem", Arguments.From(new MenuItemArguments
                    {
                        ContentItem = contentItem,
                        Level = 0,
                        Menu = menu,
                    }));

                    shape.Metadata.Differentiator = differentiator;

                    // Don't use Items.Add() or the collection won't be sorted
                    await ((Shape)menu).AddAsync(shape);
                }
            });

        builder.Describe("MenuItem")
            .OnDisplaying(async context =>
            {
                var menuItem = context.Shape;
                var menuContentItem = menuItem.GetProperty<ContentItem>("ContentItem");
                var menu = menuItem.GetProperty<IShape>("Menu");
                var level = menuItem.GetProperty<int>("Level");
                var differentiator = menuItem.Metadata.Differentiator;

                var shapeFactory = context.ServiceProvider.GetRequiredService<IShapeFactory>();

                if (MenuItemsEmbedding.Items(menuContentItem) is { Count: > 0 } menuItems)
                {
                    var permissionService = context.ServiceProvider.GetRequiredService<IPermissionService>();
                    var httpContextAccessor = context.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                    var authorizationService = context.ServiceProvider.GetRequiredService<IAuthorizationService>();
                    var contentManager = context.ServiceProvider.GetRequiredService<IContentManager>();

                    foreach (var contentItem in menuItems)
                    {
                        if (!await ShouldCreateAsync(contentItem, contentManager, permissionService, authorizationService, httpContextAccessor.HttpContext?.User))
                        {
                            continue;
                        }

                        var shape = await shapeFactory.CreateAsync("MenuItem", Arguments.From(new MenuItemArguments
                        {
                            ContentItem = contentItem,
                            Level = level + 1,
                            Menu = menu,
                        }));

                        shape.Metadata.Differentiator = differentiator;

                        // Don't use Items.Add() or the collection won't be sorted
                        await menuItem.AddAsync(shape);
                    }
                }

                // Get cached alternates and add them efficiently
                var cachedAlternates = MenuItemAlternatesFactory.GetMenuItemAlternates(
                    menuContentItem.ContentItem.ContentType,
                    differentiator,
                    level);

                menuItem.Metadata.Alternates.AddRange(cachedAlternates);

                // What a component renders a menu item from (docs/blazor-display.md, rework
                // step 3): its text and resolved link, so the display tree carries resolved
                // values and no client re-resolves the menu item content item.
                await SetLinkAsync(menuItem, menuContentItem, context.ServiceProvider);
            });

        builder.Describe("MenuItemLink")
            .OnDisplaying(displaying =>
            {
                var menuItem = displaying.Shape;
                var level = menuItem.GetProperty<int>("Level");
                var differentiator = menuItem.Metadata.Differentiator;

                var menuContentItem = menuItem.GetProperty<ContentItem>("ContentItem");

                // Get cached alternates and add them efficiently
                var cachedAlternates = MenuItemAlternatesFactory.GetMenuItemLinkAlternates(
                    menuContentItem.ContentItem.ContentType,
                    differentiator,
                    level);

                menuItem.Metadata.Alternates.AddRange(cachedAlternates);
            });

        return ValueTask.CompletedTask;
    }

    private static async Task SetLinkAsync(IShape menuItem, ContentItem menuContentItem, IServiceProvider services)
    {
        menuItem.Properties["Text"] = menuContentItem.DisplayText;

        if (menuContentItem.TryGet<LinkMenuItemFieldSet>(out var link))
        {
            // "~/about" is tenant-relative: relative to the document base, which is the tenant's.
            menuItem.Properties["Url"] = link.Url is { } url && url.StartsWith("~/", StringComparison.Ordinal) ? url[2..] : link.Url;
            menuItem.Properties["Target"] = link.Target;
            return;
        }

        if (menuContentItem.TryGet<HtmlMenuItemFieldSet>(out var html))
        {
            // The link's text is markup; it reaches the tree sanitized, whatever the part's
            // settings, since the tree's markup is rendered as is.
            menuItem.Properties["Html"] = services.GetRequiredService<Grove.Infrastructure.Html.IHtmlSanitizerService>().Sanitize(html.Html ?? string.Empty);
            menuItem.Properties["Url"] = html.Url is { } htmlUrl && htmlUrl.StartsWith("~/", StringComparison.Ordinal) ? htmlUrl[2..] : html.Url;
            menuItem.Properties["Target"] = html.Target;
            return;
        }

        if (menuContentItem.Content["ContentMenuItem"]?["SelectedContentItem"]?["SelectedIds"] is System.Text.Json.Nodes.JsonArray { Count: > 0 } ids
            && ids[0]?.GetValue<string>() is { Length: > 0 } targetId
            && services.GetService<IHttpContextAccessor>()?.HttpContext is { } httpContext)
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            if (await contentManager.GetAsync(targetId, VersionOptions.Published) is { } target)
            {
                var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(target);
                var linkGenerator = services.GetRequiredService<Microsoft.AspNetCore.Routing.LinkGenerator>();
                menuItem.Properties["Url"] = Microsoft.AspNetCore.Routing.LinkGeneratorRouteValuesAddressExtensions.GetPathByRouteValues(linkGenerator, httpContext, string.Empty, metadata.DisplayRouteValues);
            }
        }
    }

    private async static Task<bool> ShouldCreateAsync(
        ContentItem contentItem,
        IContentManager contentManager,
        IPermissionService permissionService,
        IAuthorizationService authorizationService,
        ClaimsPrincipal user)
    {
        if (contentItem.TryGet<MenuItemPermissionFieldSet>(out var permissionPart) &&
            permissionPart.PermissionNames is not null &&
            permissionPart.PermissionNames.Length > 0)
        {
            var permissions = await permissionService.FindByNamesAsync(permissionPart.PermissionNames);

            foreach (var permission in permissions)
            {
                if (await authorizationService.AuthorizeAsync(user, permission, contentItem))
                {
                    continue;
                }

                return false;
            }
        }

        if (contentItem.TryGet<ContentMenuItemFieldSet>(out var menuItemPart))
        {
            string contentItemId = (menuItemPart.ContentItem.Content["ContentMenuItem"]?["SelectedContentItem"]?["SelectedIds"] as System.Text.Json.Nodes.JsonArray)?.FirstOrDefault()?.GetValue<string>();

            if (string.IsNullOrEmpty(contentItemId))
            {
                return false;
            }

            if (menuItemPart.CheckContentPermissions)
            {
                var displayItem = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

                if (displayItem is null)
                {
                    return false;
                }

                await contentManager.LoadAsync(displayItem);

                if (!await authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, displayItem))
                {
                    return false;
                }
            }
        }

        return true;
    }
    /// <summary>
    /// Converts "foo-ba r" to "FooBaR".
    /// </summary>
    private static string FormatName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        name = name.Trim();
        var nextIsUpper = true;
        var result = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (c == '-' || char.IsWhiteSpace(c))
            {
                nextIsUpper = true;
                continue;
            }

            if (nextIsUpper)
            {
                result.Append(c.ToString().ToUpper());
            }
            else
            {
                result.Append(c);
            }

            nextIsUpper = false;
        }

        return result.ToString();
    }
}

[GenerateArguments]
internal sealed partial class MenuItemArguments
{
    public ContentItem ContentItem { get; set; }
    public int Level { get; set; }
    public IShape Menu { get; set; }
}
