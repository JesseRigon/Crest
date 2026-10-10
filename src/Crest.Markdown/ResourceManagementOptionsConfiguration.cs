using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Markdown;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("easymde")
            .SetUrl("~/Crest.Markdown/Scripts/easymde.min.js")
            .SetVersion("2.18.0");

        s_manifest
            .DefineStyle("easymde")
            .SetUrl(
                "~/Crest.Markdown/Styles/mde.min.css",
                "~/Crest.Markdown/Styles/mde.css"
            )
            .SetVersion("2.18.0");

        s_manifest
            .DefineScript("easymde-mediatoolbar")
            .SetDependencies("easymde")
            .SetUrl(
                "~/Crest.Markdown/Scripts/mediatoolbar/mde.mediatoolbar.min.js",
                "~/Crest.Markdown/Scripts/mediatoolbar/mde.mediatoolbar.js"
            )
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
