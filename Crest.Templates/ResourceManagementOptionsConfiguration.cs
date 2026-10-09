using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.Templates;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("templatepreview-edit")
            .SetUrl("~/Crest.Templates/Scripts/templatepreview.edit.min.js", "~/Crest.Templates/Scripts/templatepreview.edit.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
