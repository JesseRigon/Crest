using Grove.Services;
using Grove.Display.Icons;
using Microsoft.AspNetCore.Mvc;
using Grove.Infrastructure.Shells.Admin;
using Grove.Display.Navigation;

namespace Grove.ViewModels;

public sealed record NavigationMenu(string Name, NavigationItem[] Items, IconPack? Icons = null, NavigationSeparator[]? Separators = null, PrimaryNavMenuSettings? PrimaryNavMenuSettings = null);

/// <summary>The current user's own hidden admin-menu item keys (see GroveUserMenuPreferences).</summary>
public sealed record UserHiddenMenuItems(string[] HiddenItemKeys);

public sealed record NavigationSeparator(string Key, string? ParentKey, int Order);

public sealed record NavigationIcon(string? Key, string Library, string? Version, string? Style, string Name, string? SvgMarkup);

public sealed record NavigationItem(
    string Text,
    string? TextKey,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    NavigationIcon? Icon,
    string[] Classes,
    NavigationItem[] Items)
{
    // Menu labels (Text) are translated and must never be part of the match key - the same
    // item resolves to different Text per admin culture. Every stock Grove admin
    // navigation provider now sets a stable, culture-invariant Id, so Id is the preferred
    // match key.
    //
    // TextKey is MenuItem.Text.Name: the invariant literal a provider passed to S["..."]
    // (the localization key itself, English only by convention),
    // which Grove's own NavigationManager.Merge matches on and which therefore does not
    // vary by admin culture. It is a weaker identifier than Id, because it changes whenever
    // someone rewords the caption in the provider's source, but it is present on every item
    // rather than only on those whose provider bothered to set an Id. Falling back to it lets
    // an item contributed by a third-party provider with no Id still keep a stable handle
    // across culture changes, instead of having none at all.
    public string? Key => !string.IsNullOrEmpty(Id) ? Id : TextKey;
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;

    public static NavigationItem From(MenuItem item) => From(item, captionResolver: null);

    // An item that belongs to an admin menu (MenuName is set only by the admin node navigation
    // builders) carries its node's raw caption as Text - the built LocalizedString is
    // (LinkText, LinkText) - the invariant literal in both slots. Grove's own Razor admin
    // resolves every caption through IDataLocalizer at render time (Grove.Themes.TheAdmin's
    // NavigationItemText.cshtml, keyed on the displayed caption); serialization here is Grove's
    // render time, so resolution happens in the same place, unconditionally - but through
    // MenuCaptionResolver, which falls back through parent and sibling contexts and the
    // shipped PO catalogs before surrendering to the invariant literal - see the resolver's
    // remarks. A total miss returns the caption unchanged, so provider items keep
    // their PO-translated Text.Value.
    public static NavigationItem From(MenuItem item, MenuCaptionResolver? captionResolver)
    {
        var text = item.Text.Value;
        if (captionResolver is not null && !string.IsNullOrEmpty(text))
        {
            text = captionResolver.Resolve(
                text,
                string.IsNullOrEmpty(item.MenuName) ? null : item.MenuName,
                item.Id,
                item.Text.Name);
        }

        return new(
            text,
            item.Text.Name,
            item.Id,
            item.Href,
            item.Url,
            item.Target,
            item.Position,
            null,
            item.Classes.ToArray(),
            item.Items.OrderBy(child => child.Position, NavigationPositionComparer.Instance)
                .Select(child => From(child, captionResolver))
                .ToArray());
    }
}

internal sealed class NavigationPositionComparer : IComparer<string?>
{
    private static readonly char[] SplitChars = ['.', ':'];

    public static NavigationPositionComparer Instance { get; } = new();

    private NavigationPositionComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        var xParts = GetNormalizedPosition(x).Split(SplitChars);
        var yParts = GetNormalizedPosition(y).Split(SplitChars);
        var length = Math.Min(xParts.Length, yParts.Length);

        for (var i = 0; i < length; i++)
        {
            var xIsInt = TryNormalizeKnownPartition(xParts[i], out var xPosition);
            var yIsInt = TryNormalizeKnownPartition(yParts[i], out var yPosition);

            if (!xIsInt)
            {
                xIsInt = xParts[i].Length == 0 || int.TryParse(xParts[i], out xPosition);
            }

            if (!yIsInt)
            {
                yIsInt = yParts[i].Length == 0 || int.TryParse(yParts[i], out yPosition);
            }

            if (!xIsInt && !yIsInt)
            {
                // Position strings are built almost universally upstream via
                // LocalizedString.PrefixPosition(), which bakes in the TRANSLATED display
                // text (LocalizedString.ToString() returns .Value, discarding .Name - see
                // Grove.Display.Navigation.Core's PrefixPosition(LocalizedString) overload).
                // Comparing that text would sort items differently per admin culture. Treat
                // non-numeric segments as tied instead: OrderBy is a stable sort, so ties
                // fall through to the underlying provider's original (culture-invariant)
                // registration order rather than alphabetizing translated words.
                continue;
            }

            if (!xIsInt || (yIsInt && xPosition > yPosition))
            {
                return 1;
            }

            if (!yIsInt || xPosition < yPosition)
            {
                return -1;
            }
        }

        return xParts.Length.CompareTo(yParts.Length);
    }

    private static string GetNormalizedPosition(string? value)
    {
        if (value is null)
        {
            return "before.";
        }

        var trimmed = value.Trim(':').TrimEnd('.');

        return string.IsNullOrWhiteSpace(trimmed) ? "0" : trimmed;
    }

    private static bool TryNormalizeKnownPartition(string partition, out int position)
    {
        if (partition.Equals("before", StringComparison.OrdinalIgnoreCase))
        {
            position = -9999;
            return true;
        }

        if (partition.Equals("after", StringComparison.OrdinalIgnoreCase))
        {
            position = 9999;
            return true;
        }

        position = 0;
        return false;
    }
}
