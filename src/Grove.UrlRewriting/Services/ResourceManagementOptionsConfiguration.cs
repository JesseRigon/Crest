using Microsoft.Extensions.Options;
using Grove.ResourceManagement;

namespace Grove.UrlRewriting.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("sortable-rules")
            .SetDependencies("Sortable")
            .SetUrl("~/Grove.UrlRewriting/Scripts/sortable-rules.min.js", "~/Grove.UrlRewriting/Scripts/sortable-rules.js");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
