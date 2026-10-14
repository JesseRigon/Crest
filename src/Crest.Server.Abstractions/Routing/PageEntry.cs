namespace Crest.Routing;

/// <summary>
/// One entry of the page registry (docs/admin-menu.md › The page registry): a page, a legacy MVC
/// page, or a menu group, declared once with its routes, shell, permissions and menu placement.
/// The route table, the route gate and the admin menu's default tree are all read from these.
/// </summary>
/// <remarks>
/// Native pages are declared on the component (<c>[PageEntry]</c> next to <c>@page</c>) and read
/// by the registry's scan; legacy pages, menu groups and generated pages (kinds, content types)
/// come from <see cref="IPageSource"/>s and <see cref="IDynamicPageSource"/>s.
/// </remarks>
public sealed record PageEntry
{
    /// <summary>
    /// The stable id: unique per shell, never a caption. The tenant's menu edits and each user's
    /// hidden items are keyed by it.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The shell the page belongs to (<see cref="ShellId"/>).</summary>
    public ShellId Shell { get; init; } = ShellId.Admin;

    /// <summary>Shell-relative route templates the gate admits with <see cref="Permissions"/>; empty for a group or a legacy page.</summary>
    public IReadOnlyList<string> Routes { get; init; } = [];

    /// <summary>The routed component that renders the page; null for a group, a legacy page, or a gate-only entry a source declares for a generic page.</summary>
    public Type? Component { get; init; }

    /// <summary>Permission names, any one of which admits a caller (to the page and to its menu item).</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Served without the shell's login redirect or route authorization, and rendered shell-less (<c>[AllowAnonymous]</c>).</summary>
    public bool AllowsAnonymous { get; init; }

    /// <summary>A member page served at the member shell's base rather than under an organization.</summary>
    public bool IsOrganizationFree { get; init; }

    /// <summary>The shell's default landing page.</summary>
    public bool IsDefaultLanding { get; init; }

    /// <summary>The page runs only in the browser (its document renders InteractiveWebAssembly).</summary>
    public bool ClientOnly { get; init; }

    /// <summary>The component's own template admits nobody: a source declares its concrete routes.</summary>
    public bool DeclaredBySource { get; init; }

    /// <summary>Where the page sits in its shell's menu; null when it is not in the menu.</summary>
    public PageMenu? Menu { get; init; }

    /// <summary>
    /// The type that declared the entry (a component, a source): the context its caption's
    /// shipped translation is looked up under.
    /// </summary>
    public string? DeclaredBy { get; init; }
}

/// <summary>A page's place in its shell's menu.</summary>
/// <param name="Caption">The invariant caption, translated where displayed.</param>
/// <param name="Parent">The parent entry's id; null at the top level.</param>
/// <param name="Position">Navigation position ("5", "after.15", "end.100"); siblings without one sort by caption.</param>
/// <param name="Icon">An icon ("@iconify:mdi:palette", "fa fa-book"); null for none.</param>
public sealed record PageMenu(string Caption, string? Parent = null, string? Position = null, string? Icon = null)
{
    /// <summary>
    /// The shell-relative URL the item opens (a route, optionally with a query); for a native
    /// page, its first route without parameters by default. Null for a group that only expands.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// A legacy MVC page's route values (area, controller, action and any others), resolved to
    /// its URL through the tenant's link generator. Set instead of <see cref="Url"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? LegacyRoute { get; init; }

    /// <summary>A branch whose children are generated (New › content type): the tenant's menu edits cannot move items into or out of it.</summary>
    public bool Locked { get; init; }

    /// <summary>The caption is data (a content type's or a kind's display name), translated under that data's own context rather than as a menu caption.</summary>
    public bool CaptionIsData { get; init; }
}
