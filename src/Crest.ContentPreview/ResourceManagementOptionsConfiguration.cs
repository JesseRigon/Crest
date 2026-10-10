using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.ContentPreview;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("contentpreview-edit")
            .SetUrl("~/Crest.ContentPreview/Scripts/contentpreview.edit.min.js", "~/Crest.ContentPreview/Scripts/contentpreview.edit.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
