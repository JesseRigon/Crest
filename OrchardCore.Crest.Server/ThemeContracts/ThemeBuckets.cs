using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules.Manifest;
using Crest.Routing;

namespace Crest.Themes;

/// <summary>
/// How a theme declares which shell it hosts, and how that declaration is read.
/// </summary>
/// <remarks>
/// Manifest tags are the mechanism, because Orchard already reads them (<c>admin</c> and
/// <c>hidden</c> are honoured today): a theme tagged <c>admin</c> hosts the admin shell,
/// one tagged <c>member</c> hosts the member shell, and one tagged neither is a site
/// theme. <c>crest-blazor</c> (or the legacy <c>blazor</c> tag) marks a theme as hosting a
/// Crest Blazor shell document rather than a classic Orchard view theme.
///
/// <para>
/// <strong>Descendants count.</strong> Orchard's <c>BaseTheme</c> chain is exactly "a
/// child fork with simple modifications": a child theme inherits its parent's shapes and
/// assets, so <c>BaseTheme = "OrchardCore.Crest.Admin"</c> IS a compatible admin theme.
/// Every check here walks that chain rather than comparing a single id - a fork is the
/// normal way to brand a shell, and an id comparison would reject every fork.
/// </para>
/// </remarks>
public static class ThemeBuckets
{
    public const string AdminTag = "admin";
    public const string MemberTag = "member";

    /// <summary>Marks a theme as hosting a Crest Blazor shell document.</summary>
    public const string CrestBlazorTag = "crest-blazor";

    /// <summary>
    /// The tag Crest's own themes have always carried, honoured as an alias so existing
    /// themes and forks keep working without a manifest edit.
    /// </summary>
    public const string LegacyBlazorTag = "blazor";

    /// <summary>The bucket a theme's manifest declares.</summary>
    /// <remarks>
    /// Site is the default for a theme that declares neither tag, which keeps every
    /// existing Orchard site theme valid with no manifest change - the reason the site
    /// shell deliberately has no bucket tag of its own.
    /// </remarks>
    public static RouteBucket GetBucket(IManifestInfo? manifest)
    {
        if (HasTag(manifest, MemberTag))
        {
            return RouteBucket.Member;
        }

        return HasTag(manifest, AdminTag) ? RouteBucket.Admin : RouteBucket.Site;
    }

    /// <summary>Whether a theme hosts a Crest Blazor shell document.</summary>
    public static bool IsCrestBlazor(IManifestInfo? manifest) =>
        HasTag(manifest, CrestBlazorTag) || HasTag(manifest, LegacyBlazorTag);

    public static bool HasTag(IManifestInfo? manifest, string tag) =>
        manifest?.Tags?.Any(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>
    /// The theme and every ancestor of it, nearest first, following <c>BaseTheme</c>.
    /// </summary>
    /// <remarks>
    /// Guards against a cycle (a manifest naming itself, or two themes naming each other)
    /// by refusing to visit an id twice: a malformed manifest must not hang the request
    /// that reads it.
    /// </remarks>
    public static IEnumerable<IExtensionInfo> WalkBaseThemeChain(
        IExtensionInfo? theme,
        Func<string, IExtensionInfo?> resolve)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = theme;

        while (current is not null && visited.Add(current.Id))
        {
            yield return current;

            current = resolve is null ? null : ResolveBaseTheme(current, resolve);
        }
    }

    // Orchard exposes a theme's BaseTheme on its theme extension-info type rather than on
    // IExtensionInfo or IManifestInfo, and that type is not in the abstractions this
    // project references - so the property is read reflectively off whichever concrete
    // extension/manifest object Orchard handed us. The alternative would be taking a
    // reference purely to name a type whose single property we read once.
    //
    // A theme with no BaseTheme (or a non-theme extension) yields null, which ends the
    // walk - the common case, since most themes declare no base.
    private static IExtensionInfo? ResolveBaseTheme(IExtensionInfo extension, Func<string, IExtensionInfo?> resolve)
    {
        var baseThemeId = ReadBaseThemeId(extension) ?? ReadBaseThemeId(extension.Manifest);
        return string.IsNullOrWhiteSpace(baseThemeId) ? null : resolve(baseThemeId);
    }

    private static string? ReadBaseThemeId(object? source) =>
        source?.GetType()
            .GetProperty("BaseTheme", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.GetValue(source) as string;
}
