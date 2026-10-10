using System.Text.RegularExpressions;

namespace Crest.Localization.PortableObject;

/// <summary>
/// Maps a translation's context (<c>msgctxt</c>) from the names the platform had before it was
/// forked from OrchardCore and renamed, to the names it has now. The community translation
/// packages (<c>OrchardCore.Translations.*</c>, from Crowdin) are keyed by the original type and
/// view names; mapping them as they are read keeps those translations applying.
/// </summary>
/// <remarks>
/// The rules mirror the rename itself: <c>OrchardCore.Workflows.*</c> became
/// <c>Crest.Workflows.Platform.*</c>, every other <c>OrchardCore.*</c> became <c>Crest.*</c>,
/// and an identifier carrying the name became neutral (<c>OrchardHelper</c> to
/// <c>PlatformHelper</c>). Contexts that never carried the name are returned unchanged.
/// </remarks>
public static partial class TranslationContextMap
{
    private const string OriginalPrefix = "OrchardCore";

    public static string Map(string context)
    {
        if (string.IsNullOrEmpty(context) || !context.Contains("Orchard", StringComparison.Ordinal))
        {
            return context;
        }

        if (context.StartsWith(OriginalPrefix + ".Workflows.", StringComparison.Ordinal))
        {
            context = "Crest.Workflows.Platform." + context[(OriginalPrefix.Length + ".Workflows.".Length)..];
        }
        else if (context.StartsWith(OriginalPrefix + ".", StringComparison.Ordinal) || context == OriginalPrefix)
        {
            context = "Crest" + context[OriginalPrefix.Length..];
        }

        return IdentifierName().Replace(context, "Platform");
    }

    // "Orchard" or "OrchardCore" as part of a longer identifier: OrchardHelper, IOrchardHelper.
    [GeneratedRegex("(?<=[A-Za-z0-9_])OrchardCore|OrchardCore(?=[A-Za-z0-9])|(?<=[A-Za-z0-9_])Orchard(?![a-z])|(?<![A-Za-z0-9_])Orchard(?=[A-Z0-9_])")]
    private static partial Regex IdentifierName();
}
