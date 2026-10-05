using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;

namespace Crest.ViewModels;

public sealed record Feature(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExtensionId,
    string[] Dependencies,
    bool AlwaysEnabled,
    bool Enabled,
    bool EnabledByDependencyOnly,
    // Why this enabled feature's pages cannot work under the active themes, or null when
    // they can. Shown as a badge, so a theme change that broke a feature stays visible
    // after the warning was dismissed.
    string? Incompatibility = null)
{
    public static Feature From(IFeatureInfo feature, bool enabled) => new(
        feature.Id,
        feature.Name ?? feature.Id,
        feature.Category ?? string.Empty,
        feature.Description ?? string.Empty,
        feature.Extension?.Id ?? string.Empty,
        feature.Dependencies ?? [],
        feature.IsAlwaysEnabled,
        enabled,
        feature.EnabledByDependencyOnly);

    public static Feature From(string id, IFeatureInfo? feature, bool enabled = true) => new(
        id,
        feature?.Name ?? id,
        feature?.Category ?? string.Empty,
        feature?.Description ?? string.Empty,
        feature?.Extension?.Id ?? string.Empty,
        feature?.Dependencies ?? [],
        feature?.IsAlwaysEnabled ?? false,
        enabled,
        feature?.EnabledByDependencyOnly ?? false);
}
