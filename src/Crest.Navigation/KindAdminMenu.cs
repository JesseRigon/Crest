using Crest.Admin;
using Crest.Kinds;
using Crest.Security.Permissions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Navigation;

/// <summary>
/// The admin menu of a kind registry: a section whose own page is the registry's "All" page,
/// an "All" child that reaches it (the primary nav turns a parent with children into an
/// expander, never a link), and one child per registered kind in catalog order. The children
/// are what the tenant's layout overlay and a user's own preference hide, and an "All" page
/// reads the same served items to decide which panes to show.
/// </summary>
/// <remarks>
/// The pages are Blazor-only, with no MVC action to route to, so the items carry URLs built
/// from the real <see cref="AdminOptions.AdminUrlPrefix"/> (it replaces the literal "Admin",
/// it is not layered on top of it), matching each page's base-relative <c>@page</c> route.
/// </remarks>
public abstract class KindAdminMenu<TKind>(IOptions<AdminOptions> adminOptions, KindCatalog<TKind> catalog) : AdminNavigationProvider
    where TKind : IKindDescriptor
{
    /// <summary>What the section looks like. The captions come from the derived menu's own localizer, so they are extracted with its module.</summary>
    protected sealed record Section(
        LocalizedString Caption,
        string Position,
        string Id,
        string Icon,
        string AllRoute,
        LocalizedString AllCaption,
        Permission Permission);

    protected abstract Section Describe();

    /// <summary>The kind's caption in the menu, translated by the derived menu's localizer.</summary>
    protected abstract LocalizedString CaptionOf(TKind kind);

    /// <summary>Base-relative route of the kind's page.</summary>
    protected abstract string RouteOf(TKind kind);

    /// <summary>The menu item id of a kind's child; "{section id}-{key}" by default.</summary>
    protected virtual string MenuItemIdOf(Section section, TKind kind) => $"{section.Id}-{kind.Key}";

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        var section = Describe();

        builder.Add(section.Caption, section.Position, root =>
        {
            root.AddClass(section.Id)
                .AddClass("icon-class-" + section.Icon)
                .Id(section.Id)
                .Url($"{adminPath}{section.AllRoute}")
                .Permission(section.Permission);

            root.Add(section.AllCaption, "0", item => item
                .AddClass($"all-{section.Id}")
                .AddClass("icon-class-" + section.Icon)
                .Id($"{section.Id}-all")
                .Url($"{adminPath}{section.AllRoute}")
                .Permission(section.Permission)
                .LocalNav());

            var position = 0;
            foreach (var kind in catalog.Kinds)
            {
                position++;
                root.Add(CaptionOf(kind), position.ToString(System.Globalization.CultureInfo.InvariantCulture), item =>
                {
                    item.AddClass(kind.Key)
                        .Id(MenuItemIdOf(section, kind))
                        .Url($"{adminPath}{RouteOf(kind)}")
                        .Permission(section.Permission)
                        .LocalNav();
                    if (!string.IsNullOrWhiteSpace(kind.Icon))
                    {
                        item.AddClass("icon-class-" + kind.Icon);
                    }
                });
            }
        });

        return ValueTask.CompletedTask;
    }
}
