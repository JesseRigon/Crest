#nullable enable

using Grove.DisplayManagement.Extensions;
using Grove.Environment.Extensions;
using Grove.Modules.Manifest;
using Grove.Routing;

namespace Grove.Themes;

/// <summary>
/// How a theme declares which shell it hosts, and how that declaration is read.
/// </summary>
/// <remarks>
/// Manifest tags are the mechanism, because Grove already reads them (<c>admin</c> and
/// <c>hidden</c> are honoured today): a theme tagged <c>admin</c> hosts the admin shell,
/// one tagged <c>member</c> hosts the member shell, and one tagged neither is a site
/// theme. <c>grove-blazor</c> marks a theme as hosting a Grove Blazor shell document rather
/// than a classic Grove view theme.
///
/// <para>
/// <strong>Descendants count.</strong> Grove's <c>BaseTheme</c> chain is exactly "a
/// child fork with simple modifications": a child theme inherits its parent's shapes and
/// assets, so <c>BaseTheme = "Grove.AdminTheme"</c> IS a compatible admin theme.
/// Every check here walks that chain rather than comparing a single id - a fork is the
/// normal way to brand a shell, and an id comparison would reject every fork.
/// </para>
/// </remarks>
public static class ThemeBuckets
{
    public const string AdminTag = "admin";
    public const string MemberTag = "member";

    /// <summary>Marks a theme as hosting a Grove Blazor shell document.</summary>
    public const string GroveBlazorTag = "grove-blazor";

    /// <summary>The bucket a theme's manifest declares.</summary>
    /// <remarks>
    /// Site is the default for a theme that declares neither tag, which keeps every
    /// existing Grove site theme valid with no manifest change - the reason the site
    /// shell deliberately has no bucket tag of its own.
    /// </remarks>
    public static ShellId GetBucket(IManifestInfo? manifest)
    {
        if (HasTag(manifest, MemberTag))
        {
            return ShellId.Member;
        }

        return HasTag(manifest, AdminTag) ? ShellId.Admin : ShellId.Site;
    }

    /// <summary>Whether a theme's own manifest declares a Grove Blazor shell document.</summary>
    public static bool IsGroveBlazor(IManifestInfo? manifest) => HasTag(manifest, GroveBlazorTag);

    /// <summary>
    /// Whether a theme hosts a Grove Blazor shell document, itself or through any theme
    /// in its <c>BaseTheme</c> chain - a fork of a Grove theme is a Grove theme.
    /// </summary>
    /// <remarks>
    /// The one answer to this question: the admin shell's detector and the shell-contract
    /// check both ask it here, so serving the shell and checking compatibility can never
    /// disagree about a theme.
    /// </remarks>
    public static bool IsGroveBlazorTheme(IExtensionInfo? theme, Func<string, IExtensionInfo?> resolve) =>
        WalkBaseThemeChain(theme, resolve).Any(candidate => IsGroveBlazor(candidate.Manifest));

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

            current = ResolveBaseTheme(current, resolve);
        }
    }

    // Grove reads BaseTheme from the theme's manifest attribute onto ThemeExtensionInfo.
    // A theme with no BaseTheme (or a non-theme extension) yields null, which ends the
    // walk - the common case, since most themes declare no base.
    private static IExtensionInfo? ResolveBaseTheme(IExtensionInfo extension, Func<string, IExtensionInfo?> resolve)
    {
        var baseThemeId = (extension as ThemeExtensionInfo)?.BaseTheme;
        return string.IsNullOrWhiteSpace(baseThemeId) ? null : resolve(baseThemeId);
    }
}
