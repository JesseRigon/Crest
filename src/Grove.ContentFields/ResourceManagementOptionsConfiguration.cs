using Microsoft.Extensions.Options;
using Grove.ResourceManagement;

namespace Grove.ContentFields;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    public void Configure(ResourceManagementOptions options)
    {
        var manifest = new ResourceManifest();

        manifest
            .DefineScript("trumbowyg-media-url")
            .SetUrl("~/Grove.ContentFields/Scripts/trumbowyg/trumbowyg.media.url.min.js", "~/Grove.ContentFields/Scripts/trumbowyg/trumbowyg.media.url.js")
            .SetDependencies("trumbowyg")
            .SetVersion("1.0.0");

        manifest
            .DefineScript("trumbowyg-media-tag")
            .SetUrl("~/Grove.ContentFields/Scripts/trumbowyg/trumbowyg.media.tag.min.js", "~/Grove.ContentFields/Scripts/trumbowyg/trumbowyg.media.tag.js")
            .SetDependencies("trumbowyg")
            .SetVersion("1.0.0");

        options.ResourceManifests.Add(manifest);
    }
}
