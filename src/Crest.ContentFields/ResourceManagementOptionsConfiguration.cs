using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.ContentFields;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    public void Configure(ResourceManagementOptions options)
    {
        var manifest = new ResourceManifest();

        manifest
            .DefineScript("trumbowyg-media-url")
            .SetUrl("~/Crest.ContentFields/Scripts/trumbowyg/trumbowyg.media.url.min.js", "~/Crest.ContentFields/Scripts/trumbowyg/trumbowyg.media.url.js")
            .SetDependencies("trumbowyg")
            .SetVersion("1.0.0");

        manifest
            .DefineScript("trumbowyg-media-tag")
            .SetUrl("~/Crest.ContentFields/Scripts/trumbowyg/trumbowyg.media.tag.min.js", "~/Crest.ContentFields/Scripts/trumbowyg/trumbowyg.media.tag.js")
            .SetDependencies("trumbowyg")
            .SetVersion("1.0.0");

        options.ResourceManifests.Add(manifest);
    }
}
