using Crest.DisplayManagement.Manifest;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Modules.Manifest;

namespace Crest.DisplayManagement.Extensions;

public static class ExtensionInfoExtensions
{
    public static bool IsTheme(this IExtensionInfo extensionInfo) =>
        extensionInfo is IThemeExtensionInfo;

    public static bool IsTheme(this IFeatureInfo featureInfo) =>
        featureInfo.Extension is IThemeExtensionInfo &&
        featureInfo.Id == featureInfo.Extension.Id;

    public static bool IsTheme(this IManifestInfo manifestInfo) =>
        manifestInfo.ModuleInfo is ThemeAttribute ||
        (manifestInfo.ModuleInfo is ModuleMarkerAttribute &&
         manifestInfo.Type.Equals("Theme", StringComparison.OrdinalIgnoreCase));
}
