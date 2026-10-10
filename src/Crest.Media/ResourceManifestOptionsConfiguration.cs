using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Media;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("media")
            .SetUrl("~/Crest.Media/Scripts/media2.min.js", "~/Crest.Media/Scripts/media2.js")
            .SetVersion("2.0.0")
            .SetAttribute("type", "module");

        s_manifest
            .DefineStyle("media")
            .SetUrl("~/Crest.Media/Styles/media2.min.css", "~/Crest.Media/Styles/media2.css")
            .SetVersion("2.0.0");

        s_manifest
            .DefineScript("media-picker")
            .SetUrl("~/Crest.Media/Scripts/media-picker2.min.js", "~/Crest.Media/Scripts/media-picker2.js")
            .SetVersion("2.0.0")
            .SetAttribute("type", "module");

        s_manifest
            .DefineStyle("media-picker")
            .SetUrl("~/Crest.Media/Styles/media-picker2.min.css", "~/Crest.Media/Styles/media-picker2.css")
            .SetVersion("2.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
