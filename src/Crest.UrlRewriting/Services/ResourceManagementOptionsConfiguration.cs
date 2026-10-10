using Microsoft.Extensions.Options;
using Crest.ResourceManagement;

namespace Crest.UrlRewriting.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest s_manifest;

    static ResourceManagementOptionsConfiguration()
    {
        s_manifest = new ResourceManifest();

        s_manifest
            .DefineScript("sortable-rules")
            .SetDependencies("Sortable")
            .SetUrl("~/Crest.UrlRewriting/Scripts/sortable-rules.min.js", "~/Crest.UrlRewriting/Scripts/sortable-rules.js");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(s_manifest);
    }
}
