using Grove.DisplayManagement.Manifest;
using Grove.Environment.Extensions;
using Grove.Environment.Extensions.Features;
using Grove.Modules.Manifest;

namespace Grove.DisplayManagement.Extensions;

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
