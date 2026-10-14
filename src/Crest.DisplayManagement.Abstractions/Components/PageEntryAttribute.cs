namespace Crest.DisplayManagement.Components;

/// <summary>
/// A routed component's page registry declaration (docs/admin-menu.md › The page registry): the
/// permissions that admit a caller to it and, optionally, its place in the shell's menu. The
/// routes are the component's own <c>@page</c> templates and the shell is its assembly's bucket;
/// both are read from there, never re-typed. The route gate and the menu read the same
/// permissions: a caller holding any one of them may open the page and sees its menu item.
/// </summary>
/// <remarks>
/// A component with several <c>@page</c> routes that are distinct pages in the menu (two
/// settings groups served by one editor) carries one attribute per page, each naming its
/// <see cref="Route"/>; an attribute without one covers every route no other attribute names.
/// An admin page without any declaration is denied to everyone, and the shell build names it in
/// a warning. A page that admits anonymous callers (<c>[AllowAnonymous]</c>) needs none.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class PageEntryAttribute(params string[] permissions) : Attribute
{
    /// <summary>Permission names, any one of which admits the caller.</summary>
    public string[] Permissions { get; } = permissions;

    /// <summary>
    /// The stable entry id the tenant's menu edits and each user's hidden items are keyed by.
    /// Required when the page is placed in the menu; defaults to the component's full type name
    /// (plus the route, when the component declares several entries).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>The one <c>@page</c> template this declaration covers; every unclaimed one when absent.</summary>
    public string? Route { get; set; }

    /// <summary>The id of the menu entry this page sits under; <c>null</c> at the top level.</summary>
    public string? Parent { get; set; }

    /// <summary>The invariant menu caption. A page with no caption is not in the menu.</summary>
    public string? Caption { get; set; }

    /// <summary>The position among its siblings (navigation position syntax: "5", "after.15"); by caption when absent.</summary>
    public string? Position { get; set; }

    /// <summary>The menu icon ("@iconify:mdi:toggle-switch", or a legacy class such as "fa fa-book").</summary>
    public string? Icon { get; set; }

    /// <summary>The page runs only in the browser: its document renders InteractiveWebAssembly, for a page whose services exist only in the WASM client.</summary>
    public bool ClientOnly { get; set; }

    /// <summary>The page exists only on the Default tenant (tenant management, global reference data).</summary>
    public bool DefaultTenantOnly { get; set; }

    /// <summary>
    /// The page's concrete routes, permissions and menu items are declared by a page source (a
    /// kind registry's generic page, one entry per kind); the component's own template admits
    /// nobody and is not reported as undeclared.
    /// </summary>
    public bool DeclaredBySource { get; set; }
}
