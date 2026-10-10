using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Themes;

public sealed class ResourceManagementOptionsConfiguration
    : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("theme-head")
            .SetUrl(
                "~/Crest.Themes/Scripts/theme-head/theme-head.min.js",
                "~/Crest.Themes/Scripts/theme-head/theme-head.js"
            )
            .SetVersion("1.0.0");

        s_manifest
            .DefineScript("theme-manager")
            .SetUrl(
                "~/Crest.Themes/Scripts/theme-manager/theme-manager.min.js",
                "~/Crest.Themes/Scripts/theme-manager/theme-manager.js"
            )
            .SetDependencies("theme-head")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
